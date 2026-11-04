using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// E02-S01: bounded, authenticated, read-only Windows Named Pipe.
// NO file/terminal/tool/agent execution endpoint exists here.
internal static class PipeHost
{
    private const int Version = 1;
    private const int MaximumFrame = 4096;
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return 2;
        try
        {
            using var bootstrapTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var line = await ReadLineAsync(Console.OpenStandardInput(), 512, bootstrapTimeout.Token);
            using var doc = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 4 });
            var boot = doc.RootElement;
            if (boot.ValueKind != JsonValueKind.Object || boot.EnumerateObject().Count() != 4 ||
                !ReadInt(boot, "protocolVersion", out int version) || version != Version ||
                !ReadInt(boot, "parentPid", out int parentPid) || parentPid <= 0 ||
                !ReadString(boot, "pipeName", out var name) ||
                !Regex.IsMatch(name, "^escode-[a-f0-9]{32}$", RegexOptions.CultureInvariant) ||
                !ReadString(boot, "sessionSecret", out var encodedSecret) ||
                !Regex.IsMatch(encodedSecret, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)) return 2;

            byte[] secret = Convert.FromHexString(encodedSecret);
            try
            {
                using var parent = Process.GetProcessById(parentPid);
                using var shutdown = new CancellationTokenSource();
                EventHandler stopOnExit = (_, _) => shutdown.Cancel();
                parent.EnableRaisingEvents = true;
                parent.Exited += stopOnExit;
                try
                {
                    if (parent.HasExited) return 2;
                    try { await ServeAsync(name, secret, shutdown.Token); }
                    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
                }
                finally { parent.Exited -= stopOnExit; }
            }
            finally { CryptographicOperations.ZeroMemory(secret); }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException
            or OperationCanceledException or InvalidOperationException)
        {
            // Never log a supplied session secret or raw IPC frame.
            return 2;
        }
    }

    private static async Task ServeAsync(string name, byte[] secret, CancellationToken shutdown)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seenOrder = new Queue<string>();
        while (!shutdown.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(shutdown);
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                var request = await ReadLineAsync(server, MaximumFrame, requestTimeout.Token);
                var requestId = AuthenticateRequest(request, secret);
                if (requestId is null || !seen.Add(requestId))
                {
                    await WriteLineAsync(server, "DENIED", requestTimeout.Token);
                    continue;
                }
                seenOrder.Enqueue(requestId);
                if (seenOrder.Count > 256) seen.Remove(seenOrder.Dequeue());
                var response = new
                {
                    protocolVersion = Version,
                    requestId,
                    type = "capabilities",
                    mode = "host-read-only",
                    hostConnected = true,
                    auth = Sign(secret, $"response|{Version}|{requestId}|host-read-only|true")
                };
                await WriteLineAsync(server, JsonSerializer.Serialize(response, WireJson), requestTimeout.Token);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or InvalidOperationException
                or OperationCanceledException or DecoderFallbackException or ArgumentException)
            {
                // Invalid and stalled clients fail closed; keep the service available.
            }
        }
    }

    private static string? AuthenticateRequest(string frame, byte[] secret)
    {
        using var doc = JsonDocument.Parse(frame, new JsonDocumentOptions { MaxDepth = 4 });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 ||
            !ReadInt(root, "protocolVersion", out var version) || version != Version ||
            !ReadString(root, "requestId", out var id) || !Regex.IsMatch(id, "^[a-f0-9]{32}$") ||
            !ReadString(root, "type", out var type) || type != "capabilities" ||
            !ReadString(root, "auth", out var auth) || !Regex.IsMatch(auth, "^[a-f0-9]{64}$")) return null;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name is not ("protocolVersion" or "requestId" or "type" or "auth")) return null;
        }
        var expected = Convert.FromHexString(Sign(secret, $"request|{Version}|{id}|capabilities"));
        var actual = Convert.FromHexString(auth);
        return CryptographicOperations.FixedTimeEquals(expected, actual) ? id : null;
    }

    private static string Sign(byte[] secret, string message) =>
        Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(message))).ToLowerInvariant();

    private static bool ReadInt(JsonElement value, string name, out int result)
    {
        result = 0;
        return value.TryGetProperty(name, out var child) &&
            child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out result);
    }
    private static bool ReadString(JsonElement value, string name, out string result)
    {
        result = "";
        if (!value.TryGetProperty(name, out var child) || child.ValueKind != JsonValueKind.String) return false;
        result = child.GetString() ?? "";
        return true;
    }
    private static async Task<string> ReadLineAsync(Stream stream, int limit, CancellationToken cancellation)
    {
        using var buffer = new MemoryStream();
        var one = new byte[1];
        while (buffer.Length < limit)
        {
            if (await stream.ReadAsync(one.AsMemory(), cancellation) != 1) throw new EndOfStreamException();
            if (one[0] == '\n') return StrictUtf8.GetString(buffer.ToArray());
            buffer.WriteByte(one[0]);
        }
        throw new InvalidDataException("IPC frame size exceeded");
    }
    private static Task WriteLineAsync(Stream stream, string message, CancellationToken cancellation)
    {
        if (Encoding.UTF8.GetByteCount(message) + 1 > MaximumFrame) throw new InvalidDataException();
        return stream.WriteAsync(Encoding.UTF8.GetBytes(message + "\n").AsMemory(), cancellation).AsTask();
    }
}
