# EsCode — AI Development Studio

**Prototype status:** premium UI foundation + typed contract schemas + Windows IPC technical spike. This repository is **NOT** yet a complete IDE or a secure autonomous agent product.

## Interactive premium UI

The current implementation is a runnable browser-based visual prototype of the planned Electron+React desktop app: 13 screens, Midnight Glass / Pearl Light, density profiles, agent dock, code/terminal preview, model and recovery states, command palette (`Ctrl+K`). Actual Electron, React, Monaco and .NET integration remain future tasks.

```sh
npm install
npm test
npm run dev
```

The development server prints a localhost URL. No model, file, Git or OS tools are invoked by the prototype.

## Windows IPC proof

Prerequisite: Windows 11 x64 and .NET 10 SDK.

```powershell
dotnet build src/host/EsCode.Host/EsCode.Host.csproj -c Release
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --ipc-spike
```

This tests bounded same-user Named Pipe + token handshake only; it does **not** complete the P0 security gate.

## Planning and architecture

- [Issue planning mirror](planning/EPICS_AND_STORIES.md): 18 Epic, 54 Story, 270 tasks.
- [Architecture](docs/architecture/BASELINE.md) and [15 service schemas](contracts/README.md).
- [Premium UI audit](docs/design/UI_AUDIT_V19.md) and [13-screen acceptance gate](https://github.com/emresoyleriz/EsCode/issues/81).
- [.AI rules](.AI/START.md) and [workflow](.AI/WORKFLOW.md).
- [Draft UI foundation PR](https://github.com/emresoyleriz/EsCode/pull/82).

The full historic V18/V19 ZIP artifacts are separate ChatGPT deliverables and have **not** been completely synchronized to GitHub. In-repository planning data is a GitHub Issue mirror, not a byte-for-byte copy of the original archive.

## Git process and safety

Default branch: `main`; feature branches and PRs for delivery. Human retains commit, push and merge control in the product. P0 IPC, sandbox, recovery and buffer/worktree consistency must be verified before allowing autonomous tooling.

## Verification

GitHub Actions checks `npm test`, `npm run build` and a Windows .NET IPC spike. Consult the [Actions page](https://github.com/emresoyleriz/EsCode/actions) for actual runs. Do not equate a passing UI build with a complete security, UX or production release gate.

**Project delivery state:** In progress — no Epic has met its full Definition of Done yet.
