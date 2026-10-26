using System.Net.Http.Json;
using System.Text.Json;

namespace Yapair.Api;

public sealed class SupabaseClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly LocalDatabase _localDb;
    private readonly bool _isLocalMode;

    public LocalDatabase LocalDb => _localDb;

    public SupabaseClient(HttpClient http, LocalDatabase? localDb = null)
    {
        _http = http;
        _localDb = localDb ?? new LocalDatabase();
        _isLocalMode = http.BaseAddress == null || http.BaseAddress.Host == "localhost" && http.DefaultRequestHeaders.Contains("x-local-mode");
    }

    public async Task<bool> BatchExistsAsync(string batchId, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            return _localDb.Batches.Any(b => string.Equals(Str(b, "batch_id"), batchId, StringComparison.OrdinalIgnoreCase));
        }

        var url = $"rest/v1/sync_receipts?batch_id=eq.{Uri.EscapeDataString(batchId)}&select=batch_id&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return false;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0;
    }

    public async Task UpsertMachineAsync(MachineRow row, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var idx = _localDb.Machines.FindIndex(m => string.Equals(Str(m, "machine_key"), row.MachineKey, StringComparison.OrdinalIgnoreCase));
            var existing = idx >= 0 ? _localDb.Machines[idx] : null;
            var dict = new Dictionary<string, object?>
            {
                ["machine_key"] = row.MachineKey,
                ["machine_label"] = row.MachineLabel,
                ["first_seen"] = existing?.GetValueOrDefault("first_seen") ?? DateTimeOffset.UtcNow.ToString("o"),
                ["last_seen"] = row.LastSeen.ToString("o"),
                ["notes"] = existing?.GetValueOrDefault("notes"),
                ["owner"] = existing?.GetValueOrDefault("owner")
            };
            if (idx >= 0) _localDb.Machines[idx] = dict;
            else _localDb.Machines.Add(dict);
            _localDb.Save();
            return;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/machines?on_conflict=machine_key")
        { Content = JsonContent.Create(row, options: Json) };
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machines upsert", ct);
    }

    public async Task<int> InsertCasesAsync(IReadOnlyList<CaseRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        if (_isLocalMode)
        {
            foreach (var r in rows)
            {
                var dict = new Dictionary<string, object?>
                {
                    ["case_id"] = r.CaseId,
                    ["machine_key"] = r.MachineKey,
                    ["created_at"] = r.CreatedAt.ToString("o"),
                    ["received_at"] = DateTimeOffset.UtcNow.ToString("o"),
                    ["normalized_text"] = r.NormalizedText,
                    ["symptom_code"] = r.SymptomCode,
                    ["symptom_label"] = r.SymptomLabel,
                    ["issue_family"] = r.IssueFamily,
                    ["domain"] = r.Domain,
                    ["outcome"] = r.Outcome,
                    ["playbook_id"] = r.PlaybookId,
                    ["user_confirmation"] = r.UserConfirmation,
                    ["raw_text"] = r.RawText,
                    ["archived_at"] = null,
                    ["machines"] = new Dictionary<string, object?> { ["machine_label"] = r.MachineKey }
                };
                var idx = _localDb.Cases.FindIndex(c => string.Equals(Str(c, "case_id"), r.CaseId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) _localDb.Cases[idx] = dict;
                else _localDb.Cases.Add(dict);
            }
            _localDb.Save();
            return rows.Count;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/cases?on_conflict=case_id")
        { Content = JsonContent.Create(rows, options: Json) };
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "cases insert", ct);
        return rows.Count;
    }

    public async Task InsertReceiptAsync(SyncReceipt receipt, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            _localDb.Batches.Add(new Dictionary<string, object?>
            {
                ["batch_id"] = receipt.BatchId,
                ["case_count"] = receipt.CaseCount,
                ["api_version"] = receipt.ApiVersion,
                ["received_at"] = DateTimeOffset.UtcNow.ToString("o")
            });
            _localDb.Save();
            return;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/sync_receipts")
        { Content = JsonContent.Create(receipt, options: Json) };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "sync_receipts insert", ct);
    }

    public async Task<object> GetAdminStatsAsync(bool includeArchived, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var cases = _localDb.Cases
                .Where(c => includeArchived || c.GetValueOrDefault("archived_at") == null)
                .OrderByDescending(c => Str(c, "received_at"))
                .Take(100)
                .ToList();

            var batches = _localDb.Batches
                .OrderByDescending(b => Str(b, "received_at"))
                .Take(20)
                .ToList();

            var machines = _localDb.Machines
                .OrderByDescending(m => Str(m, "last_seen"))
                .Take(50)
                .ToList();

            return new
            {
                cases = new { total = _localDb.Cases.Count, recent = cases },
                batches = new { total = _localDb.Batches.Count, recent = batches },
                machines = new { total = _localDb.Machines.Count, recent = machines }
            };
        }

        const string caseSelect = "case_id,machine_key,normalized_text,symptom_code,symptom_label,issue_family,domain,outcome,user_confirmation,playbook_id,received_at,archived_at,machines(machine_label)";
        var filter = includeArchived ? "" : "&archived_at=is.null";
        var qCases = await QueryTableAsync("cases", caseSelect, "received_at", 100, ct, filter);
        var qBatches = await QueryTableAsync("sync_receipts", "batch_id,case_count,api_version,received_at", "received_at", 10, ct);
        var qMachines = await QueryTableAsync("machines", "machine_key,machine_label,first_seen,last_seen,notes,owner", "last_seen", 20, ct);
        return new
        {
            cases = new { total = qCases.Total, recent = qCases.Rows },
            batches = new { total = qBatches.Total, recent = qBatches.Rows },
            machines = new { total = qMachines.Total, recent = qMachines.Rows }
        };
    }

    public async Task<Dictionary<string, object?>?> GetCaseAsync(string caseId, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            return _localDb.Cases.FirstOrDefault(c => string.Equals(Str(c, "case_id"), caseId, StringComparison.OrdinalIgnoreCase));
        }

        const string select = "case_id,machine_key,normalized_text,symptom_code,symptom_label,issue_family,domain,outcome,user_confirmation,playbook_id,raw_text,received_at,archived_at,machines(machine_label)";
        var url = $"rest/v1/cases?case_id=eq.{Uri.EscapeDataString(caseId)}&select={select}&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"case query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
        var row = doc.RootElement[0];
        var dict = new Dictionary<string, object?>();
        foreach (var p in row.EnumerateObject()) dict[p.Name] = ConvertElement(p.Value);
        return dict;
    }

    public async Task<int> ArchiveCasesAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var now = DateTimeOffset.UtcNow.ToString("o");
            var count = 0;
            foreach (var id in ids)
            {
                var c = _localDb.Cases.FirstOrDefault(x => string.Equals(Str(x, "case_id"), id, StringComparison.OrdinalIgnoreCase));
                if (c != null) { c["archived_at"] = now; count++; }
            }
            _localDb.Save();
            return count;
        }
        return await PatchArchivedAsync(ids, DateTimeOffset.UtcNow, ct);
    }

    public async Task<int> UnarchiveCasesAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var count = 0;
            foreach (var id in ids)
            {
                var c = _localDb.Cases.FirstOrDefault(x => string.Equals(Str(x, "case_id"), id, StringComparison.OrdinalIgnoreCase));
                if (c != null) { c["archived_at"] = null; count++; }
            }
            _localDb.Save();
            return count;
        }
        return await PatchArchivedAsync(ids, null, ct);
    }

    public async Task<int> DeleteCasesAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return 0;

        if (_isLocalMode)
        {
            var removed = _localDb.Cases.RemoveAll(c => ids.Any(id => string.Equals(Str(c, "case_id"), id, StringComparison.OrdinalIgnoreCase)));
            _localDb.Save();
            return removed;
        }

        var url = "rest/v1/cases?case_id=in.(" + BuildIdList(ids) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "cases delete", ct);
        return ids.Count;
    }

    public async Task<Dictionary<string, object?>?> GetMachineAsync(string machineKey, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            return _localDb.Machines.FirstOrDefault(m => string.Equals(Str(m, "machine_key"), machineKey, StringComparison.OrdinalIgnoreCase));
        }

        const string select = "machine_key,machine_label,first_seen,last_seen,notes,owner";
        var url = $"rest/v1/machines?machine_key=eq.{Uri.EscapeDataString(machineKey)}&select={select}&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"machine query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;
        var row = doc.RootElement[0];
        var dict = new Dictionary<string, object?>();
        foreach (var p in row.EnumerateObject()) dict[p.Name] = ConvertElement(p.Value);
        return dict;
    }

    public async Task UpdateMachineAsync(string machineKey, string? machineLabel, string? notes, string? owner, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var m = _localDb.Machines.FirstOrDefault(x => string.Equals(Str(x, "machine_key"), machineKey, StringComparison.OrdinalIgnoreCase));
            if (m == null)
            {
                m = new Dictionary<string, object?> { ["machine_key"] = machineKey };
                _localDb.Machines.Add(m);
            }
            if (!string.IsNullOrEmpty(machineLabel)) m["machine_label"] = machineLabel;
            m["notes"] = notes;
            m["owner"] = owner;
            m["last_seen"] = DateTimeOffset.UtcNow.ToString("o");
            _localDb.Save();
            return;
        }

        var url = $"rest/v1/machines?machine_key=eq.{Uri.EscapeDataString(machineKey)}";
        var body = JsonSerializer.Serialize(new { machine_label = machineLabel, notes, owner }, Json);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machine update", ct);
    }

    public async Task<int> DeleteMachinesAsync(IReadOnlyList<string> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0) return 0;

        if (_isLocalMode)
        {
            var count = _localDb.Machines.RemoveAll(m => keys.Any(k => string.Equals(Str(m, "machine_key"), k, StringComparison.OrdinalIgnoreCase)));
            _localDb.Save();
            return count;
        }

        var url = "rest/v1/machines?machine_key=in.(" + BuildIdList(keys) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machines delete", ct);
        return keys.Count;
    }

    public async Task<int> DeleteBatchesAsync(IReadOnlyList<string> batchIds, CancellationToken ct = default)
    {
        if (batchIds.Count == 0) return 0;

        if (_isLocalMode)
        {
            var count = _localDb.Batches.RemoveAll(b => batchIds.Any(id => string.Equals(Str(b, "batch_id"), id, StringComparison.OrdinalIgnoreCase)));
            _localDb.Save();
            return count;
        }

        var url = "rest/v1/sync_receipts?batch_id=in.(" + BuildIdList(batchIds) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "batches delete", ct);
        return batchIds.Count;
    }

    public async Task<long> InsertHelpRequestAsync(string? machineKey, string? machineLabel, string problemText, string? caseId, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _localDb.HelpRequests.Add(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["machine_key"] = machineKey ?? "local",
                ["machine_label"] = machineLabel ?? "Yerel Cihaz",
                ["problem_text"] = problemText,
                ["case_id"] = caseId,
                ["status"] = "open",
                ["created_at"] = DateTimeOffset.UtcNow.ToString("o")
            });
            _localDb.Save();
            return id;
        }

        var row = new { machine_key = machineKey, machine_label = machineLabel, problem_text = problemText, case_id = caseId };
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/help_requests")
        { Content = JsonContent.Create(row, options: Json) };
        req.Headers.Add("Prefer", "return=representation");
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        await EnsureSuccess(resp, "help_request insert", ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            return doc.RootElement[0].GetProperty("id").GetInt64();
        return 0;
    }

    public async Task<List<Dictionary<string, object?>>> GetHelpRequestsAsync(string? status = null, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            return _localDb.HelpRequests
                .Where(h => string.IsNullOrEmpty(status) || string.Equals(Str(h, "status"), status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => Str(h, "created_at"))
                .ToList();
        }

        var url = "rest/v1/help_requests?select=*&order=created_at.desc&limit=100";
        if (!string.IsNullOrEmpty(status)) url += $"&status=eq.{Uri.EscapeDataString(status)}";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"help_requests query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        var list = new List<Dictionary<string, object?>>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var d = new Dictionary<string, object?>();
            foreach (var p in row.EnumerateObject()) d[p.Name] = ConvertElement(p.Value);
            list.Add(d);
        }
        return list;
    }

    public async Task UpdateHelpStatusAsync(long id, string status, string? adminNote, string? assignedTo, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var h = _localDb.HelpRequests.FirstOrDefault(x => x.TryGetValue("id", out var v) && v?.ToString() == id.ToString());
            if (h != null)
            {
                h["status"] = status;
                h["admin_note"] = adminNote;
                h["assigned_to"] = assignedTo;
                h["updated_at"] = DateTimeOffset.UtcNow.ToString("o");
                _localDb.Save();
            }
            return;
        }

        var url = $"rest/v1/help_requests?id=eq.{id}";
        var row = new { status, admin_note = adminNote, assigned_to = assignedTo, updated_at = DateTimeOffset.UtcNow };
        var body = JsonSerializer.Serialize(row, Json);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "help_request update", ct);
    }

    public async Task<long> InsertAuthAuditAsync(string? machineKey, string? modeBefore, string? modeAfter, string? action, string? targetId, string? ipAddress, string? note, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _localDb.Audits.Add(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["machine_key"] = machineKey ?? Environment.MachineName,
                ["mode_before"] = modeBefore,
                ["mode_after"] = modeAfter,
                ["action"] = string.IsNullOrWhiteSpace(action) ? "mode_change" : action,
                ["target_id"] = targetId,
                ["ip_address"] = ipAddress ?? "127.0.0.1",
                ["note"] = note,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("o")
            });
            _localDb.Save();
            return id;
        }

        var row = new
        {
            machine_key = machineKey,
            mode_before = modeBefore,
            mode_after = modeAfter,
            action = string.IsNullOrWhiteSpace(action) ? "mode_change" : action,
            target_id = targetId,
            ip_address = ipAddress,
            note
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/authorization_audit")
        { Content = JsonContent.Create(row, options: Json) };
        req.Headers.Add("Prefer", "return=representation");
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        await EnsureSuccess(resp, "authorization_audit insert", ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            return doc.RootElement[0].GetProperty("id").GetInt64();
        return 0;
    }

    public async Task<List<Dictionary<string, object?>>> GetAuthAuditsAsync(string? machine = null, string? action = null, string? since = null, CancellationToken ct = default)
    {
        if (_isLocalMode)
        {
            return _localDb.Audits
                .Where(a => string.IsNullOrEmpty(machine) || string.Equals(Str(a, "machine_key"), machine, StringComparison.OrdinalIgnoreCase))
                .Where(a => string.IsNullOrEmpty(action) || string.Equals(Str(a, "action"), action, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => Str(a, "created_at"))
                .ToList();
        }

        var url = "rest/v1/authorization_audit?select=*&order=created_at.desc&limit=150";
        if (!string.IsNullOrEmpty(machine)) url += $"&machine_key=eq.{Uri.EscapeDataString(machine)}";
        if (!string.IsNullOrEmpty(action)) url += $"&action=eq.{Uri.EscapeDataString(action)}";
        if (!string.IsNullOrEmpty(since)) url += $"&created_at=gte.{Uri.EscapeDataString(since)}";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"authorization_audit query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        var list = new List<Dictionary<string, object?>>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var d = new Dictionary<string, object?>();
            foreach (var p in row.EnumerateObject()) d[p.Name] = ConvertElement(p.Value);
            list.Add(d);
        }
        return list;
    }

    private static string? Str(Dictionary<string, object?> r, string k) => r.TryGetValue(k, out var v) ? v?.ToString() : null;

    private async Task<int> PatchArchivedAsync(IReadOnlyList<string> ids, DateTimeOffset? at, CancellationToken ct)
    {
        if (ids.Count == 0) return 0;
        var url = "rest/v1/cases?case_id=in.(" + BuildIdList(ids) + ")";
        var body = JsonSerializer.Serialize(new { archived_at = at }, Json);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, at is null ? "cases unarchive" : "cases archive", ct);
        return ids.Count;
    }

    private static string BuildIdList(IReadOnlyList<string> ids)
        => Uri.EscapeDataString(string.Join(",", ids.Select(id => "\"" + id.Replace("\"", "") + "\"")));

    private async Task<(int Total, List<Dictionary<string, object?>> Rows)> QueryTableAsync(
        string table, string select, string orderBy, int limit, CancellationToken ct, string extraFilter = "")
    {
        var url = $"rest/v1/{table}?select={select}&order={orderBy}.desc&limit={limit}{extraFilter}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"{table} query failed: HTTP {(int)resp.StatusCode} {body}");
        var total = 0;
        if (resp.Content.Headers.TryGetValues("Content-Range", out var ranges))
        {
            var range = ranges.FirstOrDefault();
            if (range != null && range.Contains('/'))
            {
                var parts = range.Split('/');
                if (parts[1] != "*" && int.TryParse(parts[1], out var n)) total = n;
            }
        }
        using var doc = JsonDocument.Parse(body);
        var rows = new List<Dictionary<string, object?>>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var d = new Dictionary<string, object?>();
            foreach (var p in row.EnumerateObject()) d[p.Name] = ConvertElement(p.Value);
            rows.Add(d);
        }
        return (total, rows);
    }

    public void SeedDemoData(bool force = true)
    {
        _localDb.EnsureSeedData(force);
    }

    public async Task<string> DispatchRemoteRepairAsync(string machineKey, string playbookId, string initiatedBy, string? parameters = null, CancellationToken ct = default)
    {
        var repairId = $"REP-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():X}";
        var entry = new Dictionary<string, object?>
        {
            ["id"] = repairId,
            ["machine_key"] = machineKey,
            ["playbook_id"] = playbookId,
            ["parameters"] = parameters ?? "",
            ["status"] = "pending",
            ["initiated_by"] = initiatedBy,
            ["dispatched_at"] = DateTimeOffset.UtcNow.ToString("o"),
            ["completed_at"] = null,
            ["output"] = null
        };

        _localDb.RemoteRepairs.Insert(0, entry);
        _localDb.Save();
        return repairId;
    }

    public async Task<List<Dictionary<string, object?>>> GetPendingRemoteRepairsAsync(string machineKey, CancellationToken ct = default)
    {
        return _localDb.RemoteRepairs
            .Where(r => string.Equals(Str(r, "machine_key"), machineKey, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(Str(r, "status"), "pending", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<List<Dictionary<string, object?>>> GetRemoteRepairsAsync(string? machineKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(machineKey))
            return _localDb.RemoteRepairs.OrderByDescending(r => Str(r, "dispatched_at")).ToList();

        return _localDb.RemoteRepairs
            .Where(r => string.Equals(Str(r, "machine_key"), machineKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => Str(r, "dispatched_at"))
            .ToList();
    }

    public async Task<bool> UpdateRemoteRepairResultAsync(string repairId, string status, string? output, CancellationToken ct = default)
    {
        var item = _localDb.RemoteRepairs.FirstOrDefault(r => string.Equals(Str(r, "id"), repairId, StringComparison.OrdinalIgnoreCase));
        if (item is null) return false;

        item["status"] = status;
        item["completed_at"] = DateTimeOffset.UtcNow.ToString("o");
        item["output"] = output;
        _localDb.Save();
        return true;
    }

    private static object? ConvertElement(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => ConvertObj(e),
        JsonValueKind.Array => ConvertArr(e),
        _ => e.ToString()
    };

    private static Dictionary<string, object?> ConvertObj(JsonElement e)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in e.EnumerateObject()) d[p.Name] = ConvertElement(p.Value);
        return d;
    }

    private static List<object?> ConvertArr(JsonElement e)
    {
        var list = new List<object?>();
        foreach (var item in e.EnumerateArray()) list.Add(ConvertElement(item));
        return list;
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp, string what, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"{what} basarisiz: HTTP {(int)resp.StatusCode} {body}");
    }
}