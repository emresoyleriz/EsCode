# EsCode — Acceptance Gates (Binding)
This file is normative for release gate tracking; no claim is made that the pending checks have passed.

## Gate G0 — Engineering reproducibility
- Exact package.json versions, audited package-lock.json and `npm ci` pass on clean Windows/CI.
- .NET 10 SDK version verified; signed artifacts and supply chain controls for Stable.
- `main` has confirmed enforced PR review + required CI.

## Gate G1 — Windows OS isolation (P0)
- Test under dedicated Win11 x64 VM and distinct user tokens.
- Explicit deny tests for unauthorized files, symlinks/junctions, processes, network egress, elevation, child process and MCP.
- Insufficient OS capabilities cause operation to be blocked, never downgraded silently.

## Gate G2 — Authenticated IPC (P0)
- Named Pipe ACL per user/session, client identity verification and guarded capability version handshake.
- Negative tests for another user/process, malformed oversized frame, replay, reconnection, event sequencing and crash.
- Renderer may call only typed preload APIs; no unrestricted OS bridges.

## Gate G3 — Durable recovery (P0)
- SQLite single-writer, WAL/backup/migrations, journaling intent and observing effects.
- Kill app/worker during each side effect; restarts reconcile OutcomeUnknown and never duplicate non-idempotent operations.

## Gate G4 — Workspace correctness (P0)
- Atomic patch checks base snapshot + Git ref + file hash + unsaved Monaco buffer revision.
- Parallel worktree conflict and rollback tests; clear human resolution and no silently lost changes.

## Gate G5 — Premium UI (release blocking)
- All 13 screens: approved original light/dark mockup, focus/keyboard/screen-reader, loading/error/offline, TR/EN, multi-monitor/mixed DPI.
- No hidden approvals or false success statuses; human signs off screenshot differences.
- p95 input and navigation benchmarks measured under realistic indexing/model load.

## Gate G6 — MVP user story
Project → Story → Plan → Approve → Edit → Test → Review → user-controlled Git succeeds with security tests. Read-only/Ask/Supervised only.

## Gate G7 — Stable
Threat review, signed installer, recovery backup/restore, privacy, SBOM, update rollback, documentation and support/compat matrix verified.

Each gate requires a run URL, environment, date, owner, actual log/artifact, negative-case coverage and reviewer. A passed small smoke test does not complete the higher gate.
