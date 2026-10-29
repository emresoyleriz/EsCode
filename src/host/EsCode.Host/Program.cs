using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Contains("--policy-spike", StringComparer.Ordinal)) return PolicySpike.Run() ? 0 : 1;
if (args.Contains("--revision-spike", StringComparer.Ordinal)) return RevisionSpike.Run() ? 0 : 1;
if (args.Contains("--recovery-spike", StringComparer.Ordinal)) return RecoverySpike.Run() ? 0 : 1;
if (!args.Contains("--ipc-spike", StringComparer.Ordinal))
{
    Console.WriteLine("EsCode .NET Host — development spike only. Use --ipc-spike to run local named-pipe verification.");
    return 0;
}
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Windows-only IPC capability verification.");
    return 2;
}
try
{
    var passed = await IpcSpike.RunAsync();
    Console.WriteLine(passed ? "IPC_SPIKE_PASS: same-user handshake, protocol and bounded frame verified." : "IPC_SPIKE_FAIL");
    return passed ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"IPC_SPIKE_FAIL: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

internal sealed record IpcMessage(int ProtocolVersion, string SessionToken, string Type, string Payload);

internal static class IpcSpike
{
    private const int SupportedProtocol = 1;
    private const int MaximumFrameBytes = 4096;
    public static async Task<bool> RunAsync()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if (Authorize(new IpcMessage(0, token, "hello", ""), token)) return false;
        if (Authorize(new IpcMessage(1, "wrong", "hello", ""), token)) return false;
        if (Authorize(new IpcMessage(1, token, "execute-tool", ""), token)) return false;
        var pipe = $"escode-ipc-spike-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var serverWork = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(timeout.Token);
            var frame = await ReadBoundedLineAsync(server, timeout.Token);
            var msg = JsonSerializer.Deserialize<IpcMessage>(frame);
            var authorized = msg is not null && Authorize(msg, token);
            var response = authorized ? "OK\n" : "DENIED\n";
            await server.WriteAsync(Encoding.UTF8.GetBytes(response), timeout.Token);
            await server.FlushAsync(timeout.Token);
            return authorized;
        }, timeout.Token);

        await using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(timeout.Token);
        var payload = JsonSerializer.Serialize(new IpcMessage(1, token, "hello", "handshake")) + "\n";
        await client.WriteAsync(Encoding.UTF8.GetBytes(payload), timeout.Token);
        await client.FlushAsync(timeout.Token);
        var reply = await ReadBoundedLineAsync(client, timeout.Token);
        var accepted = await serverWork;
        return accepted && reply == "OK";
    }

    private static bool Authorize(IpcMessage message, string expectedToken)
    {
        if (message.ProtocolVersion != SupportedProtocol || message.Type != "hello") return false;
        if (message.SessionToken.Length != expectedToken.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(message.SessionToken),
            Encoding.UTF8.GetBytes(expectedToken));
    }

    private static async Task<string> ReadBoundedLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Never use unbounded ReadLineAsync on untrusted pipe frames.
        using var data = new MemoryStream();
        var one = new byte[1];
        while (data.Length < MaximumFrameBytes)
        {
            var n = await stream.ReadAsync(one.AsMemory(), cancellationToken);
            if (n == 0) throw new EndOfStreamException("Connection closed before frame terminator.");
            if (one[0] == (byte)'\n') return Encoding.UTF8.GetString(data.ToArray());
            data.WriteByte(one[0]);
        }
        throw new InvalidDataException("IPC frame exceeds maximum allowed bytes.");
    }
}