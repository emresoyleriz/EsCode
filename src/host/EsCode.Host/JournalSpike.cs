internal static class JournalSpike
{
    private static readonly string Digest = "sha256:" + new string('a', 64);
    internal static bool Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "escode-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "workspace.sqlite3");
        try
        {
            using (var store = new SqliteJournal(file))
            {
                store.Prepare("task1", "workspace-patch", Digest, true);
                if (store.Read("task1")?.State != JournalState.Prepared) return false;
                store.MarkExecuting("task1");
                store.Prepare("task2", "git-commit", Digest, false);
                store.MarkExecuting("task2");
                try { store.MarkExecuting("task1"); return false; }
                catch (InvalidOperationException) { }
                try { store.Prepare("task1", "workspace-patch", Digest, true); return false; }
                catch (Microsoft.Data.Sqlite.SqliteException) { }
                try { store.Prepare("../escape", "bad", Digest, true); return false; }
                catch (ArgumentException) { }
                store.Checkpoint("checkpoint1", "workspace1", "feature/demo", Digest);
            }
            using (var store = new SqliteJournal(file))
            {
                if (store.RecoverInterrupted() != 2) return false;
                if (store.RecoverInterrupted() != 0) return false;
                if (store.CanRetry("task1") || store.CanRetry("task2")) return false;
                if (store.ReconcileAbsentEffect("task1", "bad")) return false;
                if (store.ReconcileAbsentEffect("task2", Digest)) return false;
                if (store.ReconcileAbsentEffect("task1", Digest)) return false;
                if (store.CanRetry("task1") || store.CanRetry("task2")) return false;
                if (store.Read("task1")?.State != JournalState.OutcomeUnknown) return false;
                if (store.CheckpointCount() != 1) return false;
                store.Backup(Path.Combine(dir, "backup.sqlite3"));
                try { store.Backup(Path.Combine(dir, "backup.sqlite3")); return false; }
                catch (IOException) { }
            }
            using (var restored = new SqliteJournal(Path.Combine(dir, "backup.sqlite3")))
            {
                if (restored.CheckpointCount() != 1) return false;
                if (restored.Read("task1")?.State != JournalState.OutcomeUnknown || restored.CanRetry("task1")) return false;
            }
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); }
            catch (IOException) { /* Windows file cleanup is best effort; do not hide test results. */ }
        }
    }
}
