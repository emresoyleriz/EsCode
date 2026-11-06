# Verification — 7 November 2026

Environment: Linux, Node v24.22.1. Repository pins Node 22; this does not replace a clean pinned Windows CI run. Base commit: 76199fdec905dda4736f15962800edb63240a432.

| Command | Actual result |
| --- | --- |
| `npm ci --ignore-scripts --fetch-retries=0 --fetch-timeout=20000` | PASS; 43 packages installed. Ignore scripts avoids downloading/running Electron during bootstrap. |
| `node scripts/patch-monaco-dompurify.mjs` | PASS; bundled Monaco sanitizer redirected to patched DOMPurify 3.4.17. |
| `node --test tests/buffer-recovery.test.js tests/preferences.test.js tests/editor-tabs.test.js tests/inline-edit.test.js` | PASS; 18 tests, zero failures. |
| `npm run check:syntax` | PASS; 31 JS/MJS/CJS files. JSX checked by Vite build. |
| `npm run build` | PASS; 1245 modules. Existing large-bundle warning remains. |
| `npm test` | NOT GREEN; 67 tests, 56 pass, 11 fail. Existing Unix socket IPC/model tests fail during server.listen with EPERM. No authentication checks bypassed. |
| `npm run visual` with same-process Vite server | PASS after installing Chromium; 52 screenshots, 13 screens, TR/EN, dark/light, four desktop viewport sizes, palette keyboard flow, persisted preferences and floating panel. |
| Browser recovery/Monaco regression in visual script | PASS; rapid typed marker survives checkpoint, closed dirty tab is retained, reload/restore succeeds, delete removes only stored copy. Same-file selection + inactive-tab close retains inline editor handle; inline replacement and Monaco undo pass. |
| `git diff --check` | PASS. |
| .NET/Windows/installer tests | NOT RUN; SDK/Windows unavailable. |

## Defects exposed by the browser run

Initial rapid-typing regression failed: the stored text contained a truncated marker. The active controlled Monaco `value` could reapply stale React text. The implementation now uses Monaco's own active model (`defaultValue` on mount), with React tracking snapshots. Inline edits go through Monaco's undoable edit API. The complete browser test passed after correction.

The new recovery controls were visually inspected in dark TR and light EN screenshots. Disabled restore is visually distinct; buttons wrap without hiding text. Screenshots are smoke evidence, not an approved reference baseline or WCAG/human acceptance signoff.

Raw local logs and screenshots are under ignored `artifacts/verification/` and `artifacts/visual/`; no secrets are logged by the feature. Re-run the commands in CI after publication. Independent review remains outstanding.

## Floating panel recovery batch

`node --test tests/panel-layout.test.js tests/preferences.test.js`: 9 passed. Production build passed. Browser smoke with additional persisted off-screen geometry and resize assertions passed, including all previous editor regressions. PR #132 hosted CI: ui-foundation, dependency-audit, windows-ui-build and visual-screenshots successful when checked; windows-ipc-spike still running. Native multimonitor acceptance and independent review remain outstanding.
