# EsCode — Current repository status (review snapshot, 4 November 2026)

## Actual implementation on main

- React 19 and Monaco ESM editor are present, with in-memory buffers only; they do not write workspace files.
- Electron 45 sandboxed desktop shell, context-isolated preload and limited read-only capability query are present. They are **not** a live .NET Host transport.
- .NET 10 code contains bounded named-pipe handshake, policy, revision and uncertain-outcome spikes; none is a production Agent Runtime.
- The dependency versions and package-lock.json are present; previous statements that the lockfile is missing are obsolete.
- TR/EN UI and preference scaffolding exist; full 13-screen design and production UX are not accepted.
- GitHub issues contain 18 Epics, **55 Story issue records but 54 unique Story IDs** because E07-S02 appears in #38 and #39.

## Existing historical evidence

- Baseline GitHub Actions run 40899370170 completed successfully for the earlier project snapshot; that does **not** validate all current development or release gates.
- Draft PR #90 contains a CLI provider model discovery diagnostic only; it does not constitute a complete agent or API integration.
- Draft PR #91 contains proposed requirement traceability and phased execution planning; not merged into main.

## Explicitly missing or unverified

- Authenticated production Electron ↔ Host connection, durable SQLite state, OS sandbox and Tool Broker, workspace save/patch, Git worktrees and real LLM chat/agent execution.
- End-to-end Windows GUI installation/testing, live model streaming/tool calling, independent review, recovery, branch rule enforcement and security certification.
- Full original V18 archive manifest and byte-for-byte import; see #87.
- Branch protection: public rulesets returned `[]`; protection endpoint returned 403 to the integration on 2026-11-04. This is **not confirmation** that main is protected.

## Reporting policy

Only claim a test pass when the exact commit, machine, command and run link exist. A draft PR or added test is not completed acceptance. Do not close #19/#20/#21/#86/#87 until verified conditions are met.

See [G0 runbook](G0_FOUNDATION_RUNBOOK.md), [technical gates](TECHNICAL_GATES_V20.md) and [README](../README.md).
