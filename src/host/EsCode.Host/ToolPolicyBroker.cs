using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// E03 authorization core only. This class NEVER invokes an operating-system tool.
// Approvals are only minted by a trusted Host approval coordinator, never by an LLM or renderer.
internal enum BrokerMode { ReadOnly, Ask, Supervised }
internal enum ToolRisk { Read, Low, High, Critical }
internal sealed record BrokerIntent(string Actor, string Tool, string Resource, string ArgumentsDigest, ToolRisk Risk);
internal sealed record BrokerDecision(bool Allowed, string Reason);

internal sealed class ToolPolicyBroker
{
    private sealed record StoredGrant(BrokerIntent Intent, DateTimeOffset ExpiresUtc, string WorkspaceId);
    private readonly Dictionary<string, StoredGrant> _grants = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly BrokerMode _mode;
    private readonly string _workspaceId;
    private static readonly HashSet<string> ReadOnlyTools = new(StringComparer.Ordinal)
    {
        "workspace.read", "workspace.search"
    };
    private static readonly HashSet<string> SupervisedTools = new(StringComparer.Ordinal)
    {
        "workspace.patch", "process.build", "process.test", "git.worktree", "git.commit", "git.push", "git.merge"
    };

    internal ToolPolicyBroker(BrokerMode mode, string workspaceId, Func<DateTimeOffset>? utcNow = null)
    {
        if (!IsSafeIdentifier(workspaceId)) throw new ArgumentException("Invalid workspace identity", nameof(workspaceId));
        _mode = mode;
        _workspaceId = workspaceId;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    // This method MUST only be called in the trusted .NET Host path after human approval.
    // Never add an IPC endpoint that accepts Approved=true or a pre-minted grant from the renderer.
    internal string? GrantAfterTrustedHumanApproval(BrokerIntent intent, string workspaceId, TimeSpan lifetime)
    {
        if (!string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal) || !WellFormed(intent) ||
            lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(5) ||
            !ModeAllows(intent)) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (_gate)
        {
            var now = _utcNow();
            foreach (var expired in _grants.Where(kvp => kvp.Value.ExpiresUtc <= now).Select(kvp => kvp.Key).ToArray())
                _grants.Remove(expired);
            if (_grants.Count >= 128) return null; // fail closed under grant exhaustion
            _grants.Add(token, new StoredGrant(intent, now.Add(lifetime), workspaceId));
        }
        return token;
    }

    // Consume once, with authorization checked at the point of intended execution.
    internal BrokerDecision Consume(string? token, BrokerIntent requested, string workspaceId)
    {
        if (token is null || token.Length != 64 || !Regex.IsMatch(token, "^[A-F0-9]{64}$") ||
            !string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal) ||
            !WellFormed(requested) || !ModeAllows(requested))
            return new BrokerDecision(false, "Denied");
        lock (_gate)
        {
            if (!_grants.Remove(token, out var entry)) return new BrokerDecision(false, "Denied");
            if (entry.ExpiresUtc <= _utcNow() || entry.WorkspaceId != workspaceId ||
                !Matches(entry.Intent, requested)) return new BrokerDecision(false, "Denied");
            return new BrokerDecision(true, "Approved single use");
        }
    }

    internal void Revoke(string? token)
    {
        if (token is null) return;
        lock (_gate) _grants.Remove(token);
    }

    private bool ModeAllows(BrokerIntent i) =>
        i.Risk != ToolRisk.Critical &&
        (ReadOnlyTools.Contains(i.Tool) && i.Risk == ToolRisk.Read ||
         _mode == BrokerMode.Supervised && SupervisedTools.Contains(i.Tool) &&
            i.Risk is ToolRisk.Low or ToolRisk.High);

    private static bool IsSafeIdentifier(string? value) =>
        value is not null && value.Length is >= 1 and <= 100 &&
        Regex.IsMatch(value, "^[a-zA-Z0-9_-]+$");

    private static bool WellFormed(BrokerIntent i) =>
        IsSafeIdentifier(i.Actor) &&
        i.Tool is not null && i.Tool.Length <= 50 &&
        i.Resource is not null && i.Resource.Length is >= 1 and <= 512 &&
        (i.Resource.StartsWith("workspace/", StringComparison.Ordinal) ||
         i.Resource.StartsWith("project/", StringComparison.Ordinal)) &&
        !i.Resource.Contains('\\') && !i.Resource.Contains(':') && !i.Resource.Contains('\0') &&
        !i.Resource.Split('/').Any(x => x is "" or "." or "..") &&
        i.ArgumentsDigest is not null && Regex.IsMatch(i.ArgumentsDigest, "^sha256:[a-f0-9]{64}$");

    private static bool Matches(BrokerIntent a, BrokerIntent b) =>
        a.Risk == b.Risk &&
        Equal(a.Actor, b.Actor) && Equal(a.Tool, b.Tool) &&
        Equal(a.Resource, b.Resource) && Equal(a.ArgumentsDigest, b.ArgumentsDigest);

    private static bool Equal(string a, string b)
    {
        var first = Encoding.UTF8.GetBytes(a);
        var second = Encoding.UTF8.GetBytes(b);
        return first.Length == second.Length && CryptographicOperations.FixedTimeEquals(first, second);
    }
}

internal static class ToolPolicyBrokerSpike
{
    internal static bool Run()
    {
        var now = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
        var broker = new ToolPolicyBroker(BrokerMode.Supervised, "workspace-A", () => now);
        var intent = new BrokerIntent("agent-1", "workspace.patch", "workspace/src/main.cs",
            "sha256:" + new string('a', 64), ToolRisk.High);
        var token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        if (token is null || token.Length != 64) return false;
        if (broker.Consume(token, intent with { Actor = "agent-2" }, "workspace-A").Allowed) return false;
        if (broker.Consume(token, intent, "workspace-A").Allowed) return false; // fail-closed + consumed
        token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        if (!broker.Consume(token, intent, "workspace-A").Allowed) return false;
        if (broker.Consume(token, intent, "workspace-A").Allowed) return false;
        token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        if (broker.Consume(token, intent with { ArgumentsDigest = "sha256:" + new string('b', 64) }, "workspace-A").Allowed) return false;
        token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        if (broker.Consume(token, intent with { Resource = "workspace/secrets.txt" }, "workspace-A").Allowed) return false;
        token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        if (broker.Consume(token, intent with { Risk = ToolRisk.Read }, "workspace-A").Allowed) return false;
        token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        if (broker.Consume(token, intent, "workspace-B").Allowed) return false;
        token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        broker.Revoke(token);
        if (broker.Consume(token, intent, "workspace-A").Allowed) return false;
        token = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
        now = now.AddMinutes(2);
        if (broker.Consume(token, intent, "workspace-A").Allowed) return false;
        if (broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(6)) is not null) return false;
        if (broker.GrantAfterTrustedHumanApproval(intent with {Resource = "workspace/../secret"}, "workspace-A", TimeSpan.FromMinutes(1)) is not null) return false;
        if (broker.GrantAfterTrustedHumanApproval(intent with {Tool = "process.shell"}, "workspace-A", TimeSpan.FromMinutes(1)) is not null) return false;
        if (broker.GrantAfterTrustedHumanApproval(intent with {Risk = ToolRisk.Critical}, "workspace-A", TimeSpan.FromMinutes(1)) is not null) return false;
        var readOnly = new ToolPolicyBroker(BrokerMode.ReadOnly, "workspace-A", () => now);
        var readIntent = intent with {Tool = "workspace.read", Risk = ToolRisk.Read};
        var readToken = readOnly.GrantAfterTrustedHumanApproval(readIntent, "workspace-A", TimeSpan.FromMinutes(1));
        if (readToken is null || !readOnly.Consume(readToken, readIntent, "workspace-A").Allowed) return false;
        if (readOnly.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1)) is not null) return false;
        var ask = new ToolPolicyBroker(BrokerMode.Ask, "workspace-A", () => now);
        if (ask.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1)) is not null) return false;
        return true;
    }
}
