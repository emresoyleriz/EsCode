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

## G2.1 AppContainer OS capability probe

The Windows CI now creates a **disposable, uniquely named** AppContainer profile with no capabilities, derives the same SID through the OS, verifies SID equality, releases both SID allocations and deletes the profile in a finally block.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --appcontainer-profile-spike
```

This is **only a capability probe**. An AppContainer profile does not place an arbitrary child in an AppContainer by itself. The actual AppContainer process launch, restricted token, job assignment, filesystem/network denial, cross-user hostile tests and approved tool broker integration must pass before untrusted code runs. Inability to provision an AppContainer MUST NOT silently fall back to a same-user unrestricted child. The profile name is generated inside trusted Host code; no renderer/model-provided name is accepted.

## G2.1 — Real restricted-process launch token verification

The Windows-only `--appcontainer-launch-spike` creates a unique disposable AppContainer
profile with **zero capabilities**, constructs a process attribute list with
PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES, and launches only the known Windows
system `whoami.exe /user` utility **suspended**.

Before allowing its primary thread to resume, the trusted Host checks
TokenIsAppContainer=1, verifies the token AppContainer SID matches the newly
created profile and asserts TokenCapabilities has zero entries. It also assigns
the child to a kill-on-close Windows Job Object *before* resuming. Handles and
profile are disposed on success or failure.

This is proof of a low-privilege **test process**, not a complete arbitrary-code
sandbox. **No renderer, model, user-controlled executable, workspace file, shell
command or MCP process can call this launcher.** OS-level negative tests for
filesystem writes, network access/egress, cross-user access, token escalation,
inherited handles and symlink attacks remain a blocking security gate; an
AppContainer identity alone is not sufficient evidence.

Run in Windows CI:

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --appcontainer-launch-spike
```

Technical reference: https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer
