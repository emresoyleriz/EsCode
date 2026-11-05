using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

// T04/T05: Windows deterministic on-disk encrypted checkpoint and rollback tests.
internal static class SessionCheckpointSpike
{
    private const string Digest =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static bool Run()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "escode-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var db = Path.Combine(root, "workspace.sqlite3");
        var backup = Path.Combine(root, "backup.sqlite3");
        var plaintext = "unsaved editor text " + Guid.NewGuid().ToString("N");
        try
        {
            using (var journal = new SqliteJournal(db))
            {
                if (ReadVersion(db) != 2) return false;
                journal.Prepare("remote1", "external-call", Digest, idempotent: true);
                journal.MarkExecuting("remote1");
                var buffers = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["src/Program.cs"] = plaintext,
                    ["src/main.ts"] = "export const valid = true;"
                };
                journal.StoreSessionCheckpoint("cp1", "project1", "story1",
                    "Paused", buffers, new[] { "remote1" });
                if (journal.CheckpointCount() != 1) return false;
                var preview = journal.RestoreSessionCheckpoint("cp1", "project1");
                if (!preview.RequiresHumanReview ||
                    preview.UnsavedBuffers["src/Program.cs"] != plaintext ||
                    preview.RunState != "Paused" ||
                    preview.PendingOperations.Single().LastKnownState != JournalState.Executing)
                    return false;
                try { journal.RestoreSessionCheckpoint("cp1", "project2"); return false; }
                catch (UnauthorizedAccessException) { }
                try
                {
                    journal.StoreSessionCheckpoint("cp1", "project1", "story1",
                        "Paused", buffers, Array.Empty<string>());
                    return false;
                }
                catch (SqliteException) { }
                if (journal.CheckpointCount() != 1) return false;
                foreach (var bad in new[] { "../outside.txt", ".env", "src/secrets.cs",
                    @"C:\outside.txt", "src/../Program.cs" })
                {
                    try
                    {
                        journal.StoreSessionCheckpoint("bad" + Guid.NewGuid().ToString("N")[..8],
                            "project1", "story1", "Paused",
                            new Dictionary<string, string> { [bad] = "untrusted" },
                            Array.Empty<string>());
                        return false;
                    }
                    catch (ArgumentException) { }
                }
                try
                {
                    journal.StoreSessionCheckpoint("oversize", "project1", "story1",
                        "Paused", new Dictionary<string, string>
                        { ["src/big.cs"] = new string('x', 128 * 1024 + 1) },
                        Array.Empty<string>());
                    return false;
                }
                catch (ArgumentException) { }
                try
                {
                    journal.StoreSessionCheckpoint("active", "project1", "story1",
                        "Running", buffers, Array.Empty<string>());
                    return false;
                }
                catch (ArgumentException) { }
                if (journal.CheckpointCount() != 1) return false;
                journal.Backup(backup);
            }

            using (var journal = new SqliteJournal(db))
            {
                if (journal.RecoverInterrupted() != 1 ||
                    journal.RecoverInterrupted() != 0 ||
                    journal.CanRetry("remote1")) return false;
                var preview = journal.RestoreSessionCheckpoint("cp1", "project1");
                if (preview.PendingOperations.Single().LastKnownState !=
                        JournalState.OutcomeUnknown ||
                    !preview.RequiresHumanReview ||
                    preview.UnsavedBuffers["src/Program.cs"] != plaintext)
                    return false;
            }

            using (var journal = new SqliteJournal(backup))
            {
                if (journal.RestoreSessionCheckpoint("cp1", "project1")
                        .UnsavedBuffers["src/Program.cs"] != plaintext)
                    return false;
                if (journal.RecoverInterrupted() != 1 ||
                    journal.CanRetry("remote1")) return false;
            }

            // Prove the WAL database never stores the raw unsaved text.
            using (var conn = OpenDb(db))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT hex(encrypted_payload) FROM session_snapshots
                    WHERE checkpoint_id='cp1'
                    """;
                var encrypted = cmd.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(encrypted) ||
                    encrypted.Contains(Convert.ToHexString(
                        System.Text.Encoding.UTF8.GetBytes(plaintext)),
                        StringComparison.OrdinalIgnoreCase))
                    return false;
                cmd.CommandText = """
                    UPDATE session_snapshots SET encrypted_payload=x'01020304'
                    WHERE checkpoint_id='cp1'
                    """;
                cmd.ExecuteNonQuery();
            }
            using (var journal = new SqliteJournal(db))
            {
                try { journal.RestoreSessionCheckpoint("cp1", "project1"); return false; }
                catch (InvalidDataException) { }
                if (journal.CanRetry("remote1")) return false;
            }

            var brokenDb = Path.Combine(root, "damaged.sqlite3");
            var fake = RandomNumberGenerator.GetBytes(512);
            File.WriteAllBytes(brokenDb, fake);
            try { using var damaged = new SqliteJournal(brokenDb); return false; }
            catch (SqliteException) { }
            if (!File.ReadAllBytes(brokenDb).AsSpan().SequenceEqual(fake)) return false;

            // Opening a valid v1 database performs a transactional v2 upgrade.
            var legacy = Path.Combine(root, "legacy.sqlite3");
            using (var conn = OpenDb(legacy))
            {
                using var command = conn.CreateCommand();
                command.CommandText = """
                    CREATE TABLE operations (
                      id TEXT PRIMARY KEY, kind TEXT NOT NULL, state TEXT NOT NULL,
                      args_sha256 TEXT NOT NULL,idempotent INTEGER NOT NULL,
                      evidence_effect_absent INTEGER NOT NULL,updated_utc TEXT NOT NULL);
                    CREATE TABLE events (
                      sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                      operation_id TEXT NOT NULL,state TEXT NOT NULL,updated_utc TEXT NOT NULL);
                    CREATE TABLE checkpoints (
                      id TEXT PRIMARY KEY,workspace_id TEXT NOT NULL,
                      git_ref TEXT NOT NULL,manifest_sha256 TEXT NOT NULL,
                      created_utc TEXT NOT NULL);
                    PRAGMA user_version=1;
                    """;
                command.ExecuteNonQuery();
            }
            using (var migrated = new SqliteJournal(legacy))
            {
                if (ReadVersion(legacy) != 2) return false;
                migrated.StoreSessionCheckpoint("legacycp", "project1", "story1",
                    "AwaitingApproval", new Dictionary<string, string>
                    { ["README.md"] = "draft" }, Array.Empty<string>());
            }
            using (var migrated = new SqliteJournal(legacy))
                if (migrated.RestoreSessionCheckpoint("legacycp", "project1")
                    .UnsavedBuffers["README.md"] != "draft") return false;
            return true;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static SqliteConnection OpenDb(string path)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        conn.Open();
        return conn;
    }

    private static long ReadVersion(string path)
    {
        using var conn = OpenDb(path);
        using var command = conn.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
