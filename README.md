# EsCode — AI Development Studio

**Engineering status:** In-development desktop UI foundation. The product is **not** a finished agent IDE and production security gates are **not** certified.

## Approved product
- Premium original Apple-inspired (not copied) Windows 11 x64 development environment.
- Electron + React + locally bundled Monaco editor; .NET 10 Host / secured IPC, risk-scoped Tool Broker, SQLite journal and multi-agent execution are target architecture.
- 13-screen shell, dark/light semantic tokens, density profiles, TR/EN interface, dockable/floating agent preview.
- MVP permissions: Read-only / Ask / Supervised. Autonomous disabled pending P0 security acceptance.
- MVP vertical slice: Project → Story → Plan → Edit → Test → Review → human-controlled Git.

## What's actually in this branch
- React 19 interface and Monaco ESM editor with local workers; editable in-memory buffers, *not* filesystem persistence.
- Electron 45 sandboxed shell, minimal context-isolated preload API and custom escode://app/ protocol. No live .NET tool or agent bridge yet.
- Versioned non-secret preferences and layout state; selected theme/density/locale/panel mode persisted safely.
- Theme CSS custom properties generated from design/tokens.json.
- 15 draft JSON Schema service contracts and separate Windows IPC/policy/revision/retry spikes.
- 18 Epic + 54 Story + 270 checklist task mirror and .AI coding agent rules.

## Developer handoff
[V20 Software Engineering Handoff](docs/DEVELOPER_HANDOFF_V20.md) · [Technical Acceptance Gates](docs/TECHNICAL_GATES_V20.md) · [Premium UI Audit](docs/design/UI_AUDIT_V19.md) · [UI-GATE #81](https://github.com/emresoyleriz/EsCode/issues/81) · [Issues](https://github.com/emresoyleriz/EsCode/issues).

## Windows development
Node 22 LTS (see .node-version), .NET 10 SDK, npm and Git are required.

    npm ci
    npm test
    npm run build
    npm run dev
    # In another terminal after build:
    npm run desktop
    dotnet build ./src/host/EsCode.Host/EsCode.Host.csproj -c Release
    dotnet run --project ./src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --ipc-spike

The built desktop shell opens the UI preview. The UI does not write actual project files, commit code or execute agents. Browser preview is localhost-only.

## CI evidence
GitHub Actions tests Node state/contracts, UI build and Playwright screenshots as well as Windows .NET IPC/policy/revision/recovery spikes. The smoke tests are **not** equivalent to secure production approval, human UX signoff, crash-safe journal or Windows installer acceptance.

## Release discipline
Every Story needs verified positive and negative tests, relevant UX design approval and reviewer evidence. Commit/push/merge controls remain with the user. Main-branch protection requires repository owner confirmation in GitHub Settings. The original V18/V19 archive documentation is distributed separately and remains pending full GitHub import.
