using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

// G3 trusted Host: global metadata is physically isolated from each workspace
// SQLite journal. Does not accept renderer/model-selected database paths.
internal sealed class JournalPartitions
{
    private readonly string _stateRoot;
    private static readonly Regex Id = new(
        "^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal JournalPartitions(string trustedAbsoluteStateRoot)
    {
        if (!Path.IsPathFullyQualified(trustedAbsoluteStateRoot))
            throw new ArgumentException("State path must be absolute");
        if (OperatingSystem.IsWindows() &&
            trustedAbsoluteStateRoot.StartsWith(@"\\", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Network-backed state store denied");
        _stateRoot = Path.GetFullPath(trustedAbsoluteStateRoot);
        EnsureUnredirected(_stateRoot);
        Directory.CreateDirectory(_stateRoot);
        EnsureUnredirected(_stateRoot);
    }

    internal SqliteJournal OpenGlobal() => new(Path.Combine(_stateRoot, "global.sqlite3"));

    internal SqliteJournal OpenWorkspace(string workspaceId)
    {
        if (workspaceId is null || !Id.IsMatch(workspaceId))
            throw new ArgumentException("Invalid workspace database identity");
        var directory = Path.Combine(_stateRoot, "workspaces");
        EnsureUnredirected(directory);
        Directory.CreateDirectory(directory);
        EnsureUnredirected(directory);
        return new SqliteJournal(Path.Combine(directory, workspaceId + ".sqlite3"));
    }

    private static void EnsureUnredirected(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Symlinked/junction state path denied");
    }
}

internal static class JournalPartitionSpike
{
    private const string Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static bool Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "escode-db-partition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var store = new JournalPartitions(Path.Combine(folder, "state"));
            using (var global = store.OpenGlobal())
            using (var first = store.OpenWorkspace("project-a"))
            using (var second = store.OpenWorkspace("project-b"))
            {
                global.Prepare("same-id", "global-task", Digest, false);
                first.Prepare("same-id", "workspace-task", Digest, false);
                second.Prepare("same-id", "other-task", Digest, false);
                first.MarkExecuting("same-id");
                if (global.Read("same-id")?.Kind != "global-task" ||
                    first.Read("same-id")?.State != JournalState.Executing ||
                    second.Read("same-id")?.Kind != "other-task" ||
                    second.Read("same-id")?.State != JournalState.Prepared) return false;
                first.Checkpoint("cp1", "project-a", "story/test", Digest);
                if (first.CheckpointCount() != 1 || second.CheckpointCount() != 0 ||
                    global.CheckpointCount() != 0) return false;
                try { using var duplicate = store.OpenWorkspace("project-a"); return false; }
                catch (IOException) { }
            }
            using (var persisted = store.OpenWorkspace("project-a"))
            {
                if (persisted.RecoverInterrupted() != 1 || persisted.RecoverInterrupted() != 0 ||
                    persisted.Read("same-id")?.State != JournalState.OutcomeUnknown ||
                    persisted.CanRetry("same-id") || persisted.CheckpointCount() != 1)
                    return false;
            }
            using (var global = store.OpenGlobal())
            using (var second = store.OpenWorkspace("project-b"))
            {
                if (global.Read("same-id")?.State != JournalState.Prepared ||
                    second.Read("same-id")?.State != JournalState.Prepared)
                    return false;
            }
            foreach (var bad in new[] { "../escape", "PROJECT-A", ".git", "", "foo/bar" })
            {
                try { using var denied = store.OpenWorkspace(bad); return false; }
                catch (ArgumentException) { }
            }
            return ValidateMigrationRollback(folder);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); }
            catch (IOException) { /* Windows cleanup best effort */ }
        }
    }

    private static bool ValidateMigrationRollback(string folder)
    {
        var db = Path.Combine(folder, "malformed.sqlite3");
        using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = db, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString()))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE operations(id TEXT PRIMARY KEY); PRAGMA user_version=0;";
            cmd.ExecuteNonQuery();
        }

        try { using var opened = new SqliteJournal(db); return false; }
        catch (InvalidDataException) { }

        using var inspect = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = db, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString());
        inspect.Open();
        using var check = inspect.CreateCommand();
        check.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt64(check.ExecuteScalar()) != 0L) return false;
        check.CommandText =
            "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('events','checkpoints');";
        if (Convert.ToInt64(check.ExecuteScalar()) != 0L) return false;
        return true;
    }
}
