# Sprint 1 — Provider diagnostics (incremental vertical slice)

Status: implementation candidate; not a completed AI agent or approved production provider bridge.

## Architecture

Preserve the approved Electron/React/Monaco UI and .NET 10 Host. Do not introduce a competing TypeScript agent runtime or bypass the typed Electron-to-Host IPC boundary. This initial command is explicitly CLI-only until the authenticated bridge and authorization gates are validated.

## Windows usage

```powershell
$env:ESCODE_LLM_API_KEY = "your-key" # optional if server does not require it
dotnet run --project ./src/host/EsCode.Host/EsCode.Host.csproj -- --provider-probe --base-url http://127.0.0.1:8000/v1 --model qwen2.5-coder-7b
```

The command requests GET /v1/models and checks for an exact model ID. It does not perform chat completion, tool calling, filesystem changes, or terminal execution. Never commit keys. Clear the environment variable when finished.

## Acceptance and limitations

- Successful model listing and exact ID => PROVIDER_MODEL_FOUND (exit 0).
- Missing model, invalid JSON, non-200 status, timeout => nonzero exit.
- HTTP redirects are disabled; no raw response body or exception message is logged.
- This is a development diagnostic for a user-supplied trusted endpoint, **not** a hardened network boundary. Do not use with untrusted URLs: SSRF/egress restrictions, DNS rebinding defense, response size limits, cancellation, capability probes, credential vault and IPC integration are pending.
- No automatic autonomous mode or tool access is enabled.

## Next implementation sequence

1. Add mock-server positive/negative tests on Windows CI.
2. Implement bounded response reads, endpoint policy and provider profile persistence without storing secrets.
3. Probe chat, streaming and tool calling separately (never infer capabilities from model listing).
4. Connect the Host to Electron via authenticated, versioned IPC with approval checks.
5. Implement supervised read/search/patch/test workflow with revision guards, checkpoint and human review.
