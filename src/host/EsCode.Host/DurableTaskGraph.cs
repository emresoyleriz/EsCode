using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// G6.1 trusted-Host-only durable dependency-ordered task graph.
// No shell, file, Git, LLM, terminal, web or Tool Broker calls are invoked.
internal sealed record PlannedTask(string Id, string Kind, IReadOnlyList<string> DependsOn);
internal enum TaskGraphState { Pending, Running, Succeeded, Failed, OutcomeUnknown, Blocked }
internal sealed class DurableTaskGraph
{
    private static readonly Regex SafeId = new(
        "^[a-z][a-z0-9_-]{0,35}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    { "analyze", "plan", "review", "verify" };

    private readonly object _gate = new();
    private readonly SqliteJournal _journal;
    private readonly Dictionary<string, PlannedTask> _tasks;
    private readonly string _runId;
    private bool _paused;

    internal DurableTaskGraph(SqliteJournal journal, string runId, IEnumerable<PlannedTask> steps)
    {
        if (runId is null || !SafeId.IsMatch(runId))
            throw new ArgumentException("Invalid graph run identity");
        var input = steps?.ToArray() ?? throw new ArgumentException("Missing plan");
        if (input.Length is < 1 or > 64)
            throw new ArgumentException("Invalid task count");
        _tasks = new Dictionary<string, PlannedTask>(StringComparer.Ordinal);
        foreach (var task in input)
        {
            if (task is null || task.Id is null || !SafeId.IsMatch(task.Id) ||
                task.Kind is null || !AllowedKinds.Contains(task.Kind) ||
                task.DependsOn is null || task.DependsOn.Count > 8 ||
                task.DependsOn.Any(x => x is null || !SafeId.IsMatch(x)) ||
                task.DependsOn.Distinct(StringComparer.Ordinal).Count() != task.DependsOn.Count ||
                !_tasks.TryAdd(task.Id, task))
                throw new ArgumentException("Duplicate, malformed or unsafe task");
        }
        foreach (var task in input)
            foreach (var dep in task.DependsOn)
                if (task.Id == dep || !_tasks.ContainsKey(dep))
                    throw new ArgumentException("Unknown or self task dependency");

        // Reject cycles before inserting any operations in SQLite.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!active.Add(id)) throw new ArgumentException("Cyclic task graph");
            foreach (var prerequisite in _tasks[id].DependsOn) Visit(prerequisite);
            active.Remove(id);
            visited.Add(id);
        }
        foreach (var id in _tasks.Keys) Visit(id);

        _journal = journal;
        _runId = runId;
        var canonical = input.OrderBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => new
            {
                x.Id,
                x.Kind,
                DependsOn = x.DependsOn.OrderBy(d => d, StringComparer.Ordinal).ToArray()
            }).ToArray();
        var digest = Sha(JsonSerializer.Serialize(canonical));
        var graphKey = "graph-" + runId;
        var existingDigest = _journal.ReadDigest(graphKey);
        if (existingDigest is null)
            _journal.Prepare(graphKey, "task-graph", digest, idempotent: true);
        else if (!string.Equals(existingDigest, digest, StringComparison.Ordinal))
            throw new InvalidOperationException("Graph manifest changed after creation");

        // A crash between creating the manifest and seeding all nodes can be
        // safely resumed only with the SAME immutable graph manifest.
        foreach (var task in input)
        {
            var id = Key(task.Id);
            if (_journal.Read(id) is null)
                _journal.Prepare(id, task.Kind, digest, idempotent: true);
            else if (_journal.ReadDigest(id) != digest)
                throw new InvalidOperationException("Graph task digest mismatch");
        }
    }

    private string Key(string id) => "task-" + _runId + "-" + id;
    private static string Sha(string value) =>
        "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private TaskGraphState StateCore(string id, HashSet<string> visiting)
    {
        if (!_tasks.TryGetValue(id, out var task))
            throw new KeyNotFoundException("Unknown task ID");
        var persisted = _journal.Read(Key(id))?.State ??
            throw new InvalidDataException("Missing task journal row");
        if (persisted == JournalState.Succeeded) return TaskGraphState.Succeeded;
        if (persisted == JournalState.Failed) return TaskGraphState.Failed;
        if (persisted is JournalState.OutcomeUnknown or JournalState.Reconciled)
            return TaskGraphState.OutcomeUnknown;
        if (persisted == JournalState.Executing) return TaskGraphState.Running;
        if (!visiting.Add(id)) throw new InvalidDataException("Graph unexpectedly cyclic");
        try
        {
            if (task.DependsOn.Any(dep =>
                StateCore(dep, visiting) is TaskGraphState.Failed or
                    TaskGraphState.OutcomeUnknown or TaskGraphState.Blocked))
                return TaskGraphState.Blocked;
        }
        finally { visiting.Remove(id); }
        return TaskGraphState.Pending;
    }

    internal TaskGraphState State(string id)
    {
        lock (_gate) return StateCore(id, new HashSet<string>(StringComparer.Ordinal));
    }

    internal IReadOnlyList<string> Ready()
    {
        lock (_gate)
        {
            if (_paused) return Array.Empty<string>();
            return _tasks.Values
                .Where(t => StateCore(t.Id, new HashSet<string>(StringComparer.Ordinal)) ==
                        TaskGraphState.Pending &&
                    t.DependsOn.All(dep => StateCore(dep,
                        new HashSet<string>(StringComparer.Ordinal)) == TaskGraphState.Succeeded))
                .Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
    }

    internal void Pause()
    {
        lock (_gate) _paused = true;
    }

    internal void Resume()
    {
        lock (_gate) _paused = false;
    }

    internal void Start(string id)
    {
        lock (_gate)
        {
            if (_paused || !Ready().Contains(id, StringComparer.Ordinal))
                throw new InvalidOperationException("Task cannot start: not ready or paused");
            _journal.MarkExecuting(Key(id));
        }
    }

    internal void Finish(string id, TaskGraphState result)
    {
        if (result is not (TaskGraphState.Succeeded or TaskGraphState.Failed or
            TaskGraphState.OutcomeUnknown))
            throw new ArgumentException("Invalid terminal state");
        lock (_gate)
        {
            if (StateCore(id, new HashSet<string>(StringComparer.Ordinal)) !=
                TaskGraphState.Running)
                throw new InvalidOperationException("Task was not running");
            _journal.MarkResult(Key(id), result switch
            {
                TaskGraphState.Succeeded => JournalState.Succeeded,
                TaskGraphState.Failed => JournalState.Failed,
                _ => JournalState.OutcomeUnknown
            });
        }
    }
}

internal static class DurableTaskGraphSpike
{
    internal static bool Run()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "escode-task-dag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var tasks = new[]
            {
                new PlannedTask("analyze", "analyze", Array.Empty<string>()),
                new PlannedTask("plan", "plan", new[] { "analyze" }),
                new PlannedTask("review", "review", new[] { "plan" }),
                new PlannedTask("verify", "verify", new[] { "review" })
            };
            var db = Path.Combine(dir, "agent.sqlite3");
            using (var journal = new SqliteJournal(db))
            {
                var graph = new DurableTaskGraph(journal, "story1", tasks);
                if (!graph.Ready().SequenceEqual(new[] { "analyze" })) return false;
                try { graph.Start("verify"); return false; } catch (InvalidOperationException) { }
                graph.Pause();
                if (graph.Ready().Count != 0) return false;
                try { graph.Start("analyze"); return false; } catch (InvalidOperationException) { }
                graph.Resume();
                graph.Start("analyze");
                graph.Finish("analyze", TaskGraphState.Succeeded);
                graph.Start("plan");
                // Simulate process crash while plan is running.
            }
            using (var journal = new SqliteJournal(db))
            {
                if (journal.RecoverInterrupted() != 1) return false;
                if (journal.RecoverInterrupted() != 0) return false;
                var restored = new DurableTaskGraph(journal, "story1", tasks);
                if (restored.State("analyze") != TaskGraphState.Succeeded ||
                    restored.State("plan") != TaskGraphState.OutcomeUnknown ||
                    restored.State("review") != TaskGraphState.Blocked ||
                    restored.State("verify") != TaskGraphState.Blocked ||
                    restored.Ready().Count != 0) return false;
                try { restored.Start("review"); return false; }
                catch (InvalidOperationException) { }
                try
                {
                    _ = new DurableTaskGraph(journal, "story1", tasks[..^1]);
                    return false;
                }
                catch (InvalidOperationException) { }

                // Another run in the same journal has independent state.
                var separate = new DurableTaskGraph(journal, "story2", tasks);
                if (!separate.Ready().SequenceEqual(new[] { "analyze" })) return false;
                separate.Start("analyze");
                separate.Finish("analyze", TaskGraphState.Failed);
                if (separate.State("plan") != TaskGraphState.Blocked ||
                    separate.State("verify") != TaskGraphState.Blocked) return false;
            }
            var invalid = new[]
            {
                new[] {
                    new PlannedTask("a", "analyze", new[] { "b" }),
                    new PlannedTask("b", "review", new[] { "a" })
                },
                new[] { new PlannedTask("a", "process.shell", Array.Empty<string>()) },
                new[] { new PlannedTask("a", "analyze", new[] { "missing" }) },
                new[] {
                    new PlannedTask("a", "analyze", Array.Empty<string>()),
                    new PlannedTask("a", "review", Array.Empty<string>())
                }
            };
            using (var journal = new SqliteJournal(db))
            {
                foreach (var tasksBad in invalid)
                {
                    try { _ = new DurableTaskGraph(journal, "invalid", tasksBad); return false; }
                    catch (ArgumentException) { }
                }
            }
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
        }
    }
}
