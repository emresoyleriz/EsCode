using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

internal static class ProviderDiagnostics
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var baseUrl = GetOption(args, "--base-url");
        var model = GetOption(args, "--model");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            Console.Error.WriteLine("Usage: --provider-probe --base-url https://host/v1 --model MODEL [--api-key-env ENV_NAME]");
            return 2;
        }

        // Explicit diagnostic command only: never triggered by renderer or model output.
        var keyVariable = GetOption(args, "--api-key-env") ?? "ESCODE_LLM_API_KEY";
        if (!System.Text.RegularExpressions.Regex.IsMatch(keyVariable, @"^[A-Za-z_][A-Za-z0-9_]{0,127}$"))
        {
            Console.Error.WriteLine("Invalid API key environment variable name.");
            return 2;
        }
        var key = Environment.GetEnvironmentVariable(keyVariable);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        if (!string.IsNullOrEmpty(key))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var endpoint = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/models");
        try
        {
            using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Console.Error.WriteLine($"Provider returned HTTP {(int)response.StatusCode}. No credentials or response body logged.");
                return 1;
            }
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 16 });
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                Console.Error.WriteLine("Invalid models response: expected data array.");
                return 1;
            }
            var found = data.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String &&
                id.GetString() == model);
            Console.WriteLine(found ? "PROVIDER_MODEL_FOUND" : "PROVIDER_CONNECTED_MODEL_NOT_LISTED");
            Console.WriteLine("Model listing is not proof of chat, streaming or tool-calling support.");
            return found ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or System.IO.IOException)
        {
            Console.Error.WriteLine($"Provider probe failed: {ex.GetType().Name}. Details suppressed to avoid credential or URL leakage.");
            return 1;
        }
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
