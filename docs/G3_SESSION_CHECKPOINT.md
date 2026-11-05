# G3 — DPAPI encrypted checkpoint and recovery preview (T04/T05 partial)

An upgraded SQLite journal schema v2 now stores encrypted unsaved editor buffer snapshots and pending-operation references in one transaction with the checkpoint metadata. Sensitive contents are encrypted with Windows DPAPI under the current user; they are not logged or sent to a renderer. The existing SQLite journal remains WAL + FULL synchronous.

The Host validates limits (32 buffers, 128KiB per buffer, 2MiB total, 64 operation IDs), rejects traversal and sensitive filenames, and binds recovery to the expected workspace and run ID. No restored buffer is automatically written to disk, and no external operation is replayed; the result is a manual-review preview. A crash while a pending operation is Executing must be explicitly moved to OutcomeUnknown by the existing recovery method.

Windows CI covers round-trip save/reopen/backup, wrong-workspace rejection, duplicate-checkpoint rollback, oversized/malicious filenames, DPAPI ciphertext tampering, corrupted SQLite refusal and schema v1 to v2 upgrade.

Security limitation: DPAPI user scope is not portable to another machine/profile without account key material. This is not a backup or cross-machine restore solution. Windows crash-kill and future-schema tests remain separate. Power loss/disk-full fault injection, actual Monaco buffer capture and durable UI approval are still open.

T06: ReconcileAbsentEffect and CanRetry remain fail-closed. No external effect is silently considered absent based on a supplied digest; a resource-specific trusted proof verifier has not been implemented.

Run on Windows: dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --session-checkpoint-spike
