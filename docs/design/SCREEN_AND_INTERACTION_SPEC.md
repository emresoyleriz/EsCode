# EsCode — Screen and Interaction Contract

Version: 1.0 draft; design specification only.

## Global shell
Command Palette global, omnipresent progress indicator, status bar, account/workspace context, discoverable keyboard shortcuts. Docking/floating panels save versioned layout state. User pin overrides suggestions. React owns layout state; Electron Main owns native window creation and boundaries. All process execution passes policy/tool broker.

## Screen inventory and key journeys
| Screen | Primary affordance | Required states | Blocking interaction |
| --- | --- | --- | --- |
| Welcome Hub | Recent workspace, open/create, recovery | empty/loading/error/recovering | Explicit project trust decision |
| IDE | Explorer, Monaco, search, terminal, agent dock | unsaved/dirty/readonly/indexing/offline | Patch apply checks buffer revision |
| Agent Studio | Chat, plan, specialist map, evidence | queued/running/approval/failed/unknown/done | Risk and grant scope shown before tools |
| Task Center | DAG, queue, timeline, pause/resume | waiting/paused/reconciling/retrying/finished | Unknown side effects never silently retried |
| Code Intelligence | Search, symbol graph, impact | indexing/stale/not found/partial | Source and freshness visible |
| Git & Review | Worktrees, staged diff, tests | conflict/protected/review-needed | Commit/push/merge separately permissioned |
| Approval Center | Approval queue, risk, expiry | pending/expired/revoked/denied | Must show actor/resource/args digest |
| Model Studio | Provider profiles, probes, routing | connected/unverified/degraded/unavailable | Probe and override distinguished |
| MCP Manager | Source trust, permissions, disable | validating/untrusted/revoked | Manifest grants preview before enabling |
| Terminal/Debugger | Session controls, output/trace | running/exit/crash/disconnected | User/agent shells clearly separated |
| Browser | Isolated navigation and evidence | loading/blocked/offline/error | Browser actions scope restricted |
| Settings | Theme, fonts, density, languages | preview/dirty/invalid/rollback | Import size/schema validation |
| Recovery | Checkpoints, journal, reconcile | interrupted/unknown/recovered | Clear consequences of restore or retry |

## Layout profiles
Compact favors keyboard access and collapsible inspectors; Comfortable is balanced; Focus centers editor/task. Responsive layout must never hide approvals or irreversible risks. Minimum usable viewport and collision behavior to be measured and approved.

## Interaction guarantees
Keyboard-first primary workflows; visible focus; dialog focus trap and return; cancellation semantics; undo where safe; destructive confirmation; recovery status preserved across window changes; notifications never imply backend success without verification.

## Visual vocabulary
Original branding and icons, clear hierarchy and restrained glass. Use opaqueness for code, terminal, logs and diff. Light/dark/high-contrast states. Motion duration targets 120–220ms and supports reduced motion.

## Evidence required before acceptance
Approved mockups, annotations, keyboard & screen-reader audit, screenshot test baseline, interaction contract tests, latency benchmarks on pinned Windows 11 x64 host, mixed DPI/multimonitor tests and review sign-off.
