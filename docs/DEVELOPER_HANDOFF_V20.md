# EsCode — V20 Software Engineering Handoff
**Date:** 29 October 2026 · **Architecture:** Baseline 1.1 + V14 decisions (172 choices) · **Delivery:** technical reference, ongoing implementation, NOT production-ready.

## 0. One-minute summary
EsCode is a premium Windows 11 x64 AI-first developer IDE designed around a 13-screen original Apple-inspired UI (not a macOS clone), React + Monaco + Electron, a .NET 10 backend, locally authenticated Named Pipes, a risk-scoped OS Tool Broker, versioned SQLite persistence and evidence-driven multi-agent workflows. The approved MVP is **Project → Story → Plan → Edit → Test → Review → human-controlled Git**, with only Read-only/Ask/Supervised permissions. Autonomous agents are explicitly outside MVP until P0 security gates are satisfied.

The merged PR #82 introduced a Vite/vanilla JS UI prototype, 15 draft JSON schemas, risk/approval/revision/recovery smoke tests and 18 Epics / 54 Stories / 270 checklist tasks. It passed CI on main [run 40899370170](https://github.com/emresoyleriz/EsCode/actions/runs/40899370170). The corrective [PR #89](https://github.com/emresoyleriz/EsCode/pull/89) replaces the UI with React+actual local Monaco editing, introduces the Electron shell, TR/EN, persisted preferences, agent floating/docking and pinned npm dependencies. Merge only after full CI/review. Current code still lacks real production agent/tool/file systems.

## 1. Implemented, tested or pending — keep these distinct
| Area | Implementation | Verified evidence | Remaining before completion |
| --- | --- | --- | --- |
| 13-screen navigation | React v2 in PR #89 | Playwright visual navigation in 26 dark/light screenshots | Approved high-fidelity screen design, error/permission/reconnect states |
| Premium UI tokens | Midnight Glass / Pearl Light, density | Screenshot smoke; CSS renders | Semantic token build, contrast math, native Windows DPI, human design signoff |
| Monaco code editing | Real Monaco, local ESM workers and in-memory buffers | Vite production compile, browser screenshots | Disk save/open, LSP, cross-file revision, dirty buffer crash recovery |
| Electron desktop | Sandboxed BrowserWindow, minimal preload, custom protocol | Static Node safety tests and build | Windows GUI e2e, custom protocol/worker runtime security, installer, IPC hookup |
| i18n/preferences | TR/EN dictionary and versioned, bounded nonsecret localStorage | Node unit tests for parity, invalid states and roundtrip | Full translation of dynamic/error strings, RTL-ready extensibility, persisted per-workspace profiles |
| Agent UI | Docked/floating panels and pin/resize prototype | UI screenshot/navigation, preference tests | Native detached windows, mixed DPI restore, real agent state machine and streaming |
| Contracts | 15 versioned JSON Schemas | JSON parsing/structural contract tests | Typed nested payloads, codegen, .NET/TS compatibility, validation security semantics |
| Windows IPC | Separate .NET 10 same-user pipe handshake spike | Windows CI: bounded frame and token handshake | Cross-user attacks, reconnect, stream replay, process ACL and actual Electron/.NET connection |
| Tool approvals | Scoped tool grant negative test spike | Windows CI | OS sandbox, real broker, revocation under concurrency and full permission matrix |
| Workspace safety | File/git/buffer revision guard spike | Windows CI | Real Monaco-to-host snapshots, atomic file patch and worktree E2E |
| Unknown outcome | Retry decision spike | Windows CI | SQLite operation journal, crash injection, reconciliation with real external effects |
| Git collaboration | Issues, PR, main merge flow | GitHub Issue count | Enforced main ruleset not verified, real worktree/Git UI, signed releases |
| Original V18 archives | Archived Markdown/JSON/CSV in separate download | Local ZIP manifest and integrity checks in conversation environment | Full V18 content **not yet imported** to GitHub; do not falsely close #87 |

## 2. Non-negotiable boundaries
- Renderer handles UI state and editor buffers; **no** direct OS shell, filesystem secrets or model-to-tool execution.
- Electron Main owns native window lifecycle and a strictly typed minimal preload contract; no generic `ipcRenderer` exposure.
- .NET Host is the authoritative durable task/agent/workspace state owner; SQLite per-workspace journal, checkpoint and backup.
- Tool Broker enforces Actor + Tool + Resource + ArgsDigest + Risk + Expiry and verifies permissions at execution time; insufficient OS capability -> **fail closed**.
- Every patch carries workspace, Git, file hash and unsaved editor buffer revisions. Never silently overwrite dirty source.
- Git commit/push/merge are separate, explicit human-authorized actions. The agent must never silently merge.
- No automatic retry for `OutcomeUnknown`; obtain evidence/reconciliation first.
- Local and cloud LLM provider capabilities are probed, not guessed. User override cannot bypass mandatory security.
- Plugin/MCP output is untrusted; apply permission manifest, isolation, egress controls and prompt-injection defenses.

## 3. UI is a release-blocking track
Original premium Apple-inspired presentation with Windows-native behaviors. Dark: #0C1120 background, #161D2E surface, #796CF4 accent; light: #F6F8FC, #FFFFFF, #6256D9. Code, diff, log and terminal remain opaque for legibility. Compact/Comfortable/Focus profiles, floating+pinning, reduced motion, font scaling, keyboard-only workflows and TR/EN text overflow tests.

**Thirteen screens:** Welcome Hub, IDE, Agent Studio, Task Center, Code Intelligence, Git & Review, Approval Center, Model Studio, MCP Manager, Terminal & Debugger, Browser Automation, Settings & Themes, Recovery & Insights.

Detailed acceptance plan: `design/SCREEN_ACCEPTANCE_MATRIX.csv` (13 × 14 = 182 checks) and `design/COMPONENT_STATE_MATRIX.csv` (32 × 12 = 384 checks). These are **NOT_TESTED** checklists, not successes. See [UI Gate #81](https://github.com/emresoyleriz/EsCode/issues/81) and UI gaps #74-#80.

Each screen is accepted only when there is an approved dark/light mockup, actions/state spec, mouse/keyboard/screen reader route, empty/error/offline/approval/recovery behavior, contrast WCAG 2.2 AA, appropriate localization, responsive windows/mixed DPI, visual regression baseline approved by human reviewer and p95 benchmarks under indexing/LLM load. Proposed targets: palette p95 <= 100 ms; panel p95 <= 150 ms; typing p95 <= 50 ms, normal animations 60fps. None is certified for production yet.

## 4. Realistic development order (not permission to skip gates)
| Order | Phase | Workstream | Parent issues | Acceptance |
|---|---|---|---|---|
| 0 | Foundation | Pinned toolchain, npm lock, CI, branch protection | #1, #86 | `npm ci`, build/test reproducible; PR protection verified |
| 1 | P0 Spike | Authenticated Electron↔.NET Named Pipes | #2, #83 | Real client/server integration, ACL/replay/reconnect/negative tests |
| 2 | P0 Spike | Windows OS sandbox / Tool Broker | #3 | Escape, filesystem/network/process negative tests on Windows 11 isolated VM |
| 3 | P0 Spike | SQLite durability, operation journal, crash recovery | #4 | Kill-point failure injection and no duplicated side effect |
| 4 | P0 Spike | Workspace buffer snapshot / patch / worktrees | #5, #6 | No silent dirty buffer loss, revision mismatch blocks |
| 5 | Parallel MVP UI | React/Monaco/Electron, true docking, TR/EN, premium UX | #7, #8, #16, #81, #83-#85 | 13-screen UI acceptance and real desktop E2E |
| 6 | MVP | Model provider/ capability probes and scoped agent orchestration | #9, #10 | Verified provider features, task DAG & approval/evidence |
| 7 | MVP | Story→plan→edit→test→review→user-controlled Git | #11 | Full supervised vertical slice with crash and security fail-path tests |
| 8 | Beta | Code indexing, multi-agent merge, MCP/browser, monitoring | #12-#15 | Reliable indexed context, isolated parallel changes, plugin trust |
| 9 | Stable | Installer, updater, rollback, release audits | #16-#18 | Signed distributable, backup restore, WCAG, performance, threat evidence |

## 5. Tests and evidence links
- Merged baseline PR: https://github.com/emresoyleriz/EsCode/pull/82
- Merged main CI: https://github.com/emresoyleriz/EsCode/actions/runs/40899370170 (all 3 jobs succeeded).
- Corrective desktop PR: https://github.com/emresoyleriz/EsCode/pull/89 (review / merge pending).
- Electron and UI baseline tests: `npm test`, `npm run build`, `npm run visual`; UI/Windows CI defined at `.github/workflows/ui-foundation.yml`.
- Windows verification via `dotnet run --project src/host/EsCode.Host -c Release -- --ipc-spike`, `--policy-spike`, `--revision-spike`, `--recovery-spike`.
- For real smoke, run Windows 11 x64 interactive Electron GUI, not just CI shell checks.

## 6. Developer commands (after merging PR #89)
```powershell
git clone https://github.com/emresoyleriz/EsCode.git
cd EsCode
npm ci
npm test
npm run build
npm run dev
# in separate PowerShell: npm run desktop
dotnet build .\src\host\EsCode.Host\EsCode.Host.csproj -c Release
dotnet run --project .\src\host\EsCode.Host\EsCode.Host.csproj -c Release -- --ipc-spike
```
Note: `npm run desktop` opens the built `dist` preview (after `npm run build`). Running live Vite desktop is optional and requires `ESCODE_DEV_SERVER=1` in the Electron process environment. No live .NET Host/IDE IPC is wired yet.

## 7. Known risks and decisions needing external verification
1. **Windows sandbox**: Risk-based OS isolation has no attacker-proven test evidence. Job objects do not enforce filesystem isolation on their own.
2. **Authenticated IPC**: Limited same-user spike passed; cannot be accepted as complete secure IPC protocol.
3. **SQLite recovery**: State/intent journals and backup/restore not implemented for live operations.
4. **Electron security**: Sandbox/preload restrictions are scaffolded; evaluate custom protocol, CSP, worker loading, update signing and runtime attacks.
5. **Premium UX**: Concept and screenshots exist, not cross-DPI/assistive-tech/latency acceptance.
6. **Main protection**: GitHub API returned 403 for branch protection inspection; public rulesets endpoint was empty when checked. Owner must configure and verify required PR reviews and CI.
7. **Original V18 package**: GitHub Issue mirror does not include all 81 original files. Attach and sync original archive with verified manifest/hashes.
8. **Model performance**: External local GPU model resource handling and tool calling capability are not proven by the UI.

## 8. GitHub issue and change conventions
- Epics: #1-#18, Stories: 54 Issue entries with five subtasks each; audit UI #74-#81; postmerge corrective #83-#87.
- Implement one Story ID per branch, make minimal patch, run checks, independently review evidence, request explicit user-controlled commit/push/merge.
- Do not claim complete on `main` merely because PR contains unmerged code.
- Closing an Issue requires all TODO checks, positive+negative test evidence, UI evidence where relevant, reviewer approval, and matching release gate.
- New risks or technology substitutions require an ADR; do not silently alter V1-V14 decisions.

## 9. Handoff signoff checklist
- [ ] PR #89 reviewed, tests green and merged by repository owner.
- [ ] Windows GUI smoke demonstrates Electron/Monaco and local non-CDN workers.
- [ ] `npm ci` reproducible with committed lockfile, toolchain patch versions documented.
- [ ] 13-screen UI baseline signed by designer/product owner.
- [ ] GitHub main protection required reviewers + CI actually configured and checked.
- [ ] P0 sandbox, IPC, crash recovery and patch/merge negative tests attached.
- [ ] Original V18 archive uploaded into versioned docs with manifest comparison.
- [ ] Autonomous remains disabled in MVP.
- [ ] Stable release gate remains closed until evidence supports production deployment.

**Outcome:** This package is sufficient for a software engineer to start prioritized implementation safely; it is not evidence that the whole product is completed.
