using Microsoft.Data.Sqlite;

// Additional negative tests for legacy digest-based unsafe reconciliation and
// unsupported schema versions. Never executes an external side effect.
internal static class JournalSafetySpike
{
    internal static bool Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "escode-journal-safety-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "state.sqlite3");
            const string digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            using (var journal = new SqliteJournal(file))
            {
                journal.Prepare("operation1", "external-operation", digest, idempotent: true);
                journal.MarkExecuting("operation1");
            }
            using (var journal = new SqliteJournal(file))
            {
                if (journal.RecoverInterrupted() != 1) return false;
                if (journal.ReconcileAbsentEffect("operation1", digest)) return false;
                if (journal.ReconcileAbsentEffect("operation1", "invalid")) return false;
                if (journal.CanRetry("operation1")) return false;
                if (journal.Read("operation1")?.State != JournalState.OutcomeUnknown) return false;
            }

            var futureDb = Path.Combine(root, "future.sqlite3");
            using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = futureDb,
                Pooling = false,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version=999;";
                cmd.ExecuteNonQuery();
            }
            try { using var unknown = new SqliteJournal(futureDb); return false; }
            catch (InvalidOperationException) { }
            using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = futureDb, Pooling = false, Mode = SqliteOpenMode.ReadWrite
            }.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version;";
                if (Convert.ToInt64(cmd.ExecuteScalar()) != 999L) return false;
            }
            return true;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }
    }
}
