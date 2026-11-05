using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Metadata-only policy: never starts a plugin, MCP server, shell or browser.
internal sealed record PluginManifestDecision(bool ValidMetadata, bool ExecutionAllowed);
internal static class PluginManifestPolicy
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    { "PluginId", "Publisher", "Version", "Permissions", "TrustLevel",
      "Compatibility", "ContractVersion", "Signature" };
    private static readonly HashSet<string> ReadOnly = new(StringComparer.Ordinal)
    { "workspace.read", "workspace.search" };

    internal static PluginManifestDecision Validate(string? text)
    {
        var deny = new PluginManifestDecision(false, false);
        if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > 16_384)
            return deny;
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return deny;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in root.EnumerateObject())
            {
                if (!Allowed.Contains(field.Name) || !seen.Add(field.Name)) return deny;
            }
            if (!Match(root, "PluginId", "^[a-z][a-z0-9._-]{0,63}$") ||
                !Match(root, "Publisher", "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$") ||
                !Match(root, "Version", "^[0-9]{1,5}\\.[0-9]{1,5}\\.[0-9]{1,5}$") ||
                !root.TryGetProperty("ContractVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var contract) || contract != 1 ||
                !root.TryGetProperty("TrustLevel", out var trust) ||
                trust.ValueKind != JsonValueKind.String || trust.GetString() != "untrusted" ||
                !root.TryGetProperty("Compatibility", out var compatibility) ||
                compatibility.ValueKind != JsonValueKind.Object ||
                compatibility.EnumerateObject().Any() ||
                root.TryGetProperty("Signature", out _)) return deny;
            if (!root.TryGetProperty("Permissions", out var permissions) ||
                permissions.ValueKind != JsonValueKind.Array ||
                permissions.GetArrayLength() > 4) return deny;
            var seenPermissions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in permissions.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.String || p.GetString() is not string value ||
                    !ReadOnly.Contains(value) || !seenPermissions.Add(value)) return deny;
            }
            return new PluginManifestDecision(true, false);
        }
        catch (JsonException) { return deny; }
    }

    private static bool Match(JsonElement root, string key, string regex) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String &&
        Regex.IsMatch(value.GetString() ?? "", regex, RegexOptions.CultureInvariant);
}

internal static class PluginManifestSpike
{
    internal static bool Run()
    {
        const string good = """
        {"PluginId":"safe.search","Publisher":"EsCode","Version":"1.0.0",
         "Permissions":["workspace.read","workspace.search"],"TrustLevel":"untrusted",
         "Compatibility":{},"ContractVersion":1}
        """;
        var accepted = PluginManifestPolicy.Validate(good);
        if (!accepted.ValidMetadata || accepted.ExecutionAllowed) return false;
        var invalid = new[]
        {
            good.Replace("workspace.read", "process.shell", StringComparison.Ordinal),
            good.Replace("workspace.read", "network.fetch", StringComparison.Ordinal),
            good.Replace("\"untrusted\"", "\"trusted\"", StringComparison.Ordinal),
            good.Replace("\"ContractVersion\":1", "\"ContractVersion\":2", StringComparison.Ordinal),
            good.Replace("\"Version\":\"1.0.0\"", "\"Version\":\"bad\"", StringComparison.Ordinal),
            good.Replace("\"Compatibility\":{}", "\"Compatibility\":{\"cmd\":\"x\"}", StringComparison.Ordinal),
            good.Replace("\"Permissions\":[\"workspace.read\",\"workspace.search\"]",
                "\"Permissions\":[\"workspace.read\",\"workspace.read\"]", StringComparison.Ordinal),
            good.Replace("\"ContractVersion\":1", "\"Signature\":\"fake\",\"ContractVersion\":1",
                StringComparison.Ordinal),
            good.Replace("\"ContractVersion\":1", "\"Unknown\":true,\"ContractVersion\":1",
                StringComparison.Ordinal),
            good.Replace("\"ContractVersion\":1", "\"PluginId\":\"duplicate\",\"ContractVersion\":1",
                StringComparison.Ordinal),
            good + new string('x', 20_000),
            "{broken", "[]"
        };
        return invalid.All(x =>
        {
            var decision = PluginManifestPolicy.Validate(x);
            return !decision.ValidMetadata && !decision.ExecutionAllowed;
        });
    }
}
