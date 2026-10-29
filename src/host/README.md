# EsCode .NET Host — IPC Spike

This is **NOT** a production-ready .NET Host.

Run on Windows 11 x64 with .NET 10 SDK:

```powershell
dotnet run --project src/host/EsCode.Host -- --ipc-spike
```

This bounded-frame local handshake verifies:
- A randomly named pipe and a random session token.
- `PipeOptions.CurrentUserOnly` where supported on Windows.
- Exact protocol version and handshake message type.
- Fixed-time token comparison; invalid token/version/type rejected by the authorization function.
- Maximum 4096-byte framed input.

**Does not verify:** cross-user ACL attack test, hostile process impersonation attempts, OS token restrictions, process supervisor, replay under reconnect, denial-of-service load, event streaming, long-running host, tool broker permissions or recovery. **P0 Secure IPC gate remains OPEN.**
