# EsCode — Requirements Traceability Matrix (review candidate)

Baseline: GitHub main, 2026-11-04. Status is **planned**, not implemented or verified. REA excluded. Architecture remains Electron/React/Monaco + .NET 10 Host.

## Inventory

- 86 issue search results; 55 story issue records; 54 unique story IDs.
- Duplicate **E07-S02**: #38 and #39. Reconcile without deleting history.
- 18 epic groups. Existing audit issues #83–#87 and UI gate #74–#81 must be included in closure checks.
- Draft provider PR #90 is diagnostic only; E09-S01 is NOT complete.
- Search inventory is a point-in-time snapshot; issue status, branch and CI must be revalidated before closure.

## Requirement → Epic → verification

| Requirement | Owning Epic(s) | Release-level verification |
|---|---|---|
| R01 Reproducible Windows installation/build and CI | E01,E17 | Clean Windows 11 x64 install, build, test, uninstall; pinned lockfiles and signed artifact verification |
| R02 Authenticated, versioned UI-to-host communication | E02 | Unauthorized/replayed/oversized messages rejected; reconnect and cancellation verified |
| R03 Permission-enforced isolated tools | E03 | Negative sandbox tests, scoped grants, no direct renderer execution |
| R04 Crash-safe durable task and operation state | E04 | Kill-point recovery, idempotency and no duplicate side effects |
| R05 Real multi-root workspace and unsaved buffer integrity | E05,E08 | Open/save/reload, symlink guard, conflict and dirty-buffer E2E |
| R06 Human-controlled Git branch/diff/rollback | E06,E11 | Review, commit, push and merge separately authorized |
| R07 Complete desktop/editor interaction | E07,E08,E16 | 13-screen UX, keyboard navigation, layout persistence, editor semantics |
| R08 OpenAI-compatible/local/cloud provider and credential management | E09 | Profiles, secure secrets, real streaming, retries, tool/JSON capability tests |
| R09 Supervised agent planning and execution | E10,E11 | Plan→approve→read→patch→build→test→review, cancellation and failure recovery |
| R10 Code intelligence and context efficiency | E12 | C#/Angular/React/Python/SQL retrieval, indexing freshness, token budgets |
| R11 Multiple agents and independent review | E13 | Isolated worktrees, conflict handling, reviewer evidence |
| R12 MCP/plugins/browser trust boundary | E14 | Prompt injection tests, permission manifest, isolated sessions |
| R13 Telemetry and resource budgets | E15 | Redacted logs, CPU/RAM/VRAM budgets, bounded queues |
| R14 Accessible localized high-quality UX | E07,E16 | TR/EN, WCAG, DPI, performance and screenshots |
| R15 Secure packaging/update/backup and release gates | E17,E18 | Installer/update rollback, restore, chaos and signoff |
| R16 Inline edit, completion, symbol navigation and debugging | E08,E12 | Live language-server scenarios, autocomplete acceptance and debugger E2E; explicit scope decision required |
| R17 Context mentions, rules and prompt budget management | E09,E10,E12 | @file/@folder/@codebase/@git, project rules, compaction and provenance tests |
| R18 Model/provider error handling and cost visibility | E09,E15 | 429/5xx/timeout/cancel, capability degradation, token and price estimates |
| R19 Real agent PTY/test/process lifecycle | E03,E08,E11 | Bounded stdout/stderr, stdin, process-tree cancellation, test fixtures |
| R20 Release-level real-project acceptance suite | E11,E18 | Clean Windows run on .NET+Angular, React and Python fixtures; no false-complete states |

## Existing Story ledger (all records; order within epic)

| Epic | Story | GitHub | Scope |
|---|---|---|---|
| E01 | E01-S01 | [#19](https://github.com/emresoyleriz/EsCode/issues/19) | Monorepo boundaries, lockfiles, reproducible local build. |
| E01 | E01-S02 | [#20](https://github.com/emresoyleriz/EsCode/issues/20) | Schema, lint, tests, secret scan, signed evidence metadata. |
| E01 | E01-S03 | [#21](https://github.com/emresoyleriz/EsCode/issues/21) | Layer dependency checks and ADR governance. |
| E02 | E02-S01 | [#22](https://github.com/emresoyleriz/EsCode/issues/22) | ACL, handshake, message limits, replay defense. |
| E02 | E02-S02 | [#23](https://github.com/emresoyleriz/EsCode/issues/23) | Version negotiation, sequence, snapshot/replay, backpressure. |
| E02 | E02-S03 | [#24](https://github.com/emresoyleriz/EsCode/issues/24) | Host restart, gap detection, user-facing error. |
| E03 | E03-S01 | [#25](https://github.com/emresoyleriz/EsCode/issues/25) | Windows primitives, negative tests, capability matrix. |
| E03 | E03-S02 | [#26](https://github.com/emresoyleriz/EsCode/issues/26) | Scope, args digest, expiry, execution-time recheck. |
| E03 | E03-S03 | [#27](https://github.com/emresoyleriz/EsCode/issues/27) | Child process cleanup, cancellation, resource limits. |
| E04 | E04-S01 | [#28](https://github.com/emresoyleriz/EsCode/issues/28) | WAL, single writer, schema migration, backups. |
| E04 | E04-S02 | [#29](https://github.com/emresoyleriz/EsCode/issues/29) | Durable intent, effect evidence, idempotency. |
| E04 | E04-S03 | [#30](https://github.com/emresoyleriz/EsCode/issues/30) | Kill-point matrix, unknown outcome, no duplicate side effects. |
| E05 | E05-S01 | [#31](https://github.com/emresoyleriz/EsCode/issues/31) | Workspace identity, file watchers, safe path handling. |
| E05 | E05-S02 | [#32](https://github.com/emresoyleriz/EsCode/issues/32) | Monaco buffer revisions, hashes, snapshot manifests. |
| E05 | E05-S03 | [#33](https://github.com/emresoyleriz/EsCode/issues/33) | Compare base revision, conflict UX, no silent overwrite. |
| E06 | E06-S01 | [#34](https://github.com/emresoyleriz/EsCode/issues/34) | main refresh, story branch, worktree ownership. |
| E06 | E06-S02 | [#35](https://github.com/emresoyleriz/EsCode/issues/35) | Commit/push/merge separate grants, protected branches. |
| E06 | E06-S03 | [#36](https://github.com/emresoyleriz/EsCode/issues/36) | Reviewable hunks, evidence, human approval. |
| E07 | E07-S01 | [#37](https://github.com/emresoyleriz/EsCode/issues/37) | Onboarding, recent projects, recovery entry. |
| E07 | E07-S02 | [#39](https://github.com/emresoyleriz/EsCode/issues/39) | Window manager, DPI, restore, layout snapshots |
| E07 | E07-S02 | [#38](https://github.com/emresoyleriz/EsCode/issues/38) | Window manager, DPI, restore, layout snapshots. |
| E07 | E07-S03 | [#40](https://github.com/emresoyleriz/EsCode/issues/40) | Midnight/Pearl tokens, accessible primitives, catalogue |
| E08 | E08-S01 | [#41](https://github.com/emresoyleriz/EsCode/issues/41) | Multi-tab, dirty buffer, command palette, keymaps |
| E08 | E08-S02 | [#42](https://github.com/emresoyleriz/EsCode/issues/42) | Separated user/agent terminals, scoped process |
| E08 | E08-S03 | [#43](https://github.com/emresoyleriz/EsCode/issues/43) | Review and apply with snapshot safety |
| E09 | E09-S01 | [#44](https://github.com/emresoyleriz/EsCode/issues/44) | OpenAI-compatible/local/cloud, secret refs |
| E09 | E09-S02 | [#45](https://github.com/emresoyleriz/EsCode/issues/45) | Tool/structured-output probe, fallback and override |
| E09 | E09-S03 | [#46](https://github.com/emresoyleriz/EsCode/issues/46) | Connection tests, capabilities, cost and context budget |
| E10 | E10-S01 | [#47](https://github.com/emresoyleriz/EsCode/issues/47) | State machine, budgets, event evidence |
| E10 | E10-S02 | [#48](https://github.com/emresoyleriz/EsCode/issues/48) | Plan preview, risk-scoped grants, user approval |
| E10 | E10-S03 | [#49](https://github.com/emresoyleriz/EsCode/issues/49) | Tool broker integration, test evidence, no false Completed |
| E11 | E11-S01 | [#50](https://github.com/emresoyleriz/EsCode/issues/50) | Story ID, branch, plan and approvals |
| E11 | E11-S02 | [#51](https://github.com/emresoyleriz/EsCode/issues/51) | Safe patch, test runner, independent review |
| E11 | E11-S03 | [#52](https://github.com/emresoyleriz/EsCode/issues/52) | User-controlled Git, crash/resume, failure UX |
| E12 | E12-S01 | [#53](https://github.com/emresoyleriz/EsCode/issues/53) | File watcher, hash, FTS, freshness |
| E12 | E12-S02 | [#54](https://github.com/emresoyleriz/EsCode/issues/54) | C#/.NET, Angular/React, Python, SQL adapters |
| E12 | E12-S03 | [#55](https://github.com/emresoyleriz/EsCode/issues/55) | Hybrid retrieval, context budget, stale index safety |
| E13 | E13-S01 | [#56](https://github.com/emresoyleriz/EsCode/issues/56) | DAG concurrency and ownership |
| E13 | E13-S02 | [#57](https://github.com/emresoyleriz/EsCode/issues/57) | Conflict analysis, combined result tests |
| E13 | E13-S03 | [#58](https://github.com/emresoyleriz/EsCode/issues/58) | Disagreement handling, reviewer evidence |
| E14 | E14-S01 | [#59](https://github.com/emresoyleriz/EsCode/issues/59) | Source trust, signature, compatibility, permission manifest |
| E14 | E14-S02 | [#60](https://github.com/emresoyleriz/EsCode/issues/60) | Tool results untrusted, isolation and grant checks |
| E14 | E14-S03 | [#61](https://github.com/emresoyleriz/EsCode/issues/61) | Isolated sessions, safe egress, screenshots/redaction |
| E15 | E15-S01 | [#62](https://github.com/emresoyleriz/EsCode/issues/62) | Progressive disclosure, event correlation |
| E15 | E15-S02 | [#63](https://github.com/emresoyleriz/EsCode/issues/63) | CPU/RAM/VRAM monitoring, concurrency and OOM |
| E15 | E15-S03 | [#64](https://github.com/emresoyleriz/EsCode/issues/64) | Redacted logs, export, opt-in telemetry |
| E16 | E16-S01 | [#65](https://github.com/emresoyleriz/EsCode/issues/65) | Snapshots, component states, no blanket updates |
| E16 | E16-S02 | [#66](https://github.com/emresoyleriz/EsCode/issues/66) | TR/EN, keyboard, screen reader, reduced motion |
| E16 | E16-S03 | [#67](https://github.com/emresoyleriz/EsCode/issues/67) | p95, FPS, DPI, layout restore benchmarks |
| E17 | E17-S01 | [#68](https://github.com/emresoyleriz/EsCode/issues/68) | Windows 11 x64, signing, dependency checks |
| E17 | E17-S02 | [#69](https://github.com/emresoyleriz/EsCode/issues/69) | Stable/Beta channels, drain/pause, rollback |
| E17 | E17-S03 | [#70](https://github.com/emresoyleriz/EsCode/issues/70) | Backup verification, migration rollback, restore tests |
| E18 | E18-S01 | [#71](https://github.com/emresoyleriz/EsCode/issues/71) | Threat model, attack suite, crash tests |
| E18 | E18-S02 | [#72](https://github.com/emresoyleriz/EsCode/issues/72) | Backtests, calibration, regression fixtures |
| E18 | E18-S03 | [#73](https://github.com/emresoyleriz/EsCode/issues/73) | MVP/Beta/Stable evidence, ADR and SBOM |

## Non-negotiable Definition of Done

1. Requirement, threats, negative cases and measurable acceptance criteria recorded.
2. Code integrated into actual desktop/host paths; no mock-only completion.
3. Unit, integration, contract, security, cancellation and recovery tests appropriate to risk pass.
4. Windows 11 interactive E2E and actual local/cloud provider tests where applicable.
5. Human UX/keyboard/accessibility evidence where UI changes.
6. Independent review and green CI attached to PR; main merge only by user.
7. Regression suite passes; feature flag, rollback and release notes updated.
8. No completion without evidence links and version/commit SHA.

## Coverage gaps needing explicit story amendments

- R16: inline autocomplete, inline edit, symbol navigation and debugging are not sufficiently explicit in current E08/E12 acceptance criteria.
- R17: context mentions, project rules, conversation compaction and context provenance need explicit E09/E10/E12 tests.
- R18: provider 429/5xx/stream interruption and token/cost observability need end-to-end provider fixture tests.
- R19: real PTY stdin/stdout/process tree cleanup, cancellation and agent-vs-user terminal separation.
- R20: cross-language golden projects, clean install, model capability mismatch and crash recovery E2E.

## Audit limitations

This is a planning and issue-text review, not a certified full source-code audit or executed Windows test report. Missing or partially imported V18 documents (#87) must be reconciled before declaring requirements complete. No percentage of working product is asserted.