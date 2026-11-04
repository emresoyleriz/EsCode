# EsCode — G0 Foundation implementation and verification

**Status:** Proposed implementation for CI validation and independent review, **not** evidence that G0 has passed.

## Implemented in this branch

- Preserve Electron Main/React/Monaco/.NET 10 Host boundaries; no new runtime or tool privileges.
- Keep the existing exact NPM dependency versions and package-lock.json; reject dependency drift in an automated test.
- Add a documented .NET 10 SDK baseline via global.json (minimum 10.0.100 with latestFeature roll-forward).
- Configure npm engine-strict and save-exact; expand ignored generated Windows/.NET files.
- Add dependency/architecture/CI-regression tests and JS syntax checks (JSX is verified by the production build).
- Run CI on all PRs, all pushes to main, and manual dispatch; add Windows Node install/test/build and production dependency audit.
- Correct outdated claims in docs/PROJECT_STATUS.md and docs/PROTOTYPE_SCOPE.md.

## Required verification (do not mark complete without actual run links)

On a **clean Windows 11 x64** machine with Node 22.23.4, .NET 10 SDK and Git:

```powershell
npm ci --no-audit --no-fund
npm run check:syntax
npm run check:foundation
npm test
npm run build
dotnet --info
dotnet build .\src\host\EsCode.Host\EsCode.Host.csproj -c Release
dotnet run --project .\src\host\EsCode.Host\EsCode.Host.csproj -c Release --no-build -- --ipc-spike
dotnet run --project .\src\host\EsCode.Host\EsCode.Host.csproj -c Release --no-build -- --policy-spike
dotnet run --project .\src\host\EsCode.Host\EsCode.Host.csproj -c Release --no-build -- --revision-spike
dotnet run --project .\src\host\EsCode.Host\EsCode.Host.csproj -c Release --no-build -- --recovery-spike
```

## Blocking owner/verification work

1. GitHub `/rulesets` returned an empty list and branch protection GET returned 403 to the integration on 2026-11-04. The owner must confirm required checks, review and no direct-main writes in Settings → Branches/Rulesets. CI alone **does not protect main**.
2. Full V18 archive and source manifest are not in the GitHub checkout; issue #87 remains open. Do not invent missing documents or claim exact reconstruction.
3. Duplicate E07-S02 story issue #38/#39 needs canonical ownership and linked duplicate disposition; historical data should remain intact.
4. GitHub Actions Windows tests, npm audit and visual screenshots must run against this exact commit and the actual logs must be reviewed.
5. Node syntax checks are **not** ESLint and visual screenshots are **not** human UI acceptance. Dedicated lint/secret-scanning and signed release provenance remain release work.

## Acceptance evidence ledger

| Gate | Result in this file | Required evidence |
|---|---|---|
| Exact dependency/lock consistency | Automated test added; awaiting PR CI | Passing Node test run URL |
| Windows JavaScript and .NET build | Workflow added; awaiting PR CI | Both job links, OS, SDK and commit |
| Security baseline | Existing Windows spike commands retained; no OS sandbox certification | Passing negative tests and P0 reviews |
| Branch protection | **Unverified, blocking G0** | Owner screenshot/configuration and required checks |
| Full V18 archival fidelity | **Unverified, blocking archival audit #87** | Original archive manifest and hash comparison |

Next engineering gate after G0 review: authenticated Electron ↔ .NET IPC integration (E02-S01), without granting filesystem or shell tools.
