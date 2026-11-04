# G4.3 — Monaco multi-tab in-memory workflow (partial)

The existing Monaco editor now has multiple open tabs, selection, close controls and keyboard-focusable buttons. Closing a tab does not discard its in-memory edited buffer; reopening restores that buffer. Dirty-file indicators appear per tab. The simulated terminal no longer shows a green check mark implying `dotnet test` actually executed.

Pure Node tests cover tab opening, closing and state preservation. CI retains JSX bundling, navigation smoke and screenshots.

**Not complete:** real disk-backed save, LSP/diagnostics, PTY, debugger, terminal process lifecycle, keyboard shortcuts and full editor accessibility. Do not claim any real build, test, debug or source save occurred from the UI. These require authenticated Host APIs and user approval. E08-S01/S02/S03 remain open.
