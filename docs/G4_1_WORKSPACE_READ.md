# G4.1 — Read-only real workspace access (first slice)

Implements real on-disk workspace listing and UTF-8 text snapshots in the trusted .NET Host code path. All paths must be relative to an existing absolute root; encoded or absolute paths, traversal, symlink/junction components, .git directories, .env secrets and binaries are denied. Reads are capped at 1 MiB per file; listing at 1,024 entries. Returned snapshots include a SHA-256 digest.

The trusted Host now also includes a **bounded optimistic text replacement primitive** using expected SHA-256, a same-directory staged file and atomic File.Replace. It refuses stale snapshots in tests. **Do not expose this primitive to renderer or agent yet:** unsaved Monaco buffer revision coordination, Git base identity and race-free OS handle checks are not implemented. This is not an authorized production patch executor. These are G4.1/G4.3/G6 follow-up work. Some Windows reparse/TOCTOU threat tests still require a hostile VM.

Run:

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release -- --workspace-read-spike
```

Keep E05-S01/S02/S03 open until true read/write, buffer revisions and atomic patch tests succeed.
