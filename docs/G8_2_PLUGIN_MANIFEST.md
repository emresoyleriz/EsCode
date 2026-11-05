# G8.2 — Untrusted plugin manifest policy (partial)

This trusted Host module validates **metadata only**. It does not start a plugin process, execute an MCP tool, make a network request, access arbitrary filesystem paths or authorize browser navigation.

Strict policy: 16KiB maximum UTF-8 payload, depth-8 JSON, no unknown or duplicate properties, constrained plugin/publisher names, exact v1 contract, simple semantic version, TrustLevel=untrusted, no compatibility claims, and only optional workspace.read/workspace.search permission names. Duplicate permissions, shell, network and write requests are denied. Unverified signatures or claims of trusted status are denied.

Even an accepted metadata manifest has ExecutionAllowed=false. The renderer, models and plugins cannot turn validation into a Tool Broker grant.

### Windows CI

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --plugin-manifest-spike
```

**Remaining work before G8.2 can close:** cryptographic publisher trust, secure MCP/Plugin host, isolated worker under restricted Windows token/AppContainer, egress policy, browser origin/session isolation, permission UI, revocation, hostile-plugin VM tests and audits.
