# EsCode — AI Development Studio

Premium AI-first Windows developer environment (architecture and development plan). **Application code is not yet implemented; technical security and recovery tests have not been executed.**

## Product scope
- Windows 11 x64 primary; Windows 10/ARM64 conditional compatibility.
- Electron + React + Monaco, .NET Host, authenticated Named Pipes, SQLite, isolated workers.
- Original Apple-inspired premium Windows UI: Midnight Glass/Pearl Light, dock/floating agent panels, accessible primitives.
- MVP: Project → Story → Plan → Edit → Test → Review → human-controlled Git.
- MVP permissions: Read-only, Ask, Supervised; autonomous only after P0 security gates.

## GitHub work board
- **18 Epic Issues:** [Epic list](https://github.com/emresoyleriz/EsCode/issues?q=is%3Aissue+is%3Aopen+in%3Atitle+EPIC)
- **54 Story Issues**, with **5 checklist TODOs each** (270 checklist items).
- **7 UI audit gap Issues**, required for design completeness.
- Duplicate Story issue #39 was closed; #38 remains authoritative.

## UI is a release-critical workstream
Read [Premium UI Audit](docs/design/UI_AUDIT_V19.md), [Screen and Interaction Specification](docs/design/SCREEN_AND_INTERACTION_SPEC.md) and [.AI UI Agent Rules](.AI/UI_AGENT_RULES.md). UI design has **not** passed a full screen-by-screen high-fidelity, accessibility or performance certification yet.

## Important delivery status
The comprehensive V18 local documentation ZIP was generated separately. **It has not yet been fully imported into this GitHub repository**; the repo currently contains the initialization, UI audit and agent design guidance. GitHub Issues capture the work plan, but detailed V18 source files and schema contracts must still be synced. Do not assume that V18 artifacts already exist at paths mentioned in Issue text.

## Start order
1. Verify Windows 11 x64 toolchain and isolated test identity/VM.
2. Verify secure IPC, sandbox, crash recovery, and revision-consistent patching.
3. Build core vertical flow and premium UI with reviewed design references.
4. Evaluate remaining Beta/Stable features via documented evidence gates.

Commit/push/merge and branch protection remain user-controlled; protection configuration has not been verified.
