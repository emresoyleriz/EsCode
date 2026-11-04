# EsCode .NET Host — Security boundary

The .NET 10 Host includes isolated Windows IPC, policy, revision and recovery spikes. G1 also introduces an actual authenticated **capabilities-only** Named Pipe connection from Electron Main.

Windows build and bridge integration:

    dotnet build src/host/EsCode.Host/EsCode.Host.csproj -c Release
    node --test tests/windows/ipc-host.test.mjs

The Host is started by Electron Main with the --serve-ipc switch and a secret bootstrap through stdin. Do not launch --serve-ipc directly without the matching parent protocol. Manual smoke switches remain available:

    dotnet run --project src/host/EsCode.Host -c Release -- --ipc-spike
    dotnet run --project src/host/EsCode.Host -c Release -- --policy-spike
    dotnet run --project src/host/EsCode.Host -c Release -- --revision-spike
    dotnet run --project src/host/EsCode.Host -c Release -- --recovery-spike

**Not certified:** cross-user/impersonation defense, Host process isolation, OS sandbox, persistent authenticated event streams, file or terminal tool authorization, crash-safe journal, model provider and agent runtime. See docs/G1_IPC_BRIDGE.md and docs/TECHNICAL_GATES_V20.md.
