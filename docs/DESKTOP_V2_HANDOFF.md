# EsCode — Desktop Foundation v2 (Engineer Handoff)

## Real implementation in this branch
- React-rendered shell (13 screens) and Monaco ESM editor with bundled local workers (offline-first).
- Electron v45 sandboxed desktop shell with minimal whitelisted preload capability API, custom `escode://` protocol and navigation deny-by-default.
- TR/EN dictionary and persisted non-secret UI preferences (versioned, validated localStorage).
- Docked and floating agent panel UI, resize and pin interactions. Native multi-window docking is **not yet implemented or validated**.
- Buffer content updated by Monaco remains in React memory and shows dirty state; no disk IO or .NET Host connection.
- Existing Windows named-pipe / policy / revision / retry technical spikes kept unchanged.

## Run (Windows 11 x64)

1. Install pinned Node LTS and .NET 10 SDK.
2. `npm install` (initial branch until lockfile generated); `npm test`; `npm run build`.
3. Web preview: `npm run dev` then open `http://127.0.0.1:5173`.
4. Desktop built preview: `npm run desktop`. For live Vite desktop view: start Vite then set `ESCODE_DEV_SERVER=1` in the Electron process environment.
5. `dotnet run --project src/host/EsCode.Host -- --ipc-spike` for the separate Windows IPC proof; not connected to the Electron app yet.

## Limitations
- Do not call this a production IDE; .NET Host, durable SQLite, tool broker, safe OS isolation and agent orchestration still not integrated.
- Electron shell security flags are tested syntactically; actual Windows renderer/IPC attack tests are pending.
- True docking across native windows, DPI/monitor restore and drag tests are still pending.
- No UX expert approval, WCAG acceptance, p95 load tests or production release.
- No production user preferences schema migrations beyond safe fallback.

## Definition of Done
A story is closed only after linked Issue acceptance, verified CI and UI visual evidence, reviewer approval and correct gate assessment. User controls commit/push/merge in product workflow. Never infer that an IPC spike is equivalent to a production security certificate.
