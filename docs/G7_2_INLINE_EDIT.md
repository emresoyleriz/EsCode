# G7.2 — Local Inline Edit with preview and revision guard (partial)

This slice adds a **manual, entirely in-memory** Monaco Inline Edit flow. Select code in the local editor, type a replacement, review original vs proposed text, and explicitly accept or cancel. The code checks the whole source-buffer snapshot before applying; if the buffer changed since selection, the proposal is rejected. It does not write real workspace files, call an LLM, or grant tool permissions.

## Tests and scope

- Node unit tests cover replace, insert, delete, invalid selection, oversized payload and stale-buffer denial.
- UI adds localized TR/EN accessible labels and a visible edit preview in the IDE.
- Existing Monaco edit buffer remains dirty after acceptance; no save-to-disk claim.
- G2.1 security gate and all OS tool restrictions remain unchanged.

**Still needed for full G7.2:** AI-powered Inline Edit, actual LSP/rename/refactoring, code completion, symbol navigation, diagnostics, debugger, revision-safe disk patch approval and keyboard/assistive-technology E2E. Do not close the Cursor-like code features Story based on this local prototype.
