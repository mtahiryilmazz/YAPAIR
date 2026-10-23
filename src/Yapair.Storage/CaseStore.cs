using System.Globalization;
using Microsoft.Data.Sqlite;
using Yapair.Core.Contracts;

namespace Yapair.Storage;

/// <summary>
/// Local SQLite case store. Raw complaint text is opt-in; by default only the
/// normalized symptom tokens and canonical codes are persisted. No machine or
/// user identifier is stored.
/// </summary>
public sealed class CaseStore
{
    private const int SchemaVersion = 1;

    private readonly string _databasePath;
    private readonly TimeProvider _timeProvider;

    public CaseStore(string? databasePath = null, TimeProvider? timeProvider = null)
    {
        _databasePath = string.IsNullOrWhiteSpace(databasePath) ? DefaultDatabasePath : Path.GetFullPath(databasePath);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static string DefaultDatabasePath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "YAPAIR", "cases.db");
        }
    }

    public string DatabasePath => _databasePath;

    public void Initialize()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;

            CREATE TABLE IF NOT EXISTS schema_info (
                version INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cases (
                case_id TEXT PRIMARY KEY,
                created_at TEXT NOT NULL,
                raw_text TEXT NULL,
                normalized_text TEXT NOT NULL,
                symptom_code TEXT NOT NULL,
                symptom_label TEXT NOT NULL,
                issue_family TEXT NOT NULL,
                domain TEXT NOT NULL,
                snapshot_id TEXT NULL,
                outcome TEXT NULL,
                playbook_id TEXT NULL,
                user_confirmation TEXT NULL,
                machine_key TEXT NULL,
                machine_label TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_cases_family ON cases (issue_family);
            CREATE INDEX IF NOT EXISTS idx_cases_created ON cases (created_at);

            INSERT INTO schema_info (version)
            SELECT $version
            WHERE NOT EXISTS (SELECT 1 FROM schema_info);

            UPDATE schema_info SET version = $version;
            """;
        command.Parameters.AddWithValue("$version", SchemaVersion);
        command.ExecuteNonQuery();
        EnsureMachineColumns(connection);
    }

    private static void EnsureMachineColumns(SqliteConnection connection)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(cases);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                existing.Add(reader.GetString(1));
            }
        }

        if (!existing.Contains("machine_key"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE cases ADD COLUMN machine_key TEXT NULL;";
            command.ExecuteNonQuery();
        }

        if (!existing.Contains("machine_label"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE cases ADD COLUMN machine_label TEXT NULL;";
            command.ExecuteNonQuery();
        }

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "CREATE INDEX IF NOT EXISTS idx_cases_machine ON cases (machine_key);";
        indexCommand.ExecuteNonQuery();
    }

    public CaseRecord AddCase(
        string text,
        bool storeRaw = false,
        string? snapshotId = null,
        string? playbookId = null,
        MachineIdentity? machine = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var normalized = SymptomNormalizer.Normalize(text);
        var record = new CaseRecord
        {
            CaseId = ("case-" + Guid.NewGuid().ToString("N"))[..17],
            CreatedAt = _timeProvider.GetUtcNow(),
            RawText = storeRaw ? text : null,
            NormalizedText = normalized.NormalizedText,
            SymptomCode = normalized.Code,
            SymptomLabel = normalized.Label,
            IssueFamily = normalized.IssueFamily,
            Domain = normalized.Domain,
            SnapshotId = snapshotId,
            PlaybookId = playbookId,
            MachineKey = machine?.Key,
            MachineLabel = machine?.Label,
        };

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO cases (
                case_id, created_at, raw_text, normalized_text, symptom_code,
                symptom_label, issue_family, domain, snapshot_id, outcome,
                playbook_id, user_confirmation, machine_key, machine_label)
            VALUES (
                $case_id, $created_at, $raw_text, $normalized_text, $symptom_code,
                $symptom_label, $issue_family, $domain, $snapshot_id, NULL,
                $playbook_id, NULL, $machine_key, $machine_label);
            """;
        command.Parameters.AddWithValue("$case_id", record.CaseId);
        command.Parameters.AddWithValue("$created_at", record.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$raw_text", (object?)record.RawText ?? DBNull.Value);
        command.Parameters.AddWithValue("$normalized_text", record.NormalizedText);
        command.Parameters.AddWithValue("$symptom_code", record.SymptomCode);
        command.Parameters.AddWithValue("$symptom_label", record.SymptomLabel);
        command.Parameters.AddWithValue("$issue_family", record.IssueFamily);
        command.Parameters.AddWithValue("$domain", record.Domain);
        command.Parameters.AddWithValue("$snapshot_id", (object?)record.SnapshotId ?? DBNull.Value);
        command.Parameters.AddWithValue("$playbook_id", (object?)record.PlaybookId ?? DBNull.Value);
        command.Parameters.AddWithValue("$machine_key", (object?)record.MachineKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$machine_label", (object?)record.MachineLabel ?? DBNull.Value);
        command.ExecuteNonQuery();

        return record;
    }

    public IReadOnlyList<CaseRecord> GetCases(int limit = 100)
    {
        if (limit <= 0)
        {
            return [];
        }

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT case_id, created_at, raw_text, normalized_text, symptom_code,
                   symptom_label, issue_family, domain, snapshot_id, outcome,
                   playbook_id, user_confirmation, machine_key, machine_label
            FROM cases
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var records = new List<CaseRecord>();
        while (reader.Read())
        {
            records.Add(Read(reader));
        }

        return records;
    }

    public IReadOnlyList<CaseRecord> GetAllCases()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT case_id, created_at, raw_text, normalized_text, symptom_code,
                   symptom_label, issue_family, domain, snapshot_id, outcome,
                   playbook_id, user_confirmation, machine_key, machine_label
            FROM cases
            ORDER BY created_at;
            """;

        using var reader = command.ExecuteReader();
        var records = new List<CaseRecord>();
        while (reader.Read())
        {
            records.Add(Read(reader));
        }

        return records;
    }

    public int Count()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM cases;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public bool SetOutcome(string caseId, string outcome, string? userConfirmation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE cases
            SET outcome = $outcome, user_confirmation = $user_confirmation
            WHERE case_id = $case_id;
            """;
        command.Parameters.AddWithValue("$outcome", outcome);
        command.Parameters.AddWithValue("$user_confirmation", (object?)userConfirmation ?? DBNull.Value);
        command.Parameters.AddWithValue("$case_id", caseId);
        return command.ExecuteNonQuery() > 0;
    }

    private SqliteConnection Open()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static CaseRecord Read(SqliteDataReader reader) => new()
    {
        CaseId = reader.GetString(0),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        RawText = reader.IsDBNull(2) ? null : reader.GetString(2),
        NormalizedText = reader.GetString(3),
        SymptomCode = reader.GetString(4),
        SymptomLabel = reader.GetString(5),
        IssueFamily = reader.GetString(6),
        Domain = reader.GetString(7),
        SnapshotId = reader.IsDBNull(8) ? null : reader.GetString(8),
        Outcome = reader.IsDBNull(9) ? null : reader.GetString(9),
        PlaybookId = reader.IsDBNull(10) ? null : reader.GetString(10),
        UserConfirmation = reader.IsDBNull(11) ? null : reader.GetString(11),
        MachineKey = reader.IsDBNull(12) ? null : reader.GetString(12),
        MachineLabel = reader.IsDBNull(13) ? null : reader.GetString(13),
    };
}
