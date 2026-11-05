using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

// G7.1: read-only local lexical index. It is not an agent tool, IPC endpoint or
// permission to disclose workspace content to a model.
internal sealed record CodeSearchHit(string RelativePath, string Sha256, string Excerpt);
internal sealed record CodeIndexStats(int Indexed, int Updated, int Removed, int Skipped);

internal sealed class CodebaseIndex : IDisposable
{
    private const int MaxFiles = 512;
    private const int MaxFileBytes = 128 * 1024;
    private const int MaxTotalBytes = 8 * 1024 * 1024;
    private readonly WorkspaceReader _reader;
    private readonly SqliteConnection _db;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".sql"
    };
    private static readonly Regex UnsafePathName = new(
        @"(^|/)(?:.*(?:secret|password|credential|private[_-]?key|id_rsa|\.env).*)(?:/|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal CodebaseIndex(string root, string databasePath)
    {
        if (!Path.IsPathFullyQualified(databasePath))
            throw new ArgumentException("Index database path must be absolute");
        var workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var database = Path.GetFullPath(databasePath);
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (database.StartsWith(workspace + Path.DirectorySeparatorChar, cmp) ||
            string.Equals(database, workspace, cmp))
            throw new UnauthorizedAccessException("Index must be stored outside the workspace");
        _reader = new WorkspaceReader(root);
        var directory = Path.GetDirectoryName(database)!;
        Directory.CreateDirectory(directory);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL;");
        Exec("PRAGMA synchronous=FULL;");
        using var version = _db.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        if ((long)(version.ExecuteScalar() ?? -1L) is not 0 and not 1)
            throw new InvalidDataException("Unsupported code-index schema version");
        Exec("CREATE TABLE IF NOT EXISTS CodeIndexFiles(" +
             "RelativePath TEXT PRIMARY KEY, Digest TEXT NOT NULL, Content TEXT NOT NULL);");
        Exec("CREATE VIRTUAL TABLE IF NOT EXISTS CodeIndexFts " +
             "USING fts5(RelativePath UNINDEXED, Content, tokenize='unicode61');");
        Exec("PRAGMA user_version=1;");
    }

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static bool IsEligible(string relative)
    {
        if (UnsafePathName.IsMatch(relative)) return false;
        return AllowedExtensions.Contains(Path.GetExtension(relative));
    }

    internal CodeIndexStats Refresh()
    {
        // Do not silently produce a partial index and then incorrectly delete entries.
        var paths = _reader.ListTextCandidates(maxEntries: MaxFiles, maxDepth: 12);
        if (paths.Count >= MaxFiles)
            throw new InvalidOperationException("Workspace exceeds safe index file limit");

        var existing = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT RelativePath, Digest FROM CodeIndexFiles;";
            using var result = cmd.ExecuteReader();
            while (result.Read()) existing[result.GetString(0)] = result.GetString(1);
        }

        var snapshots = new List<WorkspaceTextSnapshot>();
        var skipped = 0;
        var bytes = 0;
        foreach (var path in paths)
        {
            if (!IsEligible(path)) { skipped++; continue; }
            try
            {
                var snapshot = _reader.ReadText(path);
                if (snapshot.ByteLength > MaxFileBytes) { skipped++; continue; }
                bytes = checked(bytes + (int)snapshot.ByteLength);
                if (bytes > MaxTotalBytes)
                    throw new InvalidOperationException("Code index budget exceeded");
                snapshots.Add(snapshot);
            }
            catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException
                or DirectoryNotFoundException or UnauthorizedAccessException or IOException)
            {
                skipped++;
            }
        }

        // A read error must not be interpreted as safe evidence that an indexed file vanished.
        var scanned = paths.Where(IsEligible).ToHashSet(StringComparer.Ordinal);
        var updated = 0;
        var removed = 0;
        using var tx = _db.BeginTransaction();
        foreach (var item in snapshots)
        {
            if (existing.TryGetValue(item.RelativePath, out var digest) && digest == item.Sha256)
                continue;
            ExecForPath(tx, "DELETE FROM CodeIndexFts WHERE RelativePath = $path", item.RelativePath);
            ExecForPath(tx, "DELETE FROM CodeIndexFiles WHERE RelativePath = $path", item.RelativePath);
            using (var cmd = _db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO CodeIndexFiles (RelativePath, Digest, Content) " +
                                  "VALUES ($path, $hash, $content)";
                cmd.Parameters.AddWithValue("$path", item.RelativePath);
                cmd.Parameters.AddWithValue("$hash", item.Sha256);
                cmd.Parameters.AddWithValue("$content", item.Content);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = _db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO CodeIndexFts (RelativePath, Content) VALUES ($path, $content)";
                cmd.Parameters.AddWithValue("$path", item.RelativePath);
                cmd.Parameters.AddWithValue("$content", item.Content);
                cmd.ExecuteNonQuery();
            }
            updated++;
        }
        // Remove only confirmed disappeared files, not files that failed to read.
        foreach (var old in existing.Keys)
        {
            if (scanned.Contains(old)) continue;
            ExecForPath(tx, "DELETE FROM CodeIndexFts WHERE RelativePath = $path", old);
            ExecForPath(tx, "DELETE FROM CodeIndexFiles WHERE RelativePath = $path", old);
            removed++;
        }
        tx.Commit();
        return new CodeIndexStats(snapshots.Count, updated, removed, skipped);
    }

    private void ExecForPath(SqliteTransaction tx, string sql, string path)
    {
        using var cmd = _db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$path", path);
        cmd.ExecuteNonQuery();
    }

    internal IReadOnlyList<CodeSearchHit> Search(string query, int maxHits = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 160 || maxHits is < 1 or > 20 ||
            !Regex.IsMatch(query, @"^[\p{L}\p{N}_ ]+$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Search terms must be plain words");
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length is < 1 or > 4 || terms.Any(x => x.Length > 40))
            throw new ArgumentException("Use 1 to 4 short search terms");
        var match = string.Join(" AND ", terms.Select(x => "\"" + x + "\""));
        var candidates = new List<(string Path, string Digest, string Content)>();
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText =
                "SELECT i.RelativePath, i.Digest, i.Content FROM CodeIndexFts " +
                "JOIN CodeIndexFiles i ON i.RelativePath = CodeIndexFts.RelativePath " +
                "WHERE CodeIndexFts MATCH $query LIMIT $limit;";
            cmd.Parameters.AddWithValue("$query", match);
            cmd.Parameters.AddWithValue("$limit", maxHits);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                candidates.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        var output = new List<CodeSearchHit>();
        foreach (var hit in candidates)
        {
            // Never surface stale content after an external file mutation.
            try
            {
                if (_reader.ReadText(hit.Path).Sha256 != hit.Digest) continue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or InvalidDataException) { continue; }
            var at = hit.Content.IndexOf(terms[0], StringComparison.OrdinalIgnoreCase);
            var start = Math.Max(0, at >= 0 ? at - 50 : 0);
            var length = Math.Min(180, hit.Content.Length - start);
            var excerpt = hit.Content.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ');
            output.Add(new CodeSearchHit(hit.Path, hit.Digest, excerpt));
        }
        return output.AsReadOnly();
    }

    public void Dispose() => _db.Dispose();
}

internal static class CodebaseIndexSpike
{
    internal static bool Run()
    {
        var temp = Path.Combine(Path.GetTempPath(), "escode-index-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(temp, "workspace");
        var db = Path.Combine(temp, "index.sqlite3");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        try
        {
            File.WriteAllText(Path.Combine(root, "src", "Customers.cs"),
                "namespace Sample; public class CustomerLedger { public string CustomerId = \"customer\"; }");
            File.WriteAllText(Path.Combine(root, "src", "engine.py"),
                "def calculate_balance(): return 5");
            File.WriteAllText(Path.Combine(root, "src", "secrets.ts"), "const secret = 'must-not-index';");
            File.WriteAllText(Path.Combine(root, ".env"), "token=must-not-index");
            File.WriteAllText(Path.Combine(root, ".git", "config"), "must-not-index");
            using (var index = new CodebaseIndex(root, db))
            {
                var first = index.Refresh();
                if (first.Indexed != 2 || first.Updated != 2) return false;
                var hit = index.Search("CustomerLedger");
                if (hit.Count != 1 || hit[0].RelativePath != "src/Customers.cs") return false;
                if (index.Search("must").Count != 0) return false;
                try { index.Search("CustomerLedger OR *"); return false; }
                catch (ArgumentException) { }
                File.WriteAllText(Path.Combine(root, "src", "Customers.cs"),
                    "namespace Sample; public class InvoiceLedger { }");
                if (index.Search("CustomerLedger").Count != 0) return false; // stale index
                var next = index.Refresh();
                if (next.Updated != 1 || index.Search("InvoiceLedger").Count != 1) return false;
                File.Delete(Path.Combine(root, "src", "engine.py"));
                if (index.Refresh().Removed != 1) return false;
            }
            using (var reopened = new CodebaseIndex(root, db))
            {
                if (reopened.Search("InvoiceLedger").Count != 1 ||
                    reopened.Search("calculate_balance").Count != 0) return false;
                if (reopened.Refresh().Updated != 0) return false;
            }
            return true;
        }
        finally { Directory.Delete(temp, recursive: true); }
    }
}

internal static class CodebaseIndexCommand
{
    private static string? Option(string[] args, string key)
    {
        var i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    internal static int Run(string[] args)
    {
        var root = Option(args, "--workspace");
        var db = Option(args, "--database");
        var query = Option(args, "--query");
        if (root is null || db is null) return 2;
        try
        {
            using var index = new CodebaseIndex(root, db);
            var stats = index.Refresh();
            Console.WriteLine($"INDEX_REFRESH_OK indexed={stats.Indexed} changed={stats.Updated} removed={stats.Removed} skipped={stats.Skipped}");
            if (query is not null)
            {
                foreach (var hit in index.Search(query))
                    Console.WriteLine($"{hit.RelativePath} [{hit.Sha256}]: {hit.Excerpt}");
            }
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException
            or IOException or SqliteException or UnauthorizedAccessException
            or InvalidOperationException or OverflowException)
        {
            Console.Error.WriteLine($"INDEX_FAILED: {ex.GetType().Name}");
            return 1;
        }
    }
}
