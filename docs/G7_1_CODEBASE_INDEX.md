# G7.1 — Read-only Codebase Intelligence / FTS5 index (partial)

This package introduces a **trusted-Host-only** code search slice using the existing SQLite dependency. It is not a model tool, arbitrary filesystem IPC endpoint, or permission to disclose code to an LLM.

- Incremental FTS5 lexical indexing on explicitly selected workspace (C#, TypeScript, JavaScript, Python and SQL).
- Uses G4.1 WorkspaceReader: path traversal, junction/symlink, .git, .env and binary protections remain active.
- An additional conservative filename filter excludes secret/password/credential/private-key names.
- Refuses to index more than 511 visible paths, 128 KiB per file or 8 MiB total; **fails closed rather than silently claiming a complete index**.
- Persists content digests in a separate SQLite file outside the workspace and updates index atomically.
- Search accepts at most four plain terms and ten results by default (maximum twenty).
- Before returning any excerpt, verifies the live file digest. Stale hits are suppressed.
- No tokens, credentials, source snippets or model prompts are logged by this service.

### Windows test

```powershell
dotnet build src/host/EsCode.Host/EsCode.Host.csproj -c Release
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --code-index-spike
```

### Development CLI (trusted operator only)

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --code-index --workspace C:\Dev\MyRepo --database C:\Users\me\AppData\Local\EsCode\index.sqlite3 --query CustomerLedger
```

**Remaining G7.1 acceptance:** production workspace identity and multi-root; watcher-driven refresh; freshness and stale-index performance benchmarks; FTS relevance/ranking and symbol graph; content safety classification before model context; context budget and search UI; larger repository pagination; Windows adversarial filesystem tests. No agent reads this index automatically. Security gate G2.1 remains OPEN.

## G7.1 persisted lexical symbols and bounded context

`CodeSymbolIndex` builds a **separate read-only source-code symbol database**
for declared workspace directories, supporting common C# class/record/etc.,
TypeScript/JavaScript function/type/class, Python def/class, and SQL
CREATE PROCEDURE/FUNCTION/VIEW declarations. This is lexical extraction, **not
a compiler, semantic symbol graph or LSP**.

The index excludes secret-like filenames, symlinks and blocked directories via
the trusted WorkspaceReader, and caps work to 511 files, 128 KiB/file,
8 MiB cumulative content and 64 symbols/file. SQLite updates are
transactional and incremental. Results are checked against live SHA-256 file
digests before display, preventing stale source from being returned after an
external edit. A context preview extracts bounded nearby lines marked
`UntrustedSource=true`; no context is automatically sent to an LLM.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --symbol-index-spike
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --symbol-index --workspace C:\Dev\Repo --database C:\Users\me\AppData\Local\EsCode\symbols.sqlite3 --symbol CustomerLedger
```

**Remaining G7.1:** real language servers/symbol graph and cross-file references,
workspace watcher invalidation, ranking benchmarks, multi-root identity,
conservative data loss prevention/classification, UI search navigation and
actual context token counting.
