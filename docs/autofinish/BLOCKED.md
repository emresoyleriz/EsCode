# Outstanding work and blockers

## Environment checks

- .NET 10 SDK is absent (`dotnet: command not found`). Host build and Windows-specific tests were not executed here.
- Existing Unix socket IPC tests fail at `listen` with EPERM in this environment. Do not call the full suite green or weaken authentication to bypass this.
- Windows hardware/EDR/AppContainer, power-loss, mixed DPI and human acceptance remain in #125; hosted Windows tests must precede that gate.

## Publication authority

User explicitly authorized commit, push and PR publication on 7 November 2026. Work remains on review branches; no direct main merge.

## Remaining implementation

See TASKS.md and docs/planning/PACKAGE_PROGRESS_2026-11-04.md. In particular: actual sandbox worker/broker integration, authenticated workspace UI bridge, watcher/save/conflicts, real terminal/LSP/debugger, encrypted Host recovery UI, provider vault/streaming, supervised edit-test-review loop, MCP/browser execution, distribution/update/backup and release evidence remain. These are coding tasks, not solely local-PC validation.
