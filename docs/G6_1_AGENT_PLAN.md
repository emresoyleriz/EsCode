# G6.1 — Plan-only Agent orchestration (initial slice)

The trusted .NET Host now supports a **Plan** agent mode backed by the local G3 SQLite operation journal. It obtains a model response through G5's streaming OpenAI-compatible chat provider, records only a hashed story and run state, and outputs a human-readable plan. On model cancellation, it marks the run OutcomeUnknown rather than retrying automatically. On failure it marks Failed.

```powershell
"Add a users API and tests" | dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --agent-plan --base-url http://127.0.0.1:8000/v1 --model qwen2.5-coder-7b
```

The command uses ESCODE_LLM_API_KEY from the environment if needed. No raw prompt, API key, plan contents or user source code is persisted by the journal. The plan text is untrusted model output and **never automatically executes**.

**Still missing:** task DAG, tool calling, permissions/approval UI, safe file patches, revision guards, agent stop/resume, contextual file retrieval, multi-agent, end-to-end edit/build/test/review. This does **not** complete G6.1 or G6.2. Never treat plan generation as evidence of code changes.

## G6.1 durable task DAG and fail-closed restart

`DurableTaskGraph` now validates a bounded 64-node graph for duplicate IDs,
missing/cyclic dependencies and forbidden tool-task kinds. It records a
SHA-256 digest of the **immutable task graph manifest** in the SQLite
operation journal; a changed graph cannot be silently substituted on restart.
Only analyze/plan/review/verify tasks can enter the graph.

Tasks become ready only when every prerequisite has succeeded. Start/finish
transitions are journaled. After a simulated crash, `RecoverInterrupted`
marks running tasks OutcomeUnknown. Their dependants become blocked; there
is no automatic retry, shell, Git or file modification. Runtime pause stops
further task starts (in-memory only; durable pause still open).

The Windows CI smoke verifies graph restart, independent run IDs, recovery,
failure propagation, malformed graphs, cycle detection, and forbidden
`process.shell` task kinds.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --durable-task-dag-spike
```

**Still open:** durable pause/resume controls, safe dispatch to tool workers,
human approval UI, task output evidence, provider streaming during agent
runs, resource budgets, retries with verified external effects and real
edit/build/test. The graph is NOT an autonomous code-writing system.
