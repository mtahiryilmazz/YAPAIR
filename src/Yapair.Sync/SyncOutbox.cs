using Microsoft.Data.Sqlite;

namespace Yapair.Sync;

public sealed class SyncOutbox : IDisposable
{
    private readonly SqliteConnection _conn;

    public SyncOutbox(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        Initialize();
    }

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "cases.db");

    private void Initialize()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            create table if not exists sync_outbox (
                case_id           text primary key,
                machine_key       text,
                machine_label     text,
                created_at        text not null,
                normalized_text   text not null,
                symptom_code      text not null,
                symptom_label     text not null,
                issue_family      text not null,
                domain            text not null,
                outcome           text,
                playbook_id       text,
                user_confirmation text,
                raw_text          text,
                enqueued_at       text not null,
                attempts          integer not null default 0,
                last_attempt_at   text,
                last_error        text,
                sent_at           text
            );
            create index if not exists idx_outbox_pending on sync_outbox (sent_at) where sent_at is null;
            """;
        cmd.ExecuteNonQuery();
    }

    public void Enqueue(OutboxCase c)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            insert into sync_outbox (
                case_id, machine_key, machine_label, created_at,
                normalized_text, symptom_code, symptom_label, issue_family, domain,
                outcome, playbook_id, user_confirmation, raw_text, enqueued_at
            ) values (
                $case_id, $machine_key, $machine_label, $created_at,
                $normalized_text, $symptom_code, $symptom_label, $issue_family, $domain,
                $outcome, $playbook_id, $user_confirmation, $raw_text, $enqueued_at
            )
            on conflict(case_id) do nothing;
            """;
        cmd.Parameters.AddWithValue("$case_id", c.CaseId);
        cmd.Parameters.AddWithValue("$machine_key", (object?)c.MachineKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$machine_label", (object?)c.MachineLabel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created_at", c.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$normalized_text", c.NormalizedText);
        cmd.Parameters.AddWithValue("$symptom_code", c.SymptomCode);
        cmd.Parameters.AddWithValue("$symptom_label", c.SymptomLabel);
        cmd.Parameters.AddWithValue("$issue_family", c.IssueFamily);
        cmd.Parameters.AddWithValue("$domain", c.Domain);
        cmd.Parameters.AddWithValue("$outcome", (object?)c.Outcome ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$playbook_id", (object?)c.PlaybookId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$user_confirmation", (object?)c.UserConfirmation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$raw_text", (object?)c.RawText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$enqueued_at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<OutboxCase> TakePending(int limit)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            select case_id, machine_key, machine_label, created_at, normalized_text,
                   symptom_code, symptom_label, issue_family, domain, outcome, playbook_id,
                   user_confirmation, raw_text, enqueued_at, attempts, last_attempt_at, last_error
            from sync_outbox
            where sent_at is null
            order by enqueued_at
            limit $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);
        var result = new List<OutboxCase>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result.Add(new OutboxCase
            {
                CaseId = r.GetString(0),
                MachineKey = r.IsDBNull(1) ? null : r.GetString(1),
                MachineLabel = r.IsDBNull(2) ? null : r.GetString(2),
                CreatedAt = DateTimeOffset.Parse(r.GetString(3)),
                NormalizedText = r.GetString(4),
                SymptomCode = r.GetString(5),
                SymptomLabel = r.GetString(6),
                IssueFamily = r.GetString(7),
                Domain = r.GetString(8),
                Outcome = r.IsDBNull(9) ? null : r.GetString(9),
                PlaybookId = r.IsDBNull(10) ? null : r.GetString(10),
                UserConfirmation = r.IsDBNull(11) ? null : r.GetString(11),
                RawText = r.IsDBNull(12) ? null : r.GetString(12),
                EnqueuedAt = DateTimeOffset.Parse(r.GetString(13)),
                Attempts = r.GetInt32(14),
                LastAttemptAt = r.IsDBNull(15) ? null : DateTimeOffset.Parse(r.GetString(15)),
                LastError = r.IsDBNull(16) ? null : r.GetString(16)
            });
        }
        return result;
    }

    public void MarkSent(IEnumerable<string> caseIds)
    {
        var ids = caseIds.ToArray();
        if (ids.Length == 0) return;
        using var tx = _conn.BeginTransaction();
        foreach (var id in ids)
        {
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "update sync_outbox set sent_at = $at where case_id = $id";
            cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void MarkFailed(IEnumerable<string> caseIds, string error)
    {
        var ids = caseIds.ToArray();
        if (ids.Length == 0) return;
        using var tx = _conn.BeginTransaction();
        foreach (var id in ids)
        {
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                update sync_outbox
                set attempts = attempts + 1,
                    last_attempt_at = $at,
                    last_error = $err
                where case_id = $id
                """;
            cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$err", error.Length > 500 ? error[..500] : error);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public OutboxStats Stats()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            select
              sum(case when sent_at is null and last_error is null then 1 else 0 end) as pending,
              sum(case when sent_at is null and last_error is not null then 1 else 0 end) as failed,
              sum(case when sent_at is not null then 1 else 0 end) as sent
            from sync_outbox;
            """;
        using var r = cmd.ExecuteReader();
        r.Read();
        return new OutboxStats
        {
            Pending = r.IsDBNull(0) ? 0 : r.GetInt32(0),
            Failed = r.IsDBNull(1) ? 0 : r.GetInt32(1),
            Sent = r.IsDBNull(2) ? 0 : r.GetInt32(2)
        };
    }

    public void Dispose() => _conn.Dispose();
}
