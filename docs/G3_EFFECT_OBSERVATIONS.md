# G3 T06 — File desired-state observations, NOT automatic reconciliation

The trusted Host can re-read a file through the G4.1 workspace reader, recompute the exact G4.1 approved-patch intent hash, verify that it matches a journaled workspace-patch operation in OutcomeUnknown and record a durable, append-only observation in SQLite. The schema moves transactionally from v2 to v3 and stores only digests/status/IDs (not source file contents).

The only possible outcomes are TargetContentObserved and Uncertain. **TargetContentObserved does NOT prove which process wrote the file, when the side effect occurred, or that a remote/Git operation is complete.** It is only live evidence of the desired local content at observation time. Observations require manual review, do not change the operation state, and never grant retry. Unknown, missing, unreadable, changed, or redirected content remains Uncertain.

Negative tests cover mismatched actor/workspace intent, changed target file, restart persistence, evidence record bounds and the existing CanRetry/ReconcileAbsentEffect fail-closed policy. No provider or renderer may submit an 'approved' observation or pretend to be a trusted verifier.

Run Windows Host with --effect-observation-spike.

Still open T06: true external system-of-record verification, signed attestation/idempotency receipts, compare-and-swap fencing, cross-service race analysis, human reconciliation UI and audited approval before safely replaying any external effect. All automatic retries remain disabled.

## Actor-binding correction from Windows negative test

The initial test showed that the older G4.1 patch argument digest did not include the actor identity. A journal operation is therefore now eligible for a trusted observation **only** when its SHA-256 journal digest binds the actor, tool, resource, patch argument hash and risk together through `BindJournalPatchIntent`. Legacy records containing only the patch argument hash remain OutcomeUnknown and are explicitly denied observation. The journal state never becomes Succeeded and automatic retry remains impossible.
