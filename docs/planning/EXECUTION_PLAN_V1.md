# EsCode — Dependency-ordered Execution Plan V1

**Status:** proposed plan for review. No issue is automatically complete. REA excluded. Main untouched.

## Rules

- Keep the approved Electron + React + Monaco frontend and .NET 10 Host; no parallel agent engine.
- Execute one reviewable story/vertical slice per branch, Codex implementer and Copilot independent reviewer.
- Never bypass P0 IPC/sandbox/revision controls to make a demo work.
- Gate dependencies are blocking; independent UI/design and provider mock work may proceed in parallel but cannot be called integrated until prior gates pass.
- Merge, push to protected branches, and release remain human-controlled.
- The existing 5 TODO template items are insufficient alone: apply 8-part Definition of Done in REQUIREMENTS_TRACEABILITY_V1.md.
- Completion is defined as all scoped requirements + evidence passing, **not** a mathematical guarantee of zero bugs.

## Ordered gates

| Gate | Work | Existing issue links | Implementation scope | Exit evidence |
|---|---|---|---|---|
| G0 | Inventory and CI baseline | #19 #20 #21 #86 #87 | Lock toolchain, clean Windows build, branch rules, reconcile duplicate #38/#39 and missing archives | CI green on clean runner; issue inventory reconciled |
| G1 | Authenticated IPC | #22 #23 #24 #83 | Electron↔Host real protocol, replay/reconnect/backpressure | Unauthorized/replayed frames denied; host restart E2E |
| G2 | Tool sandbox and approvals | #25 #26 #27 | Fail-closed OS isolation, tool broker, worker lifecycle | Attacker-negative tests and cancellation pass |
| G3 | Persistence and workspace safety | #28 #29 #30 #31 #32 #33 | SQLite journal, recovery, multi-root, dirty buffers, safe patch | Crash injection, stale revision and symlink tests pass |
| G4 | Git and functional editor | #34 #35 #36 #37 #38 #39 #40 #41 #42 #43 #81 #84 #85 | Real file open/save, terminal, diff and user-controlled Git; UI gates | Windows GUI E2E, no dirty-buffer loss, rollback |
| G5 | LLM provider and Model Studio | #44 #45 #46 + PR #90 | Secrets, OpenAI-compatible adapters, chat streaming, tool/JSON capability, 429/retry | Local and mock remote provider matrix; secret redaction |
| G6 | Supervised agent and vertical slice | #47 #48 #49 #50 #51 #52 | Plan/approve/read/patch/test/review with bounded retries | Real .NET project feature and rollback; no unauthorized writes |
| G7 | Code intelligence and Cursor-like essentials | #53 #54 #55 #41 #42 | Incremental index, language adapters, context mentions/rules, inline edit, completion, LSP and debug scope | C#/Angular/React/Python/SQL fixtures and retrieval benchmarks |
| G8 | Multi-agent, plugins and observability | #56 #57 #58 #59 #60 #61 #62 #63 #64 | Isolated worktrees, independent review, MCP isolation, resource controls | Conflict, injection, resource exhaustion and cancellation tests |
| G9 | Product UI and distribution | #65 #66 #67 #68 #69 #70 #74–#81 | TR/EN, accessibility, installer, updates, backup, restore | Fresh Windows installation, mixed DPI, offline update rollback |
| G10 | Release certification | #71 #72 #73 | Threat model, chaos, model evaluation, SBOM and signoff | All R01–R20 linked to passing evidence; zero unresolved release blockers |

## Mandatory additions to existing story acceptance criteria

1. E08-S01 / E12-S02: editor navigation, completion, inline edits, LSP integration, diagnostics, debugger compatibility and keyboard UX.
2. E09-S01/S02/S03: provider profiles, secure key storage, streaming cancellation, tool-call and JSON-mode probes, 429/5xx/backoff, model budget, no unsafe fallback.
3. E10-S01/S02/S03: @file/@folder/@codebase/@git, project rules, compaction, token accounting, prompt-injection boundaries and session replay.
4. E08-S02 / E03-S03: true PTY and process-tree termination, stdout limits, terminal permissions and interactive input.
5. E11-S02/S03: reproducible golden fixture suite (C#/.NET + Angular, React, Python), patch conflicts, crash/restart, review, rollback and no false Completed.
6. E18-S03: signed Windows installer E2E, backup/restore, clean machine, security, accessibility and model matrix release checklist.
7. E07-S02 duplicate #38/#39: choose canonical issue and cross-link duplicate without destroying historical data.

## First three implementation slices

**Slice 0 (G0):** verify main vs open PRs; lockfiles/CI/branch protection, clean Windows build. Reconcile V18 artifacts and issue duplication.

**Slice 1 (G1):** authenticated Electron↔.NET Host read-only capability query. Negative IPC tests; no filesystem or shell permissions.

**Slice 2 (G5 preparatory, isolated CLI only):** review PR #90 provider probe, add mock-server tests, bounded response and URL policy. Do not merge into renderer until G1/G2 gates.

## Release acceptance scenarios

- New Windows 11 machine installs and opens EsCode, offline recovery and uninstall work.
- Opens an existing .NET + Angular project, saves editor changes and preserves dirty buffers across crash.
- Configures a trusted OpenAI-compatible API, probes chat/streaming/tool calling, handles unavailable models.
- Agent plans a user-management feature, requests scoped permission, patches files, runs build/test, presents review and allows revert.
- Git commit/push/merge are separately authorized by the user.
- React/Python and SQL fixtures, prompt injection, denied tool, rate limits, crash/restart, large repo, multi-monitor and resource exhaustion pass.
- Release gate cannot pass with skipped tests, unresolved critical vulnerabilities, or missing evidence.

## Review workflow

Every Story PR must cite requirement IDs, predecessor gate, acceptance tests, Windows evidence, threat/UX notes, reviewer signoff and rollback. Update traceability after each merge.