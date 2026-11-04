# EsCode — Desktop foundation scope (4 November 2026)

**Current state:** Electron + React + locally bundled Monaco UI foundation. **Not** a production, autonomous or complete agent IDE.

## Working foundation (code present; release tests still needed)

- Thirteen-screen navigation concept with original dark/light design tokens and TR/EN dictionary.
- Actual Monaco editor with in-memory editable buffers and local workers.
- Sandboxed Electron Main window, isolated preload bridge exposing read-only capabilities, custom escode:// protocol.
- Versioned non-secret preferences and layout settings, plus screenshot-smoke tooling.
- Fifteen draft JSON contracts and individual .NET Windows policy/IPC/revision/recovery spikes.
- Exact NPM dependency versions and committed package-lock.json; npm ci is already used by CI.

## Not yet delivered

- Read/write real workspace files, durable dirty-buffer recovery, completed docking/floating or full accessibility verification.
- Production authenticated Electron↔Host transport, secure Tool Broker, durable task state and SQLite journal.
- Live model chat, provider streaming, tool calling, autonomous coding, safe patch execution or real Git agent workflow.
- Signed Windows installer/updater, threat-tested OS sandbox, trusted release and human-approved UI acceptance.

## Historical correction

Older versions of this document described a vanilla DOM static-code preview and a missing package-lock.json. These claims became stale after the Electron/React/Monaco changes. Baseline UI/Windows smoke success is **not** production acceptance.

## Run and verify

```sh
npm ci
npm run check:syntax
npm test
npm run build
npm run desktop
```

Run the .NET Host separately for its limited Windows spikes; there is no connected agent transport yet. Follow [G0 runbook](G0_FOUNDATION_RUNBOOK.md) for release evidence.
