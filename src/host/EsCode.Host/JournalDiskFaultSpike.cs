using System.Diagnostics;
using Microsoft.Data.Sqlite;

// G3 T05 deterministic SQLite page-quota fault and actual killed writer.
// Quota exhaustion is not a substitute for a full physical disk/OS power-loss test.
internal static class JournalDiskFaultSpike
{
    internal static async Task<int> ChildAsync(string[] args)
    {
        var at = Array.IndexOf(args, "--database");
        if (at < 0 || at + 1 >= args.Length) return 2;
        var database = args[at + 1];
        if (!Path.IsPathFullyQualified(database) ||
            !Path.GetFileName(Path.GetDirectoryName(database) ?? "")
                .StartsWith("escode-sqlite-fault-", StringComparison.Ordinal))
            return 2;
        using var conn = Open(database);
        Execute(conn, "PRAGMA journal_mode=WAL;");
        Execute(conn, "PRAGMA synchronous=FULL;");
        Execute(conn, "CREATE TABLE IF NOT EXISTS tx_test (id INTEGER PRIMARY KEY, payload BLOB);");
        using var tx = conn.BeginTransaction();
        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO tx_test (id,payload) VALUES (1,@bytes);";
        insert.Parameters.AddWithValue("@bytes", new byte[64 * 1024]);
        insert.ExecuteNonQuery();
        Console.WriteLine("UNCLOSED_SQLITE_TRANSACTION");
        await Console.Out.FlushAsync();
        // No Commit, Rollback, or Dispose before the parent kills this process.
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 0;
    }

    internal static async Task<bool> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "escode-sqlite-fault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            if (!VerifyFullQuota(Path.Combine(root, "quota.sqlite3"))) return false;
            if (!await VerifyKilledTransactionAsync(Path.Combine(root, "crash.sqlite3")))
                return false;
            return VerifyBadUpgradeRollsBack(Path.Combine(root, "legacy.sqlite3"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool VerifyFullQuota(string path)
    {
        using var conn = Open(path);
        Execute(conn, "PRAGMA journal_mode=DELETE;");
        Execute(conn, "PRAGMA synchronous=FULL;");
        Execute(conn, "CREATE TABLE limited (id INTEGER PRIMARY KEY,payload BLOB NOT NULL);");
        var before = ScalarLong(conn, "PRAGMA page_count;");
        var limit = before + 3;
        if (ScalarLong(conn, $"PRAGMA max_page_count={limit};") != limit) return false;
        var full = false;
        try
        {
            using var tx = conn.BeginTransaction();
            using var command = conn.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "INSERT INTO limited(payload) VALUES (@bytes);";
            command.Parameters.AddWithValue("@bytes", new byte[2 * 1024 * 1024]);
            command.ExecuteNonQuery();
            tx.Commit();
        }
        catch (SqliteException e) when (e.SqliteErrorCode == 13) { full = true; }
        if (!full || ScalarLong(conn, "SELECT COUNT(*) FROM limited;") != 0)
            return false;
        return ScalarString(conn, "PRAGMA integrity_check;") == "ok";
    }

    private static async Task<bool> VerifyKilledTransactionAsync(string database)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(JournalDiskFaultSpike).Assembly.Location);
        start.ArgumentList.Add("--journal-fault-child");
        start.ArgumentList.Add("--database");
        start.ArgumentList.Add(database);
        using var child = Process.Start(start);
        if (child is null) return false;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            string? marker;
            try { marker = await child.StandardOutput.ReadLineAsync(deadline.Token); }
            catch (OperationCanceledException) { return false; }
            if (marker != "UNCLOSED_SQLITE_TRANSACTION") return false;
            child.Kill(entireProcessTree: true);
            try { await child.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException) { return false; }

            using var conn = Open(database);
            if (ScalarLong(conn, "SELECT COUNT(*) FROM tx_test;") != 0)
                return false;
            return ScalarString(conn, "PRAGMA integrity_check;") == "ok";
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
        }
    }

    private static bool VerifyBadUpgradeRollsBack(string path)
    {
        using (var conn = Open(path))
        {
            Execute(conn, """
                CREATE TABLE operations (
                  id TEXT PRIMARY KEY,kind TEXT NOT NULL,state TEXT NOT NULL,
                  args_sha256 TEXT NOT NULL,idempotent INTEGER NOT NULL,
                  evidence_effect_absent INTEGER NOT NULL,updated_utc TEXT NOT NULL);
                CREATE TABLE events (
                  sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                  operation_id TEXT NOT NULL,state TEXT NOT NULL,updated_utc TEXT NOT NULL);
                CREATE TABLE checkpoints (
                  id TEXT PRIMARY KEY,workspace_id TEXT NOT NULL,
                  git_ref TEXT NOT NULL,manifest_sha256 TEXT NOT NULL,
                  created_utc TEXT NOT NULL);
                CREATE TABLE session_snapshots(unexpected TEXT);
                PRAGMA user_version=1;
                """);
        }
        try { using var journal = new SqliteJournal(path); return false; }
        catch (InvalidDataException) { }
        using var inspect = Open(path);
        if (ScalarLong(inspect, "PRAGMA user_version;") != 1) return false;
        if (ScalarLong(inspect, """
            SELECT COUNT(*) FROM pragma_table_info('session_snapshots')
            WHERE name='unexpected';
            """) != 1) return false;
        return ScalarLong(inspect, """
            SELECT COUNT(*) FROM pragma_table_info('session_snapshots')
            WHERE name='encrypted_payload';
            """) == 0;
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long ScalarLong(SqliteConnection conn, string sql)
    {
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string? ScalarString(SqliteConnection conn, string sql)
    {
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() as string;
    }
}
