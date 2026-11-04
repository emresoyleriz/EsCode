# G6.1 — Plan-only Agent orchestration (initial slice)

The trusted .NET Host now supports a **Plan** agent mode backed by the local G3 SQLite operation journal. It obtains a model response through G5's streaming OpenAI-compatible chat provider, records only a hashed story and run state, and outputs a human-readable plan. On model cancellation, it marks the run OutcomeUnknown rather than retrying automatically. On failure it marks Failed.

```powershell
"Add a users API and tests" | dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --agent-plan --base-url http://127.0.0.1:8000/v1 --model qwen2.5-coder-7b
```

The command uses ESCODE_LLM_API_KEY from the environment if needed. No raw prompt, API key, plan contents or user source code is persisted by the journal. The plan text is untrusted model output and **never automatically executes**.

**Still missing:** task DAG, tool calling, permissions/approval UI, safe file patches, revision guards, agent stop/resume, contextual file retrieval, multi-agent, end-to-end edit/build/test/review. This does **not** complete G6.1 or G6.2. Never treat plan generation as evidence of code changes.
