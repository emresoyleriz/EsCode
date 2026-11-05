using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// G4.1: trusted-Host-only, single-use, exact-content approved patch.
// The renderer/LLM cannot mint grants and this is not an IPC tool endpoint.
internal sealed class ApprovedWorkspacePatch
{
    private readonly WorkspaceSessionRegistry _sessions;
    private readonly ToolPolicyBroker _broker;
    private readonly string _sessionId;

    internal ApprovedWorkspacePatch(WorkspaceSessionRegistry sessions,
        ToolPolicyBroker broker, string sessionId)
    {
        _sessions = sessions;
        _broker = broker;
        _sessionId = sessionId;
    }

    internal BrokerIntent Describe(string actor, string relativePath,
        string expectedSha256, string proposedText)
    {
        if (actor is null || relativePath is null || expectedSha256 is null ||
            proposedText is null || proposedText.Contains('\0') ||
            Encoding.UTF8.GetByteCount(proposedText) > 1_048_576)
            throw new ArgumentException("Invalid workspace patch input");

        // Bind the grant to the exact workspace, target path, expected source
        // digest and replacement content. Avoid ambiguous string concatenation.
        var payload = JsonSerializer.Serialize(new
        {
            workspace = _sessionId,
            relativePath,
            expectedSha256,
            proposedText
        });
        var digest = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return new BrokerIntent(actor, "workspace.patch",
            "workspace/" + _sessionId + "/" + relativePath, digest, ToolRisk.High);
    }

    internal WorkspaceTextSnapshot Apply(string? singleUseGrant, string actor,
        string relativePath, string expectedSha256, string proposedText)
    {
        var intent = Describe(actor, relativePath, expectedSha256, proposedText);
        // Reject BEFORE file or directory access. One-use grant is consumed on
        // mismatch, failure, or success to prohibit blind auto-retry.
        var permission = _broker.Consume(singleUseGrant, intent, _sessionId);
        if (!permission.Allowed)
            throw new UnauthorizedAccessException("Workspace patch requires fresh human approval");
        return _sessions.ReplaceAfterTrustedApproval(
            _sessionId, relativePath, expectedSha256, proposedText);
    }
}

internal static class ApprovedWorkspacePatchSpike
{
    private static bool Denied(Action action)
    {
        try { action(); return false; }
        catch (UnauthorizedAccessException) { return true; }
    }

    internal static bool Run()
    {
        var parent = Path.Combine(Path.GetTempPath(),
            "escode-approved-patch-" + Guid.NewGuid().ToString("N"));
        var a = Path.Combine(parent, "project-a");
        var b = Path.Combine(parent, "project-b");
        Directory.CreateDirectory(Path.Combine(a, "src"));
        Directory.CreateDirectory(Path.Combine(b, "src"));
        try
        {
            const string path = "src/program.cs";
            File.WriteAllText(Path.Combine(a, "src", "program.cs"), "class Original {}");
            File.WriteAllText(Path.Combine(b, "src", "program.cs"), "class OtherProject {}");
            File.WriteAllText(Path.Combine(a, ".env"), "TOKEN=must-not-write");
            var sessions = new WorkspaceSessionRegistry();
            sessions.AddTrustedRoot("A", a);
            sessions.AddTrustedRoot("B", b);
            var broker = new ToolPolicyBroker(BrokerMode.Supervised, "A");
            var approved = new ApprovedWorkspacePatch(sessions, broker, "A");
            var original = sessions.Read("A", path);
            var intent = approved.Describe("developer", path, original.Sha256,
                "class Approved {}");

            // Without an issued grant, real disk content must remain unchanged.
            if (!Denied(() => approved.Apply(null, "developer", path,
                    original.Sha256, "class Approved {}"))) return false;
            if (sessions.Read("A", path).Content != original.Content) return false;

            var token = broker.GrantAfterTrustedHumanApproval(intent, "A",
                TimeSpan.FromMinutes(1));
            if (token is null) return false;

            // A malicious replacement consumes the token without touching disk.
            if (!Denied(() => approved.Apply(token, "developer", path,
                    original.Sha256, "class Tampered {}"))) return false;
            if (!Denied(() => approved.Apply(token, "developer", path,
                    original.Sha256, "class Approved {}"))) return false;
            if (sessions.Read("A", path).Content != original.Content) return false;

            token = broker.GrantAfterTrustedHumanApproval(intent, "A",
                TimeSpan.FromMinutes(1));
            var written = approved.Apply(token, "developer", path,
                original.Sha256, "class Approved {}");
            if (written.Content != "class Approved {}" ||
                File.ReadAllText(Path.Combine(a, "src", "program.cs")) != "class Approved {}")
                return false;
            if (!Denied(() => approved.Apply(token, "developer", path,
                    original.Sha256, "class Approved {}"))) return false;
            if (sessions.Read("B", path).Content != "class OtherProject {}") return false;

            // An external edit between preview and apply must prevent overwrite.
            var stale = sessions.Read("A", path);
            var staleIntent = approved.Describe("developer", path, stale.Sha256,
                "class Later {}");
            token = broker.GrantAfterTrustedHumanApproval(staleIntent, "A",
                TimeSpan.FromMinutes(1));
            File.WriteAllText(Path.Combine(a, "src", "program.cs"), "class External {}");
            try
            {
                approved.Apply(token, "developer", path, stale.Sha256, "class Later {}");
                return false;
            }
            catch (InvalidOperationException) { }
            if (sessions.Read("A", path).Content != "class External {}") return false;

            // A valid grant for A cannot approve changes under project B.
            var other = sessions.Read("B", path);
            var bIntent = approved.Describe("developer", path, other.Sha256,
                "class WrongRoot {}");
            var bToken = broker.GrantAfterTrustedHumanApproval(bIntent, "A",
                TimeSpan.FromMinutes(1));
            if (!Denied(() => approved.Apply(bToken, "different-actor", path,
                    other.Sha256, "class WrongRoot {}"))) return false;
            if (sessions.Read("B", path).Content != "class OtherProject {}") return false;

            // A separate read-only broker cannot grant write permissions.
            var ro = new ToolPolicyBroker(BrokerMode.ReadOnly, "A");
            if (ro.GrantAfterTrustedHumanApproval(intent, "A",
                    TimeSpan.FromMinutes(1)) is not null) return false;
            return true;
        }
        finally
        {
            try { Directory.Delete(parent, recursive: true); }
            catch (IOException) { /* test cleanup best effort */ }
        }
    }
}
