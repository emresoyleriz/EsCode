# EsCode UI Foundation — Honest Scope

This folder implements an **interactive, browser-based high-fidelity UI prototype**, not the completed AI Development Studio application.

## Working
- Thirteen primary screens, navigable sidebar, command palette (Ctrl+K), theme/density switching.
- Interactive IDE preview (file selection and terminal visibility), agent dock pin/close, saved layout JSON export as client-side copy, version validation.
- Model and risk states represented accurately as **not connected** or **unverified**; no false success messages.
- Automated Node state/reducer tests via `npm test`.

## Not implemented
- Electron Main, .NET host, authenticated IPC, operating-system sandbox, file writes, live Monaco, LLM integration, MCP, Git, debugger and agent execution.
- Real docking drag-and-drop, crash recovery, screen-reader/keyboard full E2E, visual snapshots, performance certification.
- Production brand asset approval or cross-platform packaging.

## Run
1. Install Node and npm; run `npm install`.
2. Run `npm test` for state tests.
3. Run `npm run dev` and open the printed localhost URL.

Do not close P0/security gates, or the UI-GAP and UI-GATE issues, until evidence is attached and reviewed. This branch is intended for a review PR, not an automatic merge.
