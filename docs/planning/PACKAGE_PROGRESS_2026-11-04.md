# EsCode — Engineering Package Ledger (4 November 2026)

**Scope:** implementation packages addressed in the current development sequence. A merged code slice is NOT equivalent to completing the entire package or passing a release gate. Never set Stories/Epics to Done solely because a PR is merged.

| Requested package | Code delivery | Status of complete requested package | Explicit remaining work |
|---|---|---|---|
| G2.1 Windows Sandbox | [#95](https://github.com/emresoyleriz/EsCode/pull/95) + suspended-start probe (PR pending) | **PARTIAL** | Restricted token/AppContainer, FS/network egress, cross-user VM attack tests |
| G3 SQLite / Checkpoint / Recovery | [#96](https://github.com/emresoyleriz/EsCode/pull/96) + kill-point/backup hardening (PR pending) | **PARTIAL** | Global/workspace DB partition, migration rollback, power-loss/disk faults and trusted external effect verifier |
| G4.1 Workspace real files | [#97](https://github.com/emresoyleriz/EsCode/pull/97) | **PARTIAL** | Renderer bridge, multi-root, buffer revision, source identity/TOCTOU, approved patch transactions |
| G4.2 Git branch/worktree | [#98](https://github.com/emresoyleriz/EsCode/pull/98) | **PARTIAL** | Hook/filter sandbox, UI grants, dirty state, conflicts, commit/push/merge and revert |
| G4.3 Monaco / Terminal / Debugger | [#99](https://github.com/emresoyleriz/EsCode/pull/99) | **PARTIAL** | Real save, LSP/diagnostics, PTY, debugger and cancelable build/test |
| G5 LLM API / Model Studio | [#100](https://github.com/emresoyleriz/EsCode/pull/100) + bounded SSE/timeout hardening (PR pending) | **PARTIAL** | Trusted streaming IPC, secure key vault, provider routes/probes, Model Studio UI |
| G6.1 Agent Runtime | [#101](https://github.com/emresoyleriz/EsCode/pull/101) | **PARTIAL / PR REVIEW** | Real DAG, approval integration, tool loop, stop/resume, context and evidence |
| G6.2 Autonomous code workflow | No delivery | **OPEN** | Controlled patch→build→test→review, bounded retries and security gates |
| G7.1 Codebase Intelligence | Read-only [G7.1 FTS slice](../G7_1_CODEBASE_INDEX.md) (PR pending) | **PARTIAL** | Watcher, symbol graph, multi-root and context budgeting still open |
| G7.2 Cursor-like advanced coding | Local [Inline Edit slice](../G7_2_INLINE_EDIT.md) (PR pending) | **PARTIAL** | AI edit, LSP, completion, refactor, diagnostics, real save |
| G8.1 Multi-Agent / Independent Review | Plan critique-only slice (PR pending) | **PARTIAL** | Code diff review, isolated worktrees, reviewer evidence, conflict coordination |
| G8.2 MCP / Plugin / Browser | No delivery | **OPEN** | Trust manifests, sandboxed MCP and browser session isolation |
| G9.1 Premium UX / 13 screens | Existing UI prototype only | **OPEN** | Full functional UX, TR/EN, docking, accessibility and multi-monitor verification |
| G9.2 Windows Installer / Updates | No delivery | **OPEN** | Signed install, portable packaging, auto-update, rollback, backup |
| G10 E2E / Stable release | No delivery | **OPEN** | Clean Windows install, C#/Angular/React/Python golden projects, chaos/security and performance signoff |

## Non-negotiable delivery/acceptance conditions

1. Every package: one consolidated final commit and PR, evidence from clean CI, negative tests and reviewer findings.
2. User-controlled Git merge/push; do not silently grant privileged file/process/network access to an LLM.
3. Prevent false "Completed" states: an agent *plan* is not a code change; a local Job Object is not a sandbox; a CLI chat probe is not an integrated Model Studio.
4. The current build is a **development prototype**, not a finished Cursor replacement. G6.2 and later packages must not be marked complete without functional Windows E2E evidence.
5. GitHub main rulesets, human UX/security review and complete V18 archive still require independent verification.

## Recommended next implementation dependency

Complete G2.1's Windows sandbox restrictions and hostile-VM tests first; then close G4.1/G4.2 controlled writes and G4.3 real terminal before G6.2's supervised coding loop. Continue G5 Model Studio and G7.1 read-only context in parallel where no unsafe grants are introduced.
