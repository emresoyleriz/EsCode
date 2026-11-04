using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

// G3: durable local *metadata* journal; never stores prompts, API keys or raw user files.
// Not a tool execution engine. Only the trusted Host may create or advance operations.
internal enum JournalState { Prepared, Executing, OutcomeUnknown, Succeeded, Failed, Reconciled }
internal sealed record JournalOperation(string Id, string Kind, JournalState State, bool Idempotent, bool EvidenceEffectAbsent);

internal sealed class SqliteJournal : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly FileStream _ownerLock;
    private readonly object _gate = new();
    private const int Version = 1;
    private static readonly Regex SafeId = new("^[A-Za-z0-9_-]{1,100}$", RegexOptions.CultureInvariant);
    private static readonly Regex Sha = new("^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant);

    internal SqliteJournal(string databasePath)
    {
        if (!Path.IsPathFullyQualified(databasePath))
            throw new ArgumentException("Journal path must be absolute", nameof(databasePath));
        var fullPath = Path.GetFullPath(databasePath);
        if (!fullPath.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Journal must be an .sqlite3 file", nameof(databasePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _ownerLock = new FileStream(fullPath + ".owner.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = fullPath, Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private, Pooling = false
            }.ToString();
            _db = new SqliteConnection(connectionString);
            _db.Open();
            Execute("PRAGMA foreign_keys=ON;");
            Execute("PRAGMA busy_timeout=5000;");
            Execute("PRAGMA journal_mode=WAL;");
            Execute("PRAGMA synchronous=FULL;");
            Migrate();
        }
        catch
        {
            _ownerLock.Dispose();
            throw;
        }
    }

    private void Migrate()
    {
        var current = Convert.ToInt32(Scalar("PRAGMA user_version;"), CultureInfo.InvariantCulture);
        if (current > Version || current < 0) throw new InvalidOperationException("Unsupported journal schema");
        if (current == Version) return;
        using var tx = _db.BeginTransaction();
        using var command = _db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS operations (
              id TEXT PRIMARY KEY,
              kind TEXT NOT NULL,
              state TEXT NOT NULL CHECK(state IN
                ('Prepared','Executing','OutcomeUnknown','Succeeded','Failed','Reconciled')),
              args_sha256 TEXT NOT NULL,
              idempotent INTEGER NOT NULL CHECK(idempotent IN (0,1)),
              evidence_effect_absent INTEGER NOT NULL DEFAULT 0 CHECK(evidence_effect_absent IN (0,1)),
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS events (
              sequence INTEGER PRIMARY KEY AUTOINCREMENT,
              operation_id TEXT NOT NULL REFERENCES operations(id),
              state TEXT NOT NULL,
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS checkpoints (
              id TEXT PRIMARY KEY, workspace_id TEXT NOT NULL,
              git_ref TEXT NOT NULL, manifest_sha256 TEXT NOT NULL,
              created_utc TEXT NOT NULL
            );
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
        tx.Commit();
    }

    internal void Prepare(string id, string kind, string argsDigest, bool idempotent)
    {
        CheckId(id); CheckId(kind);
        if (!Sha.IsMatch(argsDigest)) throw new ArgumentException("Invalid SHA-256 digest");
        lock (_gate)
        {
            using var tx = _db.BeginTransaction();
            using var cmd = Command(tx, "INSERT INTO operations (id,kind,state,args_sha256,idempotent,updated_utc) VALUES (@id,@kind,'Prepared',@digest,@idempotent,@at)");
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@kind", kind);
            cmd.Parameters.AddWithValue("@digest", argsDigest);
            cmd.Parameters.AddWithValue("@idempotent", idempotent ? 1 : 0);
            cmd.Parameters.AddWithValue("@at", Time());
            if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Journal insert failed");
            AppendEvent(tx, id, JournalState.Prepared);
            tx.Commit();
        }
    }

    internal void MarkExecuting(string id) => Transition(id, JournalState.Prepared, JournalState.Executing);
    internal void MarkResult(string id, JournalState result)
    {
        if (result is not (JournalState.OutcomeUnknown or JournalState.Succeeded or JournalState.Failed))
            throw new ArgumentException("Invalid terminal state");
        Transition(id, JournalState.Executing, result);
    }

    private void Transition(string id, JournalState before, JournalState after, bool proofAbsent = false)
    {
        CheckId(id);
        lock (_gate)
        {
            using var tx = _db.BeginTransaction();
            using var cmd = Command(tx, """
                UPDATE operations SET state=@after, evidence_effect_absent=@proof, updated_utc=@at
                WHERE id=@id AND state=@before
                """);
            cmd.Parameters.AddWithValue("@after", after.ToString());
            cmd.Parameters.AddWithValue("@before", before.ToString());
            cmd.Parameters.AddWithValue("@proof", proofAbsent ? 1 : 0);
            cmd.Parameters.AddWithValue("@at", Time());
            cmd.Parameters.AddWithValue("@id", id);
            if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Illegal operation transition");
            AppendEvent(tx, id, after);
            tx.Commit();
        }
    }

    // Running operations on restart have uncertain external effects. Never auto-retry.
    internal int RecoverInterrupted()
    {
        lock (_gate)
        {
            using var command = _db.CreateCommand();
            command.CommandText = "SELECT id FROM operations WHERE state='Executing' ORDER BY id";
            var ids = new List<string>();
            using (var reader = command.ExecuteReader())
                while (reader.Read()) ids.Add(reader.GetString(0));
            foreach (var id in ids) Transition(id, JournalState.Executing, JournalState.OutcomeUnknown);
            return ids.Count;
        }
    }

    internal bool ReconcileAbsentEffect(string id, string verificationDigest)
    {
        CheckId(id);
        if (!Sha.IsMatch(verificationDigest)) return false;
        lock (_gate)
        {
            var op = Read(id);
            if (op?.State != JournalState.OutcomeUnknown || !op.Idempotent) return false;
            Transition(id, JournalState.OutcomeUnknown, JournalState.Reconciled, proofAbsent: true);
            return true;
        }
    }

    internal bool CanRetry(string id)
    {
        var op = Read(id);
        return op is { State: JournalState.Reconciled, Idempotent: true, EvidenceEffectAbsent: true };
    }

    internal JournalOperation? Read(string id)
    {
        CheckId(id);
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT kind,state,idempotent,evidence_effect_absent FROM operations WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new JournalOperation(id, reader.GetString(0),
                Enum.Parse<JournalState>(reader.GetString(1)), reader.GetInt64(2) == 1,
                reader.GetInt64(3) == 1);
        }
    }

    internal void Checkpoint(string id, string workspaceId, string gitRef, string manifestDigest)
    {
        CheckId(id); CheckId(workspaceId);
        if (string.IsNullOrWhiteSpace(gitRef) || gitRef.Length > 250 || !Sha.IsMatch(manifestDigest))
            throw new ArgumentException("Invalid checkpoint data");
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT INTO checkpoints(id,workspace_id,git_ref,manifest_sha256,created_utc) VALUES (@id,@workspace,@git,@digest,@at)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@workspace", workspaceId);
            cmd.Parameters.AddWithValue("@git", gitRef);
            cmd.Parameters.AddWithValue("@digest", manifestDigest);
            cmd.Parameters.AddWithValue("@at", Time());
            cmd.ExecuteNonQuery();
        }
    }

    internal int CheckpointCount()
    {
        lock (_gate) return Convert.ToInt32(Scalar("SELECT COUNT(*) FROM checkpoints"), CultureInfo.InvariantCulture);
    }

    // Uses SQLite's consistent snapshot API; never overwrite an existing backup.
    internal void Backup(string destination)
    {
        if (!Path.IsPathFullyQualified(destination)) throw new ArgumentException("Backup path must be absolute");
        var full = Path.GetFullPath(destination);
        if (File.Exists(full)) throw new IOException("Backup already exists");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        lock (_gate)
        {
            using var backup = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = full, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
            }.ToString());
            backup.Open();
            _db.BackupDatabase(backup);
        }
    }

    private static SqliteCommand Command(SqliteTransaction tx, string sql)
    {
        var cmd = tx.Connection!.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd;
    }
    private void AppendEvent(SqliteTransaction tx, string id, JournalState state)
    {
        using var cmd = Command(tx,
            "INSERT INTO events(operation_id,state,updated_utc) VALUES (@id,@state,@at)");
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@state", state.ToString());
        cmd.Parameters.AddWithValue("@at", Time());
        cmd.ExecuteNonQuery();
    }
    private object? Scalar(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
    private void Execute(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
    private static string Time() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private static void CheckId(string value)
    {
        if (value is null || !SafeId.IsMatch(value)) throw new ArgumentException("Invalid journal identity");
    }
    public void Dispose()
    {
        _db.Dispose();
        _ownerLock.Dispose();
    }
}
