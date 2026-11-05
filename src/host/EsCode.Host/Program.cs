using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Contains("--plugin-manifest-spike", StringComparer.Ordinal))
{
    var passed = PluginManifestSpike.Run();
    Console.WriteLine(passed ? "PLUGIN_MANIFEST_SPIKE_PASS" : "PLUGIN_MANIFEST_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--code-index-spike", StringComparer.Ordinal))
{
    var passed = CodebaseIndexSpike.Run();
    Console.WriteLine(passed ? "CODE_INDEX_SPIKE_PASS" : "CODE_INDEX_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--code-index", StringComparer.Ordinal))
    return CodebaseIndexCommand.Run(args);
if (args.Contains("--plan-review-spike", StringComparer.Ordinal))
{
    var passed = await PlanReviewSpike.RunAsync();
    Console.WriteLine(passed ? "PLAN_REVIEW_SPIKE_PASS" : "PLAN_REVIEW_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--plan-review", StringComparer.Ordinal))
    return await PlanReviewCommand.RunAsync(args);
if (args.Contains("--agent-plan-spike", StringComparer.Ordinal))
{
    var passed = await AgentPlanSpike.RunAsync();
    Console.WriteLine(passed ? "AGENT_PLAN_SPIKE_PASS" : "AGENT_PLAN_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--agent-plan", StringComparer.Ordinal))
    return await AgentPlanCommand.RunAsync(args);
if (args.Contains("--chat-spike", StringComparer.Ordinal))
{
    var passed = await ProviderChatSpike.RunAsync();
    Console.WriteLine(passed ? "PROVIDER_CHAT_SPIKE_PASS" : "PROVIDER_CHAT_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--chat", StringComparer.Ordinal))
    return await ProviderChatCommand.RunAsync(args);
if (args.Contains("--model-catalog-spike", StringComparer.Ordinal))
{
    var passed = await ModelCatalogSpike.RunAsync();
    Console.WriteLine(passed ? "MODEL_CATALOG_PASS" : "MODEL_CATALOG_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--provider-probe", StringComparer.Ordinal)) return await ProviderDiagnostics.RunAsync(args);
if (args.Contains("--serve-ipc", StringComparer.Ordinal)) return await PipeHost.RunAsync();
if (args.Contains("--worker-containment-child", StringComparer.Ordinal))
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    return 0;
}
if (args.Contains("--appcontainer-launch-spike", StringComparer.Ordinal))
{
    var passed = await AppContainerProcessSpike.RunAsync();
    Console.WriteLine(passed ? "APPCONTAINER_LAUNCH_PASS" : "APPCONTAINER_LAUNCH_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--appcontainer-profile-spike", StringComparer.Ordinal))
{
    var passed = AppContainerProfileProbe.Run();
    Console.WriteLine(passed ? "APPCONTAINER_PROFILE_PASS" : "APPCONTAINER_PROFILE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--suspended-worker-spike", StringComparer.Ordinal))
{
    var passed = await SuspendedWorkerProbe.RunAsync();
    Console.WriteLine(passed ? "SUSPENDED_WORKER_SPIKE_PASS" : "SUSPENDED_WORKER_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--worker-containment-spike", StringComparer.Ordinal))
{
    var passed = await WorkerContainmentSpike.RunAsync();
    Console.WriteLine(passed ? "WORKER_CONTAINMENT_SPIKE_PASS" : "WORKER_CONTAINMENT_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--git-output-guard-spike", StringComparer.Ordinal))
{
    var passed = await GitOutputGuardSpike.RunAsync();
    Console.WriteLine(passed ? "GIT_OUTPUT_GUARD_PASS" : "GIT_OUTPUT_GUARD_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--git-worktree-spike", StringComparer.Ordinal))
{
    var passed = await GitWorktreeSpike.RunAsync();
    Console.WriteLine(passed ? "GIT_WORKTREE_SPIKE_PASS" : "GIT_WORKTREE_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--workspace-approved-patch-spike", StringComparer.Ordinal))
{
    var passed = ApprovedWorkspacePatchSpike.Run();
    Console.WriteLine(passed ? "APPROVED_WORKSPACE_PATCH_PASS" : "APPROVED_WORKSPACE_PATCH_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--workspace-sessions-spike", StringComparer.Ordinal))
{
    var passed = WorkspaceSessionsSpike.Run();
    Console.WriteLine(passed ? "WORKSPACE_SESSIONS_PASS" : "WORKSPACE_SESSIONS_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--workspace-read-spike", StringComparer.Ordinal))
{
    var passed = WorkspaceReadSpike.Run();
    Console.WriteLine(passed ? "WORKSPACE_READ_SPIKE_PASS" : "WORKSPACE_READ_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--journal-partitions-spike", StringComparer.Ordinal))
{
    var passed = JournalPartitionSpike.Run();
    Console.WriteLine(passed ? "JOURNAL_PARTITIONS_PASS" : "JOURNAL_PARTITIONS_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--journal-safety-spike", StringComparer.Ordinal))
{
    var passed = JournalSafetySpike.Run();
    Console.WriteLine(passed ? "SQLITE_SAFETY_PASS" : "SQLITE_SAFETY_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--journal-kill-child", StringComparer.Ordinal))
    return await JournalCrashRecoverySpike.RunChildAsync(args);
if (args.Contains("--journal-crash-spike", StringComparer.Ordinal))
{
    var passed = await JournalCrashRecoverySpike.RunAsync();
    Console.WriteLine(passed ? "SQLITE_CRASH_RECOVERY_PASS" : "SQLITE_CRASH_RECOVERY_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--journal-spike", StringComparer.Ordinal))
{
    var passed = JournalSpike.Run();
    Console.WriteLine(passed ? "SQLITE_JOURNAL_SPIKE_PASS" : "SQLITE_JOURNAL_SPIKE_FAIL");
    return passed ? 0 : 1;
}
if (args.Contains("--broker-policy-spike", StringComparer.Ordinal))
{
    var passed = ToolPolicyBrokerSpike.Run();
    Console.WriteLine(passed ? "TOOL_BROKER_SPIKE_PASS" : "TOOL_BROKER_SPIKE_FAIL");
    return passed ? 0 : 1;
}
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