# G2.1 — Windows worker containment primitive

Status: **partial G2.1 implementation**; do not mark Windows Sandbox complete.

- Windows Job Object wrapper enforces kill-on-job-close, active-process limit and job memory cap; fails closed if unsupported.
- CI invokes only a known, harmless test child process. No arbitrary shell, file, model or Git command is accepted.
- The CLI probe verifies that disposing the job terminates the child.
- Existing authenticated IPC and single-use Tool Broker restrictions remain unchanged.

**Security limitations:** Job Objects are *not* filesystem isolation, network isolation, restricted token, AppContainer, mitigation policy, suspended-creation process assignment, user isolation, integrity-level enforcement or escape prevention. A worker started normally can execute before assignment; therefore **never use this primitive alone to run untrusted agent code**. The production tool executor remains blocked.

### Verification
```powershell
dotnet build src/host/EsCode.Host/EsCode.Host.csproj -c Release
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --worker-containment-spike
```

Required future work: suspended process startup/assignment, restricted token or AppContainer, per-workspace file ACLs, network egress allowlist, cross-user/attacker VM tests, timeouts, cancellation and privileged tool broker at execution time. Issues #25–#27 stay open.

## Additional lifecycle hardening: suspend-before-assign probe

A second **known-binary-only** Windows smoke test starts the harmless EsCode.Host test child with Win32 CREATE_SUSPENDED, assigns the child to a kill-on-close Job Object, resumes its primary thread only after successful assignment and checks termination on Job close. Any failure terminates and closes native handles.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --suspended-worker-spike
```

**Do not treat this as a full sandbox.** The process still uses the caller's privileges, environment, filesystem and network access. Restricted tokens/AppContainer, deny-by-default egress, handle policy and hostile Win11 VM attack tests remain blocking before arbitrary tool execution. No production agent tool invokes this spike.
