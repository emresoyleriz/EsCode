# G2 — Tool Broker authorization core (implementation candidate)

This phase adds a fail-closed, single-use, bounded in-memory grant evaluator to the
.NET Host. **This is not an OS sandbox, not an agent executor, and not an approval
endpoint.** No file, terminal, Git or network operation can be performed through it.

## Trust boundary

Only the trusted .NET Host approval coordinator may call `GrantAfterTrustedHumanApproval`
**after** explicit human approval. The renderer, model output, plugin, MCP and IPC
messages must never be able to mint grants or set `Approved=true`. Callers must
supply a canonical workspace identity and a SHA-256 digest of fully normalized
arguments. Before executing an actual tool in future, the OS Tool Broker must
re-validate symlinks, actual resolved file identity, process privileges and current
approvals. This class alone **cannot** authorize safe OS actions.

## Policy

- Default is deny. ReadOnly and Ask allow only read/search requests with explicitly
  minted grants. Supervised permits those plus a narrow tool allowlist with separate
  one-time grants for patch, build, test and human-controlled Git commit/push/merge.
- Critical-risk requests are always denied. Tool/resource/actor/arguments digest and
  risk must match the original grant exactly; no wildcard resource matching.
- Grants expire within five minutes, may be revoked, and are single use even if a
  denied invocation attempts to consume them.
- Grant count is capped to protect resources. Unknown tools, path traversal,
  malformed identities, missing hashes and cross-workspace use fail closed.

## Windows verification

Run `dotnet build src/host/EsCode.Host/EsCode.Host.csproj -c Release`, then:

    dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --broker-policy-spike

Test output must be `TOOL_BROKER_SPIKE_PASS`. CI must also retain existing G0/G1 jobs.

## Remaining G2 gates (open)

- E03-S01: Windows OS isolation capability and attacker-negative tests in VM.
- E03-S02: Real human approval UI, typed IPC authorization and execution-time
  resolution checks; this authorization spike alone does not close the Story.
- E03-S03: Process-tree cleanup, resource limits, cancellation, sandbox containment.
- Persistent audit/recovery journals and safe patch conflicts remain pending later phases.

Reviewers must reject any attempt to expose a generic `executeTool` IPC method
or treat an HMAC-authenticated Host connection as equivalent to OS sandboxing.
