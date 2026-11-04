using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// G5 CLI-only OpenAI-compatible streaming; no renderer/agent/tools/secret persistence.
internal static class ProviderChat
{
    internal static Uri Endpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Invalid provider endpoint");
        if (uri.Scheme == "http" &&
            !(uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip)))
            throw new ArgumentException("Plain HTTP providers must use loopback");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/chat/completions");
    }

    internal static async Task<string> StreamAsync(
        string baseUrl, string model, string prompt, string? apiKey,
        Action<string>? onChunk = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model) || !Regex.IsMatch(model, "^[a-zA-Z0-9][a-zA-Z0-9_./:-]{0,127}$"))
            throw new ArgumentException("Invalid model ID");
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 8192)
            throw new ArgumentException("Prompt must contain 1–8192 characters");
        var endpoint = Endpoint(baseUrl);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            stream = true
        }), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Provider HTTP {(int)response.StatusCode}");
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
            throw new InvalidDataException("Provider did not return an SSE stream");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        var result = new StringBuilder();
        var eventCount = 0;
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) throw new InvalidDataException("Provider stream ended without [DONE]");
            if (line.Length > 65_536) throw new InvalidDataException("Provider SSE event too large");
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var body = line[5..].TrimStart();
            if (body == "[DONE]") return result.ToString();
            if (++eventCount > 2048) throw new InvalidDataException("Provider event budget exceeded");
            using var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
            if (!json.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                throw new InvalidDataException("Malformed chat stream");
            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
            {
                if (delta.TryGetProperty("tool_calls", out _))
                    throw new InvalidOperationException("Tool calling is not enabled");
                if (delta.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    var chunk = content.GetString() ?? "";
                    if (result.Length + chunk.Length > 100_000)
                        throw new InvalidDataException("Output budget exceeded");
                    result.Append(chunk);
                    onChunk?.Invoke(chunk);
                }
            }
        }
    }
}

internal static class ProviderChatCommand
{
    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
    internal static async Task<int> RunAsync(string[] args)
    {
        var baseUrl = Option(args, "--base-url");
        var model = Option(args, "--model");
        if (baseUrl is null || model is null)
        {
            Console.Error.WriteLine("Usage: --chat --base-url URL/v1 --model MODEL (prompt supplied on stdin)");
            return 2;
        }
        var prompt = await Console.In.ReadLineAsync();
        if (prompt is null) return 2;
        try
        {
            await ProviderChat.StreamAsync(baseUrl, model, prompt,
                Environment.GetEnvironmentVariable("ESCODE_LLM_API_KEY"),
                chunk => Console.Write(chunk));
            Console.WriteLine();
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or
            InvalidOperationException or ArgumentException or TaskCanceledException or JsonException)
        {
            // No provider body, prompt, URL, API key or raw exception message in diagnostics.
            Console.Error.WriteLine($"Chat request failed: {ex.GetType().Name}");
            return 1;
        }
    }
}
