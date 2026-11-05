using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

// G3: Windows DPAPI user-scoped buffer recovery. Recovery returns a review-only
// preview, never applies files or retries external effects.
internal sealed record PendingCheckpointOperation(string Id, JournalState? LastKnownState);
internal sealed record SessionRecoveryPreview(string CheckpointId, string WorkspaceId,
    string RunId, string RunState, IReadOnlyDictionary<string, string> UnsavedBuffers,
    IReadOnlyList<PendingCheckpointOperation> PendingOperations, bool RequiresHumanReview);

internal sealed partial class SqliteJournal
{
    private const int MaxCheckpointBytes = 2 * 1024 * 1024;
    private static readonly Regex CheckpointPath = new(
        "^[A-Za-z0-9_][A-Za-z0-9_ ./-]{0,239}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SensitiveName = new(
        "(secret|password|credential|private.key|id_rsa|token)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private sealed record CheckpointPayload(string WorkspaceId, string RunId,
        string RunState, SortedDictionary<string, string> Buffers, string[] Operations);

    internal void StoreSessionCheckpoint(string checkpointId, string workspaceId,
        string runId, string runState, IReadOnlyDictionary<string, string> unsavedBuffers,
        IReadOnlyList<string> pendingOperations)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        CheckId(checkpointId);
        CheckId(workspaceId);
        CheckId(runId);
        if (runState is not ("Paused" or "AwaitingApproval" or "OutcomeUnknown"))
            throw new ArgumentException("Cannot checkpoint an executable running state");
        if (unsavedBuffers is null || unsavedBuffers.Count > 32 || pendingOperations is null ||
            pendingOperations.Count > 64)
            throw new ArgumentException("Checkpoint input exceeds limits");
        var buffers = new SortedDictionary<string, string>(StringComparer.Ordinal);
        long budget = 0;
        foreach (var item in unsavedBuffers)
        {
            ValidateBuffer(item.Key, item.Value);
            budget = checked(budget + Encoding.UTF8.GetByteCount(item.Value));
            if (budget > MaxCheckpointBytes)
                throw new ArgumentException("Checkpoint buffer budget exceeded");
            buffers.Add(item.Key, item.Value);
        }
        var ops = pendingOperations.ToArray();
        if (ops.Any(x => x is null || !SafeId.IsMatch(x)) ||
            ops.Distinct(StringComparer.Ordinal).Count() != ops.Length)
            throw new ArgumentException("Invalid pending operation identities");

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(
            new CheckpointPayload(workspaceId, runId, runState, buffers, ops));
        try
        {
            if (plaintext.Length > MaxCheckpointBytes)
                throw new ArgumentException("Checkpoint payload exceeds the safe limit");
            var digest = ShaBytes(plaintext);
            var ciphertext = UserDpapi.Apply(plaintext, encrypt: true);
            try
            {
                lock (_gate)
                {
                    foreach (var id in ops)
                        if (Read(id) is null)
                            throw new InvalidOperationException("Unknown pending operation");
                    using var tx = _db.BeginTransaction();
                    using (var command = Command(tx, """
                        INSERT INTO checkpoints(id,workspace_id,git_ref,manifest_sha256,created_utc)
                        VALUES (@id,@workspace,@run,@hash,@at)
                        """))
                    {
                        command.Parameters.AddWithValue("@id", checkpointId);
                        command.Parameters.AddWithValue("@workspace", workspaceId);
                        command.Parameters.AddWithValue("@run", "session-" + runId);
                        command.Parameters.AddWithValue("@hash", digest);
                        command.Parameters.AddWithValue("@at", Time());
                        command.ExecuteNonQuery();
                    }
                    using (var command = Command(tx, """
                        INSERT INTO session_snapshots(checkpoint_id,workspace_id,run_id,
                          run_state,encrypted_payload,created_utc)
                        VALUES (@id,@workspace,@run,@state,@encrypted,@at)
                        """))
                    {
                        command.Parameters.AddWithValue("@id", checkpointId);
                        command.Parameters.AddWithValue("@workspace", workspaceId);
                        command.Parameters.AddWithValue("@run", runId);
                        command.Parameters.AddWithValue("@state", runState);
                        command.Parameters.AddWithValue("@encrypted", ciphertext);
                        command.Parameters.AddWithValue("@at", Time());
                        command.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
            finally { CryptographicOperations.ZeroMemory(ciphertext); }
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    internal SessionRecoveryPreview RestoreSessionCheckpoint(string checkpointId,
        string expectedWorkspaceId)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        CheckId(checkpointId);
        CheckId(expectedWorkspaceId);
        byte[] ciphertext;
        string storedHash, actualWorkspace, runId, runState;
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                SELECT s.workspace_id,s.run_id,s.run_state,s.encrypted_payload,
                  c.manifest_sha256 FROM session_snapshots s
                INNER JOIN checkpoints c ON c.id=s.checkpoint_id
                WHERE s.checkpoint_id=@id AND length(s.encrypted_payload)<=@size
                """;
            cmd.Parameters.AddWithValue("@id", checkpointId);
            cmd.Parameters.AddWithValue("@size", MaxCheckpointBytes + 8192);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                throw new InvalidDataException("Missing or oversize protected checkpoint");
            actualWorkspace = reader.GetString(0);
            runId = reader.GetString(1);
            runState = reader.GetString(2);
            ciphertext = reader.GetFieldValue<byte[]>(3);
            storedHash = reader.GetString(4);
        }
        if (actualWorkspace != expectedWorkspaceId)
            throw new UnauthorizedAccessException("Workspace identity does not match snapshot");
        var plaintext = UserDpapi.Apply(ciphertext, encrypt: false);
        try
        {
            var actualHash = ShaBytes(plaintext);
            if (storedHash != actualHash)
                throw new InvalidDataException("Checkpoint digest mismatch");
            var payload = JsonSerializer.Deserialize<CheckpointPayload>(plaintext,
                new JsonSerializerOptions { MaxDepth = 8 }) ??
                throw new InvalidDataException("Checkpoint payload is empty");
            if (payload.WorkspaceId != expectedWorkspaceId || payload.RunId != runId ||
                payload.RunState != runState || payload.Buffers is null ||
                payload.Operations is null || payload.Buffers.Count > 32 ||
                payload.Operations.Length > 64 ||
                runState is not ("Paused" or "AwaitingApproval" or "OutcomeUnknown"))
                throw new InvalidDataException("Checkpoint metadata mismatch");
            foreach (var entry in payload.Buffers)
                ValidateBuffer(entry.Key, entry.Value);
            if (payload.Operations.Any(x => x is null || !SafeId.IsMatch(x)) ||
                payload.Operations.Distinct(StringComparer.Ordinal).Count() != payload.Operations.Length)
                throw new InvalidDataException("Invalid persisted operation IDs");
            // A snapshot never authorizes execution or overwrites the current file.
            // Call RecoverInterrupted explicitly before presenting this preview.
            var states = payload.Operations.Select(id =>
                new PendingCheckpointOperation(id, Read(id)?.State)).ToArray();
            return new SessionRecoveryPreview(checkpointId, expectedWorkspaceId,
                runId, runState, payload.Buffers, states, RequiresHumanReview: true);
        }
        catch (JsonException) { throw new InvalidDataException("Malformed checkpoint payload"); }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private static string ShaBytes(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void ValidateBuffer(string path, string contents)
    {
        if (string.IsNullOrWhiteSpace(path) || !CheckpointPath.IsMatch(path) ||
            path.Split('/').Any(x => x.Length == 0 || x == "." || x == ".." ||
                x.StartsWith(".", StringComparison.Ordinal) || SensitiveName.IsMatch(x)) ||
            contents is null || contents.IndexOf('\0') >= 0 ||
            Encoding.UTF8.GetByteCount(contents) > 128 * 1024)
            throw new ArgumentException("Unsafe buffer path or content");
    }
}

internal static class UserDpapi
{
    private const uint UiForbidden = 0x1;
    private const int MaxInput = 2 * 1024 * 1024 + 8192;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        internal int Length;
        internal IntPtr Data;
    }

    internal static byte[] Apply(byte[] input, bool encrypt)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (input is null || input.Length is < 1 or > MaxInput)
            throw new InvalidDataException("Invalid DPAPI payload size");
        var allocated = Marshal.AllocHGlobal(input.Length);
        DataBlob result = default;
        try
        {
            Marshal.Copy(input, 0, allocated, input.Length);
            var source = new DataBlob { Length = input.Length, Data = allocated };
            var ok = encrypt
                ? CryptProtectData(ref source, "EsCode protected unsaved editor buffers",
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out result)
                : CryptUnprotectData(ref source, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out result);
            if (!ok) throw new InvalidDataException("DPAPI checkpoint decryption/protection failed");
            if (result.Length is < 1 or > MaxInput || result.Data == IntPtr.Zero)
                throw new InvalidDataException("DPAPI output exceeds limits");
            var output = new byte[result.Length];
            Marshal.Copy(result.Data, output, 0, result.Length);
            return output;
        }
        finally
        {
            Marshal.Copy(new byte[input.Length], 0, allocated, input.Length);
            Marshal.FreeHGlobal(allocated);
            if (result.Data != IntPtr.Zero)
            {
                if (result.Length is > 0 and <= MaxInput)
                    Marshal.Copy(new byte[result.Length], 0, result.Data, result.Length);
                LocalFree(result.Data);
            }
        }
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr value);
}
