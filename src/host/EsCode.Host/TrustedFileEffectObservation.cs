using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

// T06: independently re-read a local target state and persist an observation.
// A matching target is NOT proof who performed a prior write, and an absent
// target is NEVER proof that a side effect did not occur.
internal sealed record FileEffectObservation(string ObservationId, string OperationId,
    string Result, string? ObservedSha256, bool RequiresHumanReview, bool RetryPermitted);

internal sealed partial class SqliteJournal
{
    // Existing patch argument hashes omit actor identity. A separately bounded,
    // canonical journal digest must bind the authenticated actor AND the full
    // patch grant intent. Legacy unbound operations are not observable.
    internal static string BindJournalPatchIntent(BrokerIntent intent)
    {
        if (intent.Actor is null ||
            !Regex.IsMatch(intent.Actor, "^[A-Za-z0-9_-]{1,100}$",
                RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid authenticated actor");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            intent.Actor, intent.Tool, intent.Resource, intent.ArgumentsDigest,
            Risk = intent.Risk.ToString()
        });
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(payload)).ToLowerInvariant();
    }

    internal FileEffectObservation ObserveFilePatchOutcome(
        string operationId, string workspaceId, string actor, string relativePath,
        string expectedSourceDigest, string proposedText,
        WorkspaceSessionRegistry sessions, ApprovedWorkspacePatch patch)
    {
        CheckId(operationId);
        CheckId(workspaceId);
        if (sessions is null || patch is null)
            throw new ArgumentException("Trusted Host verifier dependencies missing");
        var intent = patch.Describe(actor, relativePath, expectedSourceDigest, proposedText);
        var journalDigest = BindJournalPatchIntent(intent);
        if (intent.Resource != "workspace/" + workspaceId + "/" + relativePath)
            throw new UnauthorizedAccessException("Patch session and resource differ");
        lock (_gate)
        {
            var operation = Read(operationId);
            if (operation is null || operation.Kind != "workspace-patch" ||
                operation.State != JournalState.OutcomeUnknown ||
                ReadDigest(operationId) != journalDigest)
                throw new UnauthorizedAccessException("No matching uncertain file operation");
        }

        string? observedDigest = null;
        var result = "Uncertain";
        try
        {
            // Existing G4.1 path guards, reparse-point checks and SHA256 live
            // reads apply. Do not rely on an old checkpoint or model assertion.
            var live = sessions.Read(workspaceId, relativePath);
            observedDigest = live.Sha256;
            var desired = "sha256:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(proposedText))).ToLowerInvariant();
            if (live.Sha256 == desired && live.Content == proposedText)
                result = "TargetContentObserved";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or InvalidDataException)
        {
            // A missing/inaccessible file is an UNCERTAIN result, not evidence
            // of an absent side effect and NEVER permission for a retry.
        }

        var observationId = "obs-" + Guid.NewGuid().ToString("N");
        var resourceDigest = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                "workspace/" + workspaceId + "/" + relativePath))).ToLowerInvariant();
        lock (_gate)
        {
            using var tx = _db.BeginTransaction();
            using (var count = Command(tx, """
                SELECT COUNT(*) FROM effect_observations WHERE operation_id=@op
                """))
            {
                count.Parameters.AddWithValue("@op", operationId);
                if (Convert.ToInt32(count.ExecuteScalar()) >= 12)
                    throw new InvalidOperationException("Observation count limit reached");
            }
            using (var write = Command(tx, """
                INSERT INTO effect_observations(
                  observation_id,operation_id,workspace_id,resource_sha256,
                  intent_sha256,observed_sha256,result,created_utc)
                SELECT @observation,id,@workspace,@resource,@intent,@observed,@result,@at
                FROM operations WHERE id=@op AND kind='workspace-patch'
                  AND state='OutcomeUnknown' AND args_sha256=@intent
                """))
            {
                write.Parameters.AddWithValue("@observation", observationId);
                write.Parameters.AddWithValue("@op", operationId);
                write.Parameters.AddWithValue("@workspace", workspaceId);
                write.Parameters.AddWithValue("@resource", resourceDigest);
                write.Parameters.AddWithValue("@intent", journalDigest);
                write.Parameters.AddWithValue("@observed",
                    (object?)observedDigest ?? DBNull.Value);
                write.Parameters.AddWithValue("@result", result);
                write.Parameters.AddWithValue("@at", Time());
                if (write.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("Operation changed during verification");
            }
            tx.Commit();
        }

        // Review-only, even for matching content. Never calls Transition,
        // ReconcileAbsentEffect, tool dispatch or CanRetry.
        return new FileEffectObservation(observationId, operationId, result,
            observedDigest, RequiresHumanReview: true, RetryPermitted: false);
    }

    internal IReadOnlyList<FileEffectObservation> ReadFileObservations(string operationId)
    {
        CheckId(operationId);
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                SELECT observation_id,result,observed_sha256
                FROM effect_observations WHERE operation_id=@id
                ORDER BY created_utc,observation_id LIMIT 12
                """;
            cmd.Parameters.AddWithValue("@id", operationId);
            var results = new List<FileEffectObservation>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                results.Add(new FileEffectObservation(reader.GetString(0),
                    operationId, reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    RequiresHumanReview: true, RetryPermitted: false));
            return results.AsReadOnly();
        }
    }
}

internal static class TrustedEffectObservationSpike
{
    internal static bool Run()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "escode-effect-observe-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(Path.Combine(workspace, "src"));
        try
        {
            const string path = "src/Program.cs";
            var file = Path.Combine(workspace, "src", "Program.cs");
            File.WriteAllText(file, "class Before {}");
            var registry = new WorkspaceSessionRegistry();
            registry.AddTrustedRoot("A", workspace);
            var broker = new ToolPolicyBroker(BrokerMode.Supervised, "A");
            var patch = new ApprovedWorkspacePatch(registry, broker, "A");
            var original = registry.Read("A", path);
            const string proposed = "class After {}";
            var intent = patch.Describe("developer", path, original.Sha256, proposed);
            var database = Path.Combine(root, "effects.sqlite3");

            using (var journal = new SqliteJournal(database))
            {
                journal.Prepare("fileop1", "workspace-patch",
                    SqliteJournal.BindJournalPatchIntent(intent), idempotent: false);
                journal.MarkExecuting("fileop1");
                // Reproduce a crash after writing the file but before receipt.
                File.WriteAllText(file, proposed);
            }
            using (var journal = new SqliteJournal(database))
            {
                if (journal.RecoverInterrupted() != 1) throw new InvalidOperationException("EFFECT_TEST_CHECK_155");
                var observed = journal.ObserveFilePatchOutcome("fileop1", "A",
                    "developer", path, original.Sha256, proposed, registry, patch);
                if (observed.Result != "TargetContentObserved" ||
                    !observed.RequiresHumanReview || observed.RetryPermitted ||
                    journal.CanRetry("fileop1") ||
                    journal.Read("fileop1")?.State != JournalState.OutcomeUnknown)
                    throw new InvalidOperationException("EFFECT_TEST_CHECK_162");
                try
                {
                    journal.ObserveFilePatchOutcome("fileop1", "A", "attacker",
                        path, original.Sha256, proposed, registry, patch);
                    throw new InvalidOperationException("EFFECT_TEST_CHECK_167");
                }
                catch (UnauthorizedAccessException) { }
                try
                {
                    journal.ObserveFilePatchOutcome("fileop1", "B", "developer",
                        path, original.Sha256, proposed, registry, patch);
                    throw new InvalidOperationException("EFFECT_TEST_CHECK_174");
                }
                catch (UnauthorizedAccessException) { }
                // A pre-fix journal record without actor binding must remain
                // uncertain and must NOT create a trusted observation.
                journal.Prepare("legacyop", "workspace-patch",
                    intent.ArgumentsDigest, idempotent: false);
                journal.MarkExecuting("legacyop");
                if (journal.RecoverInterrupted() != 1)
                    throw new InvalidOperationException("Legacy setup did not recover");
                try
                {
                    journal.ObserveFilePatchOutcome("legacyop", "A", "developer",
                        path, original.Sha256, proposed, registry, patch);
                    throw new InvalidOperationException("Legacy unbound digest accepted");
                }
                catch (UnauthorizedAccessException) { }
                File.WriteAllText(file, "class External {}");
                var uncertain = journal.ObserveFilePatchOutcome("fileop1", "A",
                    "developer", path, original.Sha256, proposed, registry, patch);
                if (uncertain.Result != "Uncertain" || uncertain.RetryPermitted ||
                    journal.ReadFileObservations("fileop1").Count != 2 ||
                    journal.ReconcileAbsentEffect("fileop1", intent.ArgumentsDigest))
                    throw new InvalidOperationException("EFFECT_TEST_CHECK_183");
            }
            using (var reopened = new SqliteJournal(database))
            {
                var persisted = reopened.ReadFileObservations("fileop1");
                if (persisted.Count != 2 ||
                    !persisted.Any(x => x.Result == "TargetContentObserved") ||
                    !persisted.Any(x => x.Result == "Uncertain") ||
                    persisted.Any(x => x.RetryPermitted || !x.RequiresHumanReview) ||
                    reopened.CanRetry("fileop1") ||
                    reopened.Read("fileop1")?.State != JournalState.OutcomeUnknown)
                    throw new InvalidOperationException("EFFECT_TEST_CHECK_194");
            }
            return true;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
