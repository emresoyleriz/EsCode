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
