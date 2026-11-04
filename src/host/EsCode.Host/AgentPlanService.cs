using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// G6.1 Plan mode only: no file edits, terminal, Git or tool execution.
internal sealed record AgentPlanResult(string RunId, string Plan, JournalState State);

internal sealed class AgentPlanService
{
    private readonly SqliteJournal _journal;
    private readonly Func<string, CancellationToken, Task<string>> _planner;
    internal AgentPlanService(SqliteJournal journal, Func<string, CancellationToken, Task<string>> planner)
    {
        _journal = journal;
        _planner = planner;
    }

    internal async Task<AgentPlanResult> PlanAsync(string story, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(story) || story.Length > 4096)
            throw new ArgumentException("Story must be between 1 and 4096 characters");
        var runId = "plan-" + Guid.NewGuid().ToString("N");
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(story))).ToLowerInvariant();
        _journal.Prepare(runId, "agent-plan", digest, idempotent: true);
        _journal.MarkExecuting(runId);
        try
        {
            var plan = await _planner(
                "Create a short, testable software development plan. Do not claim to run commands, edit files or commit code. " +
                "Return steps only. User story: " + story, cancellationToken);
            if (string.IsNullOrWhiteSpace(plan) || plan.Length > 100_000)
                throw new InvalidDataException("Invalid planning output");
            _journal.MarkResult(runId, JournalState.Succeeded);
            return new AgentPlanResult(runId, plan, JournalState.Succeeded);
        }
        catch (OperationCanceledException)
        {
            // Uncertain provider response or cost: never auto-retry without reconciliation.
            _journal.MarkResult(runId, JournalState.OutcomeUnknown);
            throw;
        }
        catch
        {
            _journal.MarkResult(runId, JournalState.Failed);
            throw;
        }
    }
}

internal static class AgentPlanCommand
{
    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    internal static async Task<int> RunAsync(string[] args)
    {
        var baseUrl = Option(args, "--base-url");
        var model = Option(args, "--model");
        var story = await Console.In.ReadLineAsync();
        if (baseUrl is null || model is null || story is null) return 2;
        try
        {
            var location = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EsCode", "sessions", "agent.sqlite3");
            using var journal = new SqliteJournal(location);
            journal.RecoverInterrupted();
            var planner = new AgentPlanService(journal, (prompt, ct) =>
                ProviderChat.StreamAsync(baseUrl, model, prompt,
                    Environment.GetEnvironmentVariable("ESCODE_LLM_API_KEY"), cancellationToken: ct));
            var result = await planner.PlanAsync(story);
            Console.WriteLine(result.Plan);
            Console.Error.WriteLine("Plan recorded; no tools or file changes executed.");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or
            InvalidOperationException or Microsoft.Data.Sqlite.SqliteException or
            HttpRequestException or JsonException or TaskCanceledException)
        {
            Console.Error.WriteLine($"Agent planning failed: {ex.GetType().Name}");
            return 1;
        }
    }
}

internal static class AgentPlanSpike
{
    internal static async Task<bool> RunAsync()
    {
        var folder = Path.Combine(Path.GetTempPath(), "escode-agent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var journal = new SqliteJournal(Path.Combine(folder, "agent.sqlite3"));
            var service = new AgentPlanService(journal, (story, ct) =>
                Task.FromResult("1. Analyze the source\n2. Build tests"));
            var result = await service.PlanAsync("Add a user search endpoint");
            if (result.State != JournalState.Succeeded ||
                journal.Read(result.RunId)?.State != JournalState.Succeeded ||
                !result.Plan.Contains("Build tests", StringComparison.Ordinal)) return false;
            var failing = new AgentPlanService(journal,
                (story, ct) => throw new InvalidOperationException("Simulated model failure"));
            try { await failing.PlanAsync("Test failure"); return false; }
            catch (InvalidOperationException) { }
            var empty = new AgentPlanService(journal,
                (story, ct) => Task.FromResult(""));
            try { await empty.PlanAsync("Test empty plan"); return false; }
            catch (InvalidDataException) { }
            try { await service.PlanAsync(""); return false; }
            catch (ArgumentException) { }
            return true;
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }
}
