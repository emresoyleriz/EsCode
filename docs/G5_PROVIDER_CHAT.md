# G5 — OpenAI-compatible streaming chat (initial provider slice)

This CLI-only .NET Host service can call a user-configured OpenAI-compatible `/v1/chat/completions` endpoint with `stream=true`. It supports SSE token chunks, a bounded response size, timeouts, 429/HTTP error classification, refusal to execute tool calls and a bearer API key only from the `ESCODE_LLM_API_KEY` environment variable. No secrets are saved or sent to the renderer. Redirects are disabled. Plain HTTP only permits loopback; HTTPS is permitted for an explicitly configured trusted endpoint.

### Local development usage

```powershell
$env:ESCODE_LLM_API_KEY = "your-key"  # omit for no-key local endpoint
"Create a C# method to validate a GUID." | dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --chat --base-url http://127.0.0.1:8000/v1 --model qwen2.5-coder-7b
```

Use `--chat-spike` to run local deterministic SSE success/denial/malformed stream tests without a remote model.

**Limitations:** A successful chat probe does not certify native tool calling, model compatibility, safe file edits, Model Studio UI, credential vault, cancellation UX, retries/backoff, streaming event replay or trusted network egress controls. HTTPS endpoint trust is user-supplied; do not let LLM prompts or retrieved data choose URLs. No agent execution or IPC tool bridge exists here. E09-S01/02/03 remain OPEN.
