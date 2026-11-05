using System.Text.RegularExpressions;

// G4.1: trusted-Host-only multi-root registry. Read-only; never grants renderer
// paths, file write privileges, or untrusted agent access.
internal sealed class WorkspaceSessionRegistry
{
    private const int MaximumRoots = 8;
    private readonly Dictionary<string, WorkspaceReader> _readers =
        new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private static readonly Regex SafeSessionId =
        new("^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal void AddTrustedRoot(string sessionId, string absoluteRoot)
    {
        if (sessionId is null || !SafeSessionId.IsMatch(sessionId))
            throw new ArgumentException("Invalid workspace session identifier");
        // WorkspaceReader validates absolute roots, network shares, and reparse
        // ancestors before a session is registered.
        var reader = new WorkspaceReader(absoluteRoot);
        lock (_gate)
        {
            if (_readers.Count >= MaximumRoots)
                throw new InvalidOperationException("Workspace count limit reached");
            if (!_readers.TryAdd(sessionId, reader))
                throw new InvalidOperationException("Duplicate workspace identifier");
        }
    }

    internal bool Close(string sessionId)
    {
        if (sessionId is null || !SafeSessionId.IsMatch(sessionId))
            throw new ArgumentException("Invalid workspace session identifier");
        lock (_gate) return _readers.Remove(sessionId);
    }

    internal WorkspaceTextSnapshot Read(string sessionId, string relativePath) =>
        GetReader(sessionId).ReadText(relativePath);

    // Only the trusted Host's approval coordinator calls this after consuming
    // a scoped, single-use Tool Broker grant. Never bind it to generic IPC.
    internal WorkspaceTextSnapshot ReplaceAfterTrustedApproval(string sessionId,
        string relativePath, string expectedSha256, string text) =>
        GetReader(sessionId).ReplaceText(relativePath, expectedSha256, text);

    internal IReadOnlyList<string> List(string sessionId, int maxEntries = 128) =>
        GetReader(sessionId).ListTextCandidates(maxEntries);

    // A trusted Host must re-read the file at the instant of an intended edit,
    // even if a FileSystemWatcher event was never delivered.
    internal bool IsCurrent(string sessionId, string relativePath, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 71 ||
            !expectedSha256.StartsWith("sha256:", StringComparison.Ordinal) ||
            expectedSha256.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") >= 0)
            throw new ArgumentException("Invalid revision hash");
        var live = Read(sessionId, relativePath);
        return string.Equals(live.Sha256, expectedSha256, StringComparison.Ordinal);
    }

    private WorkspaceReader GetReader(string sessionId)
    {
        if (sessionId is null || !SafeSessionId.IsMatch(sessionId))
            throw new ArgumentException("Invalid workspace session identifier");
        lock (_gate)
        {
            if (!_readers.TryGetValue(sessionId, out var reader))
                throw new KeyNotFoundException("Unknown workspace session");
            return reader;
        }
    }
}

internal static class WorkspaceSessionsSpike
{
    internal static bool Run()
    {
        var parent = Path.Combine(Path.GetTempPath(), "escode-roots-" + Guid.NewGuid().ToString("N"));
        var first = Path.Combine(parent, "project-a");
        var second = Path.Combine(parent, "project-b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        Directory.CreateDirectory(Path.Combine(first, "src"));
        Directory.CreateDirectory(Path.Combine(second, "src"));
        try
        {
            File.WriteAllText(Path.Combine(first, "src", "shared.cs"), "class FromA {}");
            File.WriteAllText(Path.Combine(second, "src", "shared.cs"), "class FromB {}");
            File.WriteAllText(Path.Combine(first, ".env"), "SECRET_A");
            var registry = new WorkspaceSessionRegistry();
            registry.AddTrustedRoot("A", first);
            registry.AddTrustedRoot("B", second);
            var a = registry.Read("A", "src/shared.cs");
            var b = registry.Read("B", "src/shared.cs");
            if (a.Content != "class FromA {}" || b.Content != "class FromB {}" ||
                a.Sha256 == b.Sha256 || !registry.IsCurrent("A", "src/shared.cs", a.Sha256))
                return false;
            File.WriteAllText(Path.Combine(first, "src", "shared.cs"), "class ModifiedExternally {}");
            if (registry.IsCurrent("A", "src/shared.cs", a.Sha256)) return false;
            if (!registry.IsCurrent("B", "src/shared.cs", b.Sha256)) return false;

            try { registry.Read("A", "../project-b/src/shared.cs"); return false; }
            catch (UnauthorizedAccessException) { }
            try { registry.Read("A", ".env"); return false; }
            catch (UnauthorizedAccessException) { }
            try { registry.Read("unknown", "src/shared.cs"); return false; }
            catch (KeyNotFoundException) { }
            try { registry.IsCurrent("A", "src/shared.cs", "sha256:wrong"); return false; }
            catch (ArgumentException) { }
            try { registry.AddTrustedRoot("A", second); return false; }
            catch (InvalidOperationException) { }
            try { registry.AddTrustedRoot("../bad", second); return false; }
            catch (ArgumentException) { }
            if (!registry.Close("A")) return false;
            try { registry.Read("A", "src/shared.cs"); return false; }
            catch (KeyNotFoundException) { }
            if (registry.Close("A")) return false;
            registry.AddTrustedRoot("A", first);
            if (registry.Read("A", "src/shared.cs").Content != "class ModifiedExternally {}")
                return false;
            return true;
        }
        finally
        {
            try { Directory.Delete(parent, recursive: true); }
            catch (IOException) { }
        }
    }
}
