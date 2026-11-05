using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

// G7.1 trusted Host-only lexical symbol navigation + bounded context preview.
// Not a compiler, LSP, plugin executor, IPC tool or automatic model attachment.
internal sealed record IndexedSymbol(string RelativePath, string Sha256,
    string Name, string Kind, int Line);
internal sealed record IndexedContext(string RelativePath, string Sha256,
    int StartLine, string Text, bool UntrustedSource);

internal sealed class CodeSymbolIndex : IDisposable
{
    private const int MaxFiles = 512;
    private const int MaxFileBytes = 128 * 1024;
    private const int MaxTotalBytes = 8 * 1024 * 1024;
    private static readonly Regex Name = new(
        "^[A-Za-z_][A-Za-z0-9_]{0,79}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".sql" };
    private static readonly Regex Sensitive = new(
        @"(?:^|/)[^/]*(?:secret|password|credential|private[_-]?key|id_rsa|\.env)[^/]*(?:/|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CsSymbols = new(
        @"\b(class|interface|struct|enum|record)\s+(?:class\s+)?([A-Za-z_][A-Za-z0-9_]{0,79})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ScriptSymbols = new(
        @"\b(?:export\s+)?(?:default\s+)?(?:async\s+)?(function|class|interface|type|enum)\s+([A-Za-z_][A-Za-z0-9_]{0,79})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PythonSymbols = new(
        @"^\s*(?:async\s+)?(def|class)\s+([A-Za-z_][A-Za-z0-9_]{0,79})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SqlSymbols = new(
        @"^\s*(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?(PROC(?:EDURE)?|FUNCTION|VIEW)\s+(?:[A-Za-z_][A-Za-z0-9_]*\.)?([A-Za-z_][A-Za-z0-9_]{0,79})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly WorkspaceReader _workspace;
    private readonly SqliteConnection _database;
    internal CodeSymbolIndex(string trustedWorkspaceRoot, string trustedAbsoluteDatabase)
    {
        if (!Path.IsPathFullyQualified(trustedAbsoluteDatabase))
            throw new ArgumentException("Symbol index DB path must be absolute");
        _workspace = new WorkspaceReader(trustedWorkspaceRoot);
        var workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trustedWorkspaceRoot));
        var db = Path.GetFullPath(trustedAbsoluteDatabase);
        var cmp = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (db.StartsWith(workspace + Path.DirectorySeparatorChar, cmp) ||
            string.Equals(workspace, db, cmp))
            throw new UnauthorizedAccessException("Symbol index must be outside workspace");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        _database = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = db, Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private, Pooling = false
        }.ToString());
        _database.Open();
        RunSql("PRAGMA journal_mode=WAL;");
        RunSql("PRAGMA synchronous=FULL;");
        using (var version = _database.CreateCommand())
        {
            version.CommandText = "PRAGMA user_version;";
            if (Convert.ToInt64(version.ExecuteScalar()) is not 0 and not 1)
                throw new InvalidDataException("Unsupported symbol index version");
        }
        RunSql("CREATE TABLE IF NOT EXISTS SymbolFiles(" +
               "RelativePath TEXT PRIMARY KEY, Digest TEXT NOT NULL);");
        RunSql("CREATE TABLE IF NOT EXISTS Symbols(" +
               "RelativePath TEXT NOT NULL, Digest TEXT NOT NULL, Name TEXT NOT NULL," +
               "Kind TEXT NOT NULL, Line INTEGER NOT NULL," +
               "PRIMARY KEY(RelativePath, Name, Kind, Line));");
        RunSql("CREATE INDEX IF NOT EXISTS IX_Symbols_Name ON Symbols(Name COLLATE NOCASE);");
        RunSql("PRAGMA user_version=1;");
    }
    private void RunSql(string sql)
    {
        using var command = _database.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
    private static bool Eligible(string path) =>
        Extensions.Contains(Path.GetExtension(path)) && !Sensitive.IsMatch(path);

    private static IEnumerable<(string Name, string Kind, int Line)> Extract(
        WorkspaceTextSnapshot snapshot)
    {
        var ext = Path.GetExtension(snapshot.RelativePath).ToLowerInvariant();
        var regex = ext switch
        {
            ".cs" => CsSymbols,
            ".js" or ".jsx" or ".ts" or ".tsx" => ScriptSymbols,
            ".py" => PythonSymbols,
            ".sql" => SqlSymbols,
            _ => throw new InvalidOperationException("Unexpected source language")
        };
        var lines = snapshot.Content.Replace("\r\n", "\n").Split('\n');
        var emitted = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 4096) continue;
            var matches = regex.Matches(lines[i]);
            foreach (Match match in matches)
            {
                if (!match.Success) continue;
                yield return (match.Groups[2].Value, match.Groups[1].Value, i + 1);
                if (++emitted >= 64) yield break;
            }
        }
    }

    internal (int Files, int Updated, int Removed) Refresh()
    {
        var paths = _workspace.ListTextCandidates(maxEntries: MaxFiles, maxDepth: 12);
        if (paths.Count >= MaxFiles)
            throw new InvalidOperationException("Symbol index file limit reached");
        var current = new List<WorkspaceTextSnapshot>();
        var totalBytes = 0;
        foreach (var path in paths.Where(Eligible))
        {
            try
            {
                var file = _workspace.ReadText(path);
                if (file.ByteLength > MaxFileBytes) continue;
                totalBytes = checked(totalBytes + (int)file.ByteLength);
                if (totalBytes > MaxTotalBytes)
                    throw new InvalidOperationException("Symbol index byte budget exceeded");
                current.Add(file);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or
                UnauthorizedAccessException or FileNotFoundException) { }
        }

        var existing = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var command = _database.CreateCommand())
        {
            command.CommandText = "SELECT RelativePath,Digest FROM SymbolFiles;";
            using var reader = command.ExecuteReader();
            while (reader.Read()) existing[reader.GetString(0)] = reader.GetString(1);
        }

        var visited = paths.Where(Eligible).ToHashSet(StringComparer.Ordinal);
        var updated = 0;
        var removed = 0;
        using var transaction = _database.BeginTransaction();
        void Delete(string path)
        {
            foreach (var table in new[] { "Symbols", "SymbolFiles" })
            {
                using var cmd = _database.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = $"DELETE FROM {table} WHERE RelativePath=$path;";
                cmd.Parameters.AddWithValue("$path", path);
                cmd.ExecuteNonQuery();
            }
        }
        foreach (var file in current)
        {
            if (existing.TryGetValue(file.RelativePath, out var hash) &&
                hash == file.Sha256) continue;
            Delete(file.RelativePath);
            using (var cmd = _database.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO SymbolFiles (RelativePath,Digest) VALUES ($path,$hash);";
                cmd.Parameters.AddWithValue("$path", file.RelativePath);
                cmd.Parameters.AddWithValue("$hash", file.Sha256);
                cmd.ExecuteNonQuery();
            }
            foreach (var symbol in Extract(file))
            {
                using var cmd = _database.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT OR IGNORE INTO Symbols " +
                    "(RelativePath,Digest,Name,Kind,Line) VALUES ($path,$hash,$name,$kind,$line);";
                cmd.Parameters.AddWithValue("$path", file.RelativePath);
                cmd.Parameters.AddWithValue("$hash", file.Sha256);
                cmd.Parameters.AddWithValue("$name", symbol.Name);
                cmd.Parameters.AddWithValue("$kind", symbol.Kind);
                cmd.Parameters.AddWithValue("$line", symbol.Line);
                cmd.ExecuteNonQuery();
            }
            updated++;
        }
        // Read failures are NOT evidence that the original source was deleted.
        foreach (var missing in existing.Keys.Where(k => !visited.Contains(k)))
        {
            Delete(missing);
            removed++;
        }
        transaction.Commit();
        return (current.Count, updated, removed);
    }

    internal IReadOnlyList<IndexedSymbol> Search(string symbol, int maxHits = 10)
    {
        if (symbol is null || !Name.IsMatch(symbol) || maxHits is < 1 or > 20)
            throw new ArgumentException("Invalid symbol lookup");
        var candidates = new List<IndexedSymbol>();
        using (var cmd = _database.CreateCommand())
        {
            cmd.CommandText = "SELECT RelativePath,Digest,Name,Kind,Line FROM Symbols " +
                              "WHERE Name=$name COLLATE NOCASE ORDER BY RelativePath,Line LIMIT $limit;";
            cmd.Parameters.AddWithValue("$name", symbol);
            cmd.Parameters.AddWithValue("$limit", maxHits);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                candidates.Add(new IndexedSymbol(reader.GetString(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetInt32(4)));
        }
        var output = new List<IndexedSymbol>();
        foreach (var hit in candidates)
        {
            try
            {
                if (_workspace.ReadText(hit.RelativePath).Sha256 == hit.Sha256)
                    output.Add(hit);
            }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or
                UnauthorizedAccessException or InvalidDataException) { }
        }
        return output.AsReadOnly();
    }

    internal IReadOnlyList<IndexedContext> Context(string symbol, int maxChars = 2400)
    {
        if (maxChars is < 128 or > 8000)
            throw new ArgumentOutOfRangeException(nameof(maxChars));
        var output = new List<IndexedContext>();
        var remaining = maxChars;
        foreach (var hit in Search(symbol, maxHits: 10))
        {
            var file = _workspace.ReadText(hit.RelativePath);
            if (file.Sha256 != hit.Sha256) continue; // external edit since Search
            var lines = file.Content.Replace("\r\n", "\n").Split('\n');
            var start = Math.Max(0, hit.Line - 3);
            var end = Math.Min(lines.Length, hit.Line + 2);
            var snippet = string.Join('\n', lines[start..end]);
            if (snippet.Length > remaining) snippet = snippet[..remaining];
            if (snippet.Length == 0) break;
            output.Add(new IndexedContext(hit.RelativePath, hit.Sha256, start + 1,
                snippet, UntrustedSource: true));
            remaining -= snippet.Length;
            if (remaining < 128) break;
        }
        return output.AsReadOnly();
    }

    public void Dispose() => _database.Dispose();
}

internal static class CodeSymbolIndexSpike
{
    internal static bool Run()
    {
        var temp = Path.Combine(Path.GetTempPath(),
            "escode-symbols-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(temp, "workspace");
        var db = Path.Combine(temp, "state", "symbols.sqlite3");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        try
        {
            File.WriteAllText(Path.Combine(root, "src", "People.cs"),
                "namespace Demo;\npublic class CustomerLedger { }");
            File.WriteAllText(Path.Combine(root, "src", "service.ts"),
                "export async function loadCustomers() { return 1; }");
            File.WriteAllText(Path.Combine(root, "src", "task.py"),
                "def calculate_balance():\n    return 42");
            File.WriteAllText(Path.Combine(root, "src", "report.sql"),
                "CREATE PROCEDURE dbo.GetReports AS SELECT 1;");
            File.WriteAllText(Path.Combine(root, "src", "secret-settings.ts"),
                "export function mustNotIndex() {}");
            File.WriteAllText(Path.Combine(root, ".env"), "TOKEN=private");
            using (var idx = new CodeSymbolIndex(root, db))
            {
                if (idx.Refresh().Updated != 4) return false;
                foreach (var symbol in new[] {
                    "CustomerLedger", "loadCustomers", "calculate_balance", "GetReports" })
                    if (idx.Search(symbol).Count != 1) return false;
                if (idx.Search("mustNotIndex").Count != 0) return false;
                var context = idx.Context("CustomerLedger", maxChars: 256);
                if (context.Count != 1 || !context[0].UntrustedSource ||
                    !context[0].Text.Contains("CustomerLedger") ||
                    context.Sum(x => x.Text.Length) > 256) return false;
                try { idx.Search("CustomerLedger OR *"); return false; }
                catch (ArgumentException) { }
                File.WriteAllText(Path.Combine(root, "src", "People.cs"),
                    "namespace Demo;\npublic class InvoiceLedger { }");
                if (idx.Search("CustomerLedger").Count != 0) return false;
                if (idx.Refresh().Updated != 1 || idx.Search("InvoiceLedger").Count != 1)
                    return false;
                File.Delete(Path.Combine(root, "src", "task.py"));
                if (idx.Refresh().Removed != 1 ||
                    idx.Search("calculate_balance").Count != 0) return false;
            }
            using (var reopened = new CodeSymbolIndex(root, db))
            {
                if (reopened.Search("InvoiceLedger").Count != 1 ||
                    reopened.Search("calculate_balance").Count != 0 ||
                    reopened.Refresh().Updated != 0) return false;
            }
            return true;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); }
            catch (IOException) { }
        }
    }
}

internal static class CodeSymbolIndexCommand
{
    private static string? Option(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    internal static int Run(string[] args)
    {
        var root = Option(args, "--workspace");
        var db = Option(args, "--database");
        var symbol = Option(args, "--symbol");
        if (root is null || db is null || symbol is null) return 2;
        try
        {
            using var index = new CodeSymbolIndex(root, db);
            index.Refresh();
            foreach (var result in index.Search(symbol))
                Console.WriteLine($"{result.RelativePath}:{result.Line} {result.Kind} {result.Name}");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
            or InvalidDataException or SqliteException or IOException or UnauthorizedAccessException
            or OverflowException)
        {
            Console.Error.WriteLine("Symbol lookup failed: " + ex.GetType().Name);
            return 1;
        }
    }
}
