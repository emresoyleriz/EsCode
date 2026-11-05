using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

internal static class ProviderChatSpike
{
    private static async Task<(string Output, string Request)> MockAsync(int status, string contentType, string data,
        Func<string, Task<string>> client)
    {
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var serverTask = Task.Run(async () =>
        {
            using var socket = await server.AcceptTcpClientAsync(limit.Token);
            await using var stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(limit.Token) ?? "";
            int length = 0;
            string? header;
            while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync(limit.Token)))
            {
                if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(header["Content-Length:".Length..].Trim(), out length);
            }
            if (length > 8192 || length < 1) throw new InvalidDataException("Unexpected mock body");
            var chars = new char[length];
            var read = 0;
            while (read < chars.Length)
            {
                var amount = await reader.ReadAsync(chars.AsMemory(read), limit.Token);
                if (amount == 0) throw new EndOfStreamException();
                read += amount;
            }
            var content = Encoding.UTF8.GetBytes(data);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\n" +
                $"Content-Type: {contentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, limit.Token);
            await stream.WriteAsync(content, limit.Token);
            await stream.FlushAsync(limit.Token);
            return requestLine + "\n" + new string(chars);
        }, limit.Token);
        var baseUrl = $"http://127.0.0.1:{port}/v1";
        string output = "";
        Exception? clientError = null;
        try { output = await client(baseUrl); }
        catch (Exception ex) { clientError = ex; }
        var received = await serverTask;
        if (clientError is not null) throw clientError;
        return (output, received);
    }

    internal static async Task<bool> RunAsync()
    {
        var sse = "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hello \"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"world\"}}]}\n\n" +
            "data: [DONE]\n\n";
        var captured = new StringBuilder();
        var good = await MockAsync(200, "text/event-stream", sse, baseUrl =>
            ProviderChat.StreamAsync(baseUrl, "mock", "test", null, chunk => { captured.Append(chunk); }));
        if (good.Output != "Hello world" || captured.ToString() != "Hello world" ||
            !good.Request.StartsWith("POST /v1/chat/completions", StringComparison.Ordinal) ||
            !good.Request.Contains("\"model\":\"mock\"", StringComparison.Ordinal)) return false;
        try
        {
            await MockAsync(429, "application/json", "rate limit", baseUrl =>
                ProviderChat.StreamAsync(baseUrl, "mock", "test", null));
            return false;
        }
        catch (InvalidOperationException) { }
        try
        {
            await MockAsync(200, "text/event-stream",
                "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n",
                baseUrl => ProviderChat.StreamAsync(baseUrl, "mock", "test", null));
            return false;
        }
        catch (InvalidDataException) { }
        try
        {
            await MockAsync(200, "text/event-stream",
                "data: {\"choices\":[{\"delta\":{\"tool_calls\":[]}}]}\n\ndata: [DONE]\n\n",
                baseUrl => ProviderChat.StreamAsync(baseUrl, "mock", "test", null));
            return false;
        }
        catch (InvalidOperationException) { }
        // Oversized, malformed and multibyte lines are validated BEFORE
        // converting an untrusted response line into a managed string.
        var bytes = Encoding.UTF8.GetBytes(new string('x', 65_537) + "\n");
        var oversized = new BoundedSseLineReader(new MemoryStream(bytes));
        try { await oversized.ReadLineAsync(CancellationToken.None); return false; }
        catch (InvalidDataException) { }
        var invalidUtf8 = new BoundedSseLineReader(new MemoryStream(new byte[] { 0xC3, 0x28, 0x0A }));
        try { await invalidUtf8.ReadLineAsync(CancellationToken.None); return false; }
        catch (InvalidDataException) { }
        var utf8 = new BoundedSseLineReader(new MemoryStream(
            Encoding.UTF8.GetBytes("data: € বাংলা\r\n")));
        if (await utf8.ReadLineAsync(CancellationToken.None) != "data: € বাংলা") return false;
        if (await utf8.ReadLineAsync(CancellationToken.None) is not null) return false;
        try { ProviderChat.Endpoint("http://8.8.8.8/v1"); return false; }
        catch (ArgumentException) { }
        try { ProviderChat.Endpoint("https://user:password@example.com/v1"); return false; }
        catch (ArgumentException) { }
        return true;
    }
}
