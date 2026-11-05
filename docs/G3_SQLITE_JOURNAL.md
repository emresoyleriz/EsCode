# G3 — SQLite durable operation journal (initial vertical slice)

**Status: implemented core proposal, pending CI/review. Not a complete crash-safe agent runtime.**

- Uses pinned Microsoft.Data.Sqlite 10.0.14 with WAL, synchronous FULL and foreign keys.
- On-disk SQLite with schema version 1, explicit upgrade from version 0; rejects unknown newer schema.
- Single active process writer via exclusive owner lock plus SQLite transaction atomicity.
- Parameterized operation IDs, kinds, argument SHA-256 digests, state transitions and append-only event metadata.
- Stores only operation metadata/digests; no secrets, prompts or raw files.
- On restart, operations interrupted in Executing become OutcomeUnknown; **no automatic retry**.
- Reconciliation requires idempotence and an explicit verified-effect-absent digest argument from trusted code; this method DOES NOT itself verify external effects and must not be wired to a model/renderer.
- Checkpoint metadata and non-overwriting SQLite consistent backup.
- Positive/negative smoke runs in CI: restart, unknown outcome, duplicate operations, invalid IDs, non-idempotent denial, backup/restore.

### Windows verification

```powershell
dotnet build src/host/EsCode.Host/EsCode.Host.csproj -c Release
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --journal-spike
```

**Still open for G3:** full global/workspace SQLite split, schema migration rollback strategy, durability kill-point matrix, UI journal/event replay, workspace snapshot integrity, actual file checkpoint contents, external side-effect reconciliation, backup/restore operator UI and secure credential storage. E04-S01/S02/S03 remain open until those requirements and real Windows crash tests are verified.

## G3 crash-kill integration and backup hardening

The Windows CI suite now launches a separate .NET Host process in **Prepared** and **Executing** states, waits until the journal's SQLite transaction is durably committed, forcibly kills that process, then reopens the same database in a new process. It verifies:

- A killed Prepared operation remains Prepared, with zero recovery transitions.
- A killed Executing operation becomes OutcomeUnknown **exactly once** after explicit recovery.
- No operation is silently marked Succeeded, automatically retried or reconciled.
- Reopening after recovery preserves the state and does not duplicate transitions.
- Child processes run only a known safe Host test entrypoint and perform no external effects.

SQLite consistent backups are now staged under a unique same-directory name, closed, then atomically published without overwriting an existing backup. A failed snapshot must not appear under the target backup name.

```powershell
dotnet run --project src/host/EsCode.Host/EsCode.Host.csproj -c Release --no-build -- --journal-crash-spike
```

**Still not complete:** This test covers two deterministic kill points, not sudden power loss, disk full/corrupt DB, external effects, global/workspace DB separation or actual file checkpoint content. Note that the legacy ReconcileAbsentEffect API validates only digest syntax, NOT independent evidence of an absent external side effect; it must remain inaccessible to model/renderer and must be replaced with a trusted effect verifier before safe retry support is released.

## G3 fail-closed reconciliation hardening

Previously `ReconcileAbsentEffect(id, verificationDigest)` treated **any syntactically valid SHA-256 value** supplied by the caller as proof that an external side effect was absent, making an OutcomeUnknown operation retryable. This was unsafe.

The method now always denies reconciliation and `CanRetry` always returns false, including for legacy journal rows marked Reconciled. No provider request, Git operation, file write or external action will be automatically replayed based on unverifiable evidence. This intentionally disables automated retry until a trusted **external system-of-record verifier** and a provenance-bearing evidence contract exist.

Windows CI `--journal-safety-spike` proves a valid hash cannot authorize retry and confirms that attempting to open an unsupported future SQLite schema cannot downgrade the database version. This is not a replacement for full crash/power-failure validation.
