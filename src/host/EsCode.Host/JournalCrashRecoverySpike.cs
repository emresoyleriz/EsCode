using System.Diagnostics;

// G3 deterministic kill-point integration test. Executes only this trusted Host
// assembly and never invokes workspace, Git, shell, AI or external effects.
internal static class JournalCrashRecoverySpike
{
    private const string ReadyMarker = "JOURNAL_KILLPOINT_READY";

    internal static async Task<int> RunChildAsync(string[] args)
    {
        var fileAt = Array.IndexOf(args, "--journal-file");
        var pointAt = Array.IndexOf(args, "--killpoint");
        if (fileAt < 0 || pointAt < 0 || fileAt + 1 >= args.Length ||
            pointAt + 1 >= args.Length) return 2;
        var file = args[fileAt + 1];
        var point = args[pointAt + 1];
        if (point is not ("prepared" or "executing") || !Path.IsPathFullyQualified(file) ||
            !(Path.GetFileName(Path.GetDirectoryName(file) ?? "") ?? "").StartsWith(
                "escode-journal-crash-", StringComparison.Ordinal))
            return 2;
        using (var journal = new SqliteJournal(file))
        {
            journal.Prepare("op-kill", "test-only", "sha256:" + new string('a', 64), idempotent: true);
            if (point == "executing") journal.MarkExecuting("op-kill");
            Console.WriteLine(ReadyMarker);
            await Console.Out.FlushAsync();
            // The parent intentionally kills the process. Never perform effects.
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        return 0;
    }

    internal static async Task<bool> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "escode-journal-crash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            if (!await CheckPointAsync(root, "prepared", JournalState.Prepared, 0)) return false;
            if (!await CheckPointAsync(root, "executing", JournalState.OutcomeUnknown, 1)) return false;
            return true;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { /* Retain test result if Windows transiently holds a file. */ }
            catch (UnauthorizedAccessException) { /* CI cleanup best effort. */ }
        }
    }

    private static async Task<bool> CheckPointAsync(string root, string point,
        JournalState expected, int recovered)
    {
        var file = Path.Combine(root, point + ".sqlite3");
        var assembly = typeof(JournalCrashRecoverySpike).Assembly.Location;
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("--journal-kill-child");
        start.ArgumentList.Add("--journal-file");
        start.ArgumentList.Add(file);
        start.ArgumentList.Add("--killpoint");
        start.ArgumentList.Add(point);
        using var child = Process.Start(start);
        if (child is null) return false;
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            string? marker;
            try { marker = await child.StandardOutput.ReadLineAsync(limit.Token); }
            catch (OperationCanceledException) { return false; }
            if (marker != ReadyMarker) return false;
            // This simulates an ungraceful exit after a durable transaction.
            // No finally/dispose is run in the child prior to its termination.
            child.Kill(entireProcessTree: true);
            try { await child.WaitForExitAsync(limit.Token); }
            catch (OperationCanceledException) { return false; }
            using (var journal = new SqliteJournal(file))
            {
                if (journal.Read("op-kill")?.State != (point == "prepared" ?
                    JournalState.Prepared : JournalState.Executing)) return false;
                if (journal.CanRetry("op-kill")) return false;
                if (journal.RecoverInterrupted() != recovered) return false;
                if (journal.RecoverInterrupted() != 0) return false;
                if (journal.Read("op-kill")?.State != expected) return false;
                if (journal.CanRetry("op-kill")) return false;
            }
            // Recovery persists independently of the process that performed it.
            using var reopened = new SqliteJournal(file);
            return reopened.Read("op-kill")?.State == expected &&
                   reopened.RecoverInterrupted() == 0 &&
                   !reopened.CanRetry("op-kill");
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
        }
    }
}
