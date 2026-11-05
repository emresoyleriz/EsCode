using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

// G5 read-only local provider discovery. No model inference, tool calls,
// credentials, endpoint persistence or arbitrary outbound network access.
internal sealed record ModelCatalogResult(bool Found, string Status);

internal static class ModelCatalogProbe
{
    private const int MaxBodyBytes = 256 * 1024;
    private static readonly Regex ModelId = new(
        "^[A-Za-z0-9][A-Za-z0-9_./:-]{0,127}$", RegexOptions.CultureInvariant);

    internal static bool IsSafeRequest(string? baseUrl, string? model)
    {
        if (string.IsNullOrEmpty(baseUrl) || baseUrl.Length > 128 ||
            baseUrl.IndexOfAny(['%', '\\', '|', '\r', '\n']) >= 0 ||
            string.IsNullOrEmpty(model) || !ModelId.IsMatch(model) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || uri.UserInfo.Length > 0 ||
            uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            (uri.AbsolutePath != "/v1" && uri.AbsolutePath != "/v1/") ||
            !IPAddress.TryParse(uri.Host, out var ip) ||
            !IPAddress.IsLoopback(ip) || uri.Port is < 1 or > 65535)
            return false;
        return true;
    }

    internal static async Task<ModelCatalogResult> ProbeAsync(
        string baseUrl, string model, CancellationToken cancellationToken = default,
        HttpMessageHandler? mockHandler = null)
    {
        if (!IsSafeRequest(baseUrl, model))
            throw new ArgumentException("Invalid local model probe input");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        using var client = mockHandler is null
            ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            : new HttpClient(mockHandler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(5);
        var url = baseUrl.TrimEnd('/') + "/models";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
            return new ModelCatalogResult(false, "authorization-required");
        if (response.StatusCode != HttpStatusCode.OK)
            return new ModelCatalogResult(false, "provider-unavailable");
        if (response.Content.Headers.ContentLength is > MaxBodyBytes)
            return new ModelCatalogResult(false, "provider-unavailable");
        await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await input.ReadAsync(buffer.AsMemory(), deadline.Token);
            if (count == 0) break;
            if (output.Length + count > MaxBodyBytes)
                return new ModelCatalogResult(false, "provider-unavailable");
            output.Write(buffer, 0, count);
        }
        try
        {
            using var json = JsonDocument.Parse(output.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            if (!json.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > 512)
                return new ModelCatalogResult(false, "provider-unavailable");
            var found = data.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("id", out var value) &&
                value.ValueKind == JsonValueKind.String && value.GetString() == model);
            return new ModelCatalogResult(found, found ? "model-found" : "model-not-listed");
        }
        catch (JsonException)
        {
            return new ModelCatalogResult(false, "provider-unavailable");
        }
    }
}

internal static class ModelCatalogSpike
{
    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        internal string? Path;
        internal MockHandler(HttpStatusCode status, string body)
        {
            _status = status; _body = body;
        }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            });
        }
    }
    internal static async Task<bool> RunAsync()
    {
        const string url = "http://127.0.0.1:8000/v1";
        const string model = "qwen2.5-coder-7b";
        foreach (var invalid in new[] {
            "https://127.0.0.1/v1", "http://example.org/v1",
            "http://127.0.0.1:8000/admin", "http://127.0.0.1:8000/v1?key=secret",
            "http://127.0.0.1:8000/v1%2F", "http://localhost:8000/v1" })
            if (ModelCatalogProbe.IsSafeRequest(invalid, model)) return false;
        if (ModelCatalogProbe.IsSafeRequest(url, "bad|model")) return false;
        var okay = new MockHandler(HttpStatusCode.OK,
            """{"data":[{"id":"different"},{"id":"qwen2.5-coder-7b"}]}""");
        var result = await ModelCatalogProbe.ProbeAsync(url, model, mockHandler: okay);
        if (!result.Found || result.Status != "model-found" ||
            okay.Path != "/v1/models") return false;
        var absent = await ModelCatalogProbe.ProbeAsync(url, model,
            mockHandler: new MockHandler(HttpStatusCode.OK, """{"data":[{"id":"another"}]}"""));
        if (absent.Found || absent.Status != "model-not-listed") return false;
        var denied = await ModelCatalogProbe.ProbeAsync(url, model,
            mockHandler: new MockHandler(HttpStatusCode.Unauthorized, "secret: must never surface"));
        if (denied.Found || denied.Status != "authorization-required") return false;
        var oversized = await ModelCatalogProbe.ProbeAsync(url, model,
            mockHandler: new MockHandler(HttpStatusCode.OK, new string('x', 270_000)));
        if (oversized.Found || oversized.Status != "provider-unavailable") return false;
        var invalidJson = await ModelCatalogProbe.ProbeAsync(url, model,
            mockHandler: new MockHandler(HttpStatusCode.OK, "{broken"));
        return !invalidJson.Found && invalidJson.Status == "provider-unavailable";
    }
}
