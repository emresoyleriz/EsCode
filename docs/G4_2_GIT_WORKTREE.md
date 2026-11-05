# G4.2 — Git story worktree foundation (partial)

This package provides a trusted-Host-only Git worktree creation primitive. It invokes Git with `ArgumentList` (never shell), validates a strict story identifier and destination, limits command runtime, and consumes a scoped **one-time** ToolPolicyBroker grant before creating `story/<id>` from `refs/heads/main`. A Windows integration fixture creates a temporary local repository and asserts denial without a matching token, successful branch/worktree creation and replay denial.

**Not production-ready:** source repository Git hooks, checkout filters, external Git config, safe.directory, protected branch rules and hostile repo paths are not yet sandboxed. The test fixture controls all input and has no remote. Do not expose the worktree routine over IPC or allow agents to call it until Windows sandbox and user approval screens have passed. Push, merge, commit, conflict recovery, dirty buffers and worktree cleanup integration remain pending.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --git-worktree-spike
```

## G4.2 trusted Git process-output hardening and tracked status

- Git subprocess output is now bounded **while reading**, with a 128 KiB stdout and 32 KiB stderr cap. Overflow or timeout fails closed, terminates the process tree and never returns partial success or raw errors.
- Git system/global config is disabled for the trusted Git fixture; an arbitrary hostile repo's local config, hooks and filters remain untrusted. The agent Git execution path is NOT enabled.
- The trusted Host can obtain porcelain status of tracked files only, with core.fsmonitor disabled. The Windows fixture modifies a worktree file and verifies a tracked status indicator.
- Negative tests cover bounded and oversized output; UI IPC, commit, push and merge require separate scope grants and sandbox verification.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --git-output-guard-spike
```
