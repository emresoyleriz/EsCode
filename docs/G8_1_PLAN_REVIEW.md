# G8.1 — Separate read-only plan reviewer (partial)

Status: model-generated **plan critique**, not verified review of code changes. No privileged tool access.

The Host can now run separate author and reviewer model calls, optionally using different model IDs. The reviewer prompt treats the author plan as untrusted data and requests missing tests, security risks and rollback analysis. No model response grants approval, runs Git or changes files. Every result is labelled unverified and requires human approval.

Only hashed run metadata is stored in SQLite. The plan and reviewer text are not persisted there. Cancellation becomes OutcomeUnknown without automated retry. Both calls use the G5 bounded SSE provider client.

### Windows test and operator usage

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --plan-review-spike
'Add users API' | dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --plan-review --base-url http://127.0.0.1:8000/v1 --model qwen2.5-coder-7b --reviewer-model qwen2.5-coder-7b
```

Still open: real code-diff review, reviewer-isolated worktrees, separate identity, reviewer disagreement resolution, evidence links, test/CI proof, agent scheduling and UI integration. A second call to the same model is not independent human review. Agent authorization and sandbox gates remain closed.
