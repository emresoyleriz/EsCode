# G4.1 — Read-only real workspace access (first slice)

Implements real on-disk workspace listing and UTF-8 text snapshots in the trusted .NET Host code path. All paths must be relative to an existing absolute root; encoded or absolute paths, traversal, symlink/junction components, .git directories, .env secrets and binaries are denied. Reads are capped at 1 MiB per file; listing at 1,024 entries. Returned snapshots include a SHA-256 digest.

The trusted Host now also includes a **bounded optimistic text replacement primitive** using expected SHA-256, a same-directory staged file and atomic File.Replace. It refuses stale snapshots in tests. **Do not expose this primitive to renderer or agent yet:** unsaved Monaco buffer revision coordination, Git base identity and race-free OS handle checks are not implemented. This is not an authorized production patch executor. These are G4.1/G4.3/G6 follow-up work. Some Windows reparse/TOCTOU threat tests still require a hostile VM.

Run:

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --workspace-read-spike
```

Keep E05-S01/S02/S03 open until true read/write, buffer revisions and atomic patch tests succeed.

## G4.1 Multi-root session and conflict-detection slice

The trusted .NET Host can register up to eight **separate, explicitly selected absolute directory roots** under opaque session IDs. Each session uses the existing WorkspaceReader path and reparse-point protections. File reads and candidate lists are scoped to the registered root.

`IsCurrent(sessionId, relativePath, sha256)` re-opens and hashes the actual file at the time of validation, rather than trusting a possibly missed watcher event. The CLI smoke proves different roots with identical filenames do not leak or mix content, edits in another process invalidate only the affected revision, traversal and secret segments are denied, and closed roots stop accepting reads.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --workspace-sessions-spike
```

**Security status:** Host-only read access. There is no renderer-controlled root registration, filesystem write grant, approved patch UI, atomic Windows handle identity proof, multi-root watcher or secure agent patch execution. G2.1 remains a prerequisite before letting untrusted code use file tools.

## G4.1 — Trusted approved disk patch with content binding

`ApprovedWorkspacePatch` binds every proposed edit to an immutable SHA-256
digest of the **session ID, relative file path, expected source file revision
and full replacement text**. Only the trusted Host's supervised
`ToolPolicyBroker` can mint a matching single-use, short-lived grant after
human approval. On apply, the grant must match exactly; it is consumed even
if a filesystem error or revision conflict follows. No automatic replay.

The Windows smoke uses real disk files in two isolated roots and proves:
no grant ⇒ no write; content/actor tampering ⇒ denied and grant consumed;
valid grant ⇒ one real file replacement; replay ⇒ denied; external edit
after approval ⇒ stale-revision rejection; another root unchanged; and
ReadOnly mode ⇒ no write grants.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --workspace-approved-patch-spike
```

**Important limits:** There is no renderer-accessible or agent-accessible
save endpoint. There is also still a short TOCTOU window between
`WorkspaceReader`'s final revision check and Windows rename; a trusted
kernel handle/identity gate and adversarial symlink race tests are required
before allowing untrusted tools or UI workflows to replace arbitrary files.
This module validates a safe-by-default approval protocol, not a complete
file-save feature.
