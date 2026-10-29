# EsCode — UI Foundation Scope (Post-Merge)

**Implementation stage:** browser-based interactive UI prototype, not the production Electron+React+Monaco desktop application.

## Demonstrated in main
- 13 navigable screen surfaces, dark/light themes, density profiles and command palette.
- Basic client-side panel toggles, settings UI and visual code preview.
- 15 draft JSON schemas and structural tests.
- GitHub Actions visual smoke run captures 26 light/dark screenshots and verifies navigation.
- Limited Windows .NET named pipe, approval scope, revision and retry guard smoke checks.
- Main CI: https://github.com/emresoyleriz/EsCode/actions/runs/40899370170 — all three jobs successful.

## Missing production behavior
- Real Electron, React, Monaco, LSP, debugging, OS commands and agent execution.
- Persisted layout and preferences; full translations (locale dropdown is not i18n).
- Actual docking/drag/floating, snapshot restoration and authentic file selection behavior (displayed code is static).
- Full keyboard/focus/assistive-tech usability, responsive/high-DPI Windows multi-monitor acceptance, human-approved visual references and measured p95 performance.
- Robust client and backend authentication, sandbox adversarial tests, durable storage recovery, Git and model integrations.
- Reproducible package-lock / npm ci and complete V18 documentation import.

## Run the existing preview

```sh
npm install
npm test
npm run dev
```

Use GitHub Actions for CI evidence. Tests that pass prove the explicitly tested subset; they do not certify production architecture or release quality.

## Tracking
- [Merged PR #82](https://github.com/emresoyleriz/EsCode/pull/82)
- [UI-GATE #81](https://github.com/emresoyleriz/EsCode/issues/81)
- [Post-merge audit issues #83–#87](https://github.com/emresoyleriz/EsCode/issues?q=is%3Aissue+is%3Aopen+AUDIT)
