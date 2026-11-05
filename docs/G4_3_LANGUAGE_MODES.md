# G4.3 — Monaco file-language recognition (partial)

Monaco now chooses its in-browser language mode based on source filename rather than treating every non-JSON file as C#. Supported source extensions include C#, JS/TS/JSX/TSX, JSON, Python, SQL, Markdown, YAML, HTML, CSS and PowerShell. Unknown files remain plaintext.

This is a **browser-only highlighting and built-in language mode improvement**, not an LSP runtime or process launcher. It does not write files, execute terminals, grant agent tools, or install debugger adapters. Real project editor integration, PTY, LSP, diagnostics, debugger and cancelable builds still require the G2.1 sandbox and G4.1 revision-safe workspace bridge.
