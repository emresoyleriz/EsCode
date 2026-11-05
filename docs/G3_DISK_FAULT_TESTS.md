# G3 T05 — SQLite failure injection and incomplete transaction recovery

Windows CI now checks three failure modes:

- Deterministic SQLITE_FULL produced by an intentionally low SQLite max_page_count. The oversized write must fail, leave no partial row and preserve a clean integrity_check result.
- A separate .NET process opens a transaction and inserts data; its parent forcibly terminates it without COMMIT or rollback. A new connection must see no uncommitted row and report integrity_check=ok.
- An intentionally broken v1 session_snapshots table causes v2 migration to fail without changing user_version=1 or silently adding valid columns.

Run via --journal-fault-spike on the .NET Host. This is a deterministic database-page-quota simulation, **not** a physically full volume, storage-controller corruption or sudden power cut. Production retry policy remains fail-closed; no external effect is repeated.

Remaining T05 acceptance: fault-injection VM for actual full disk and sudden power loss, fsync tests, WAL recovery under storage faults, backup restoration and volume ACL checks.
