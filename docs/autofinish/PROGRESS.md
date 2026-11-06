# EsCode execution progress — 7 November 2026

Base: `76199fdec905dda4736f15962800edb63240a432` (main). Work branch: `work/32-buffer-recovery`.

## Implemented locally — E05-S02 #32, partial prototype slice

- Explicit sample-editor checkpoint creation, restoration and deletion in Recovery; never automatic code persistence.
- Versioned, bounded local record with exact sample-baseline matching and tab/selection validation.
- Preserves closed dirty buffers, empty files and tab order. Restores all-or-nothing; conflicting current unsaved edits are never overwritten.
- Storage denial, corruption, wrong sample version and quota failure keep current editor state intact.
- TR/EN states, semantic theme styles, disabled restore state and unsaved-window close warning.
- Cross-window storage changes refresh available recovery state.
- Unit coverage and a browser recovery journey added to existing visual smoke script.

This is an explicitly labeled, unencrypted **sample workspace** copy. It does not implement Host DPAPI checkpoint integration, real workspace identities, durable automatic crash recovery or actual file saves. #32 stays open.

## Editor defects fixed while verifying recovery — #41 related

- Rapid typing could lose characters when React reapplied a stale controlled Monaco value. Monaco now owns its active text model; React retains buffer snapshots for tab switches and explicit checkpoint actions.
- Inline edits use Monaco executeEdits with undo stops and validate against the live model, preserving undo.
- Reselecting the active file or closing an inactive tab no longer clears the editor ref.
- Browser verification exercises rapid typing, inactive-tab close, same-file selection, inline edit and undo as well as checkpoint persistence.

## Repository verification

- Authenticated repository read confirmed access to emresoyleriz/EsCode.
- Main tree, open issues and open PRs examined; PR #131 contains planning only.
- 86 open issue records captured in TASKS.md, including parent epics; this is not a count of independent coding tasks.
- Existing package ledger still lists implementation gaps in every major release area. Those gaps were not moved into the local-PC gate #125.
- Recovery/editor package published as PR #132, commit 3e68267d10f7a26289e5ed4b07269e087ee9fb7d. No merge, issue closure or release publication.

## Floating panel recovery — #85 / #38 partial slice

Persisted off-screen coordinates and viewport changes now clamp the full floating panel width and height into the visible viewport. Pinning still blocks user drag but does not leave the panel inaccessible after display changes. Pure geometry tests cover large, negative, invalid and tiny viewport cases. Browser smoke passed for saved coordinates (9000,9000), width 640, then 1366×768 and 980×620 resize; existing recovery/inline regressions also pass. Real mixed-DPI/multimonitor native acceptance remains open.
