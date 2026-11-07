# EsCode — Autofinish execution backlog

Snapshot: 2026-11-07. This is a planning/evidence baseline, **not** a claim that features or tests have passed.

## Execution order

| Priority | Work package | GitHub evidence | Exit criteria | State |
| --- | --- | --- | --- | --- |
| P0 | Reconcile baseline and historical source inventory | #87 | File manifest and verified historical source comparison | BLOCKED: original V18 archive not in repository |
| P0 | Reproducible CI and main protections | #86 | npm ci, Node/.NET Windows CI and owner-confirmed branch rules | PARTIAL: lockfile and pinned packages exist; rules unverified |
| P0 | Electron↔Host authenticated IPC and broker boundary | #83 and architecture gates | Negative IPC/auth tests, least-privilege read-only transport | TODO |
| P0 | Workspace filesystem and buffer recovery | #85 | Conflict-safe save, revision checks, recovery tests | TODO |
| P0 | Sandboxed execution and durable state | #125 | Windows OS sandbox negative tests and crash-safe journal | TODO; final hardware gate remains blocked |
| P1 | Git workflow and user-controlled review | #81 | Branch/diff/commit flow with explicit user approval | TODO |
| P1 | Model providers and agent execution | #81 | Provider capability tests, bounded tool permissions, reproducible review | TODO |
| P1 | Full UI, docking, TR/EN and accessibility | #74–#85 | 13-screen functional and visual acceptance | TODO |
| P0 release | Security, chaos, installer and go/no-go | #71–#73, #125 | Independent evidence and Windows local signoff | BLOCKED until implementation and local tests |

## Rules

1. Implement and test in dependency order; don't close issues on code presence alone.
2. Do not enable autonomous execution before Windows sandbox security gates pass.
3. Keep main untouched; use reviewable branches and PRs.
4. Log test command, commit SHA, environment, result and known limitations for every completed work package.
5. Preserve #125 as the final local-PC acceptance gate; do not shift implementable coding tasks there.

## Session evidence

- GitHub issue inventory queried on 2026-11-07; 86 open issues displayed by repository web view, no open PR returned by connected GitHub search.
- Read current docs/PROJECT_STATUS.md, docs/PROTOTYPE_SCOPE.md and package.json from main.
- Source clone unavailable in execution container (DNS resolution of github.com failed); therefore no local build/test pass is claimed.
