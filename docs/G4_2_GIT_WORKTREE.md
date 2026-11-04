# G4.2 — Git story worktree foundation (partial)

This package provides a trusted-Host-only Git worktree creation primitive. It invokes Git with `ArgumentList` (never shell), validates a strict story identifier and destination, limits command runtime, and consumes a scoped **one-time** ToolPolicyBroker grant before creating `story/<id>` from `refs/heads/main`. A Windows integration fixture creates a temporary local repository and asserts denial without a matching token, successful branch/worktree creation and replay denial.

**Not production-ready:** source repository Git hooks, checkout filters, external Git config, safe.directory, protected branch rules and hostile repo paths are not yet sandboxed. The test fixture controls all input and has no remote. Do not expose the worktree routine over IPC or allow agents to call it until Windows sandbox and user approval screens have passed. Push, merge, commit, conflict recovery, dirty buffers and worktree cleanup integration remain pending.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --git-worktree-spike
```
