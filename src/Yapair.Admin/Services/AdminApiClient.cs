using System.Net.Http.Json;
using System.Text.Json;

namespace Yapair.Admin.Services;

public sealed class AdminApiClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public AdminApiClient(string? baseUrl = null)
    {
        _baseUrl = (baseUrl ?? Environment.GetEnvironmentVariable("YAPAIR_API_BASE_URL") ?? "http://localhost:5080").TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl + "/") };
    }

    public async Task<JsonElement?> GetHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await _http.GetFromJsonAsync<JsonElement>("health", ct);
            return r;
        }
        catch { return null; }
    }

    public async Task<JsonElement?> GetStatsAsync(bool archived = false, CancellationToken ct = default)
    {
        try
        {
            var url = "admin/stats" + (archived ? "?archived=1" : "");
            return await _http.GetFromJsonAsync<JsonElement>(url, ct);
        }
        catch { return null; }
    }

    public async Task<JsonElement?> GetCaseAsync(string id, CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<JsonElement>($"admin/case/{Uri.EscapeDataString(id)}", ct); }
        catch { return null; }
    }

    public async Task<bool> ArchiveCasesAsync(List<string> ids, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/cases/archive", new { ids }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> UnarchiveCasesAsync(List<string> ids, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/cases/unarchive", new { ids }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteCasesAsync(List<string> ids, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/cases/delete", new { ids }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<JsonElement?> GetMachineAsync(string key, CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<JsonElement>($"admin/machine/{Uri.EscapeDataString(key)}", ct); }
        catch { return null; }
    }

    public async Task<bool> UpdateMachineAsync(string key, string? label, string? notes, string? owner, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/machines/update", new
            {
                machine_key = key,
                machine_label = label,
                notes,
                owner
            }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteMachinesAsync(List<string> ids, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/machines/delete", new { ids }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteBatchesAsync(List<string> ids, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/batches/delete", new { ids }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<JsonElement?> GetHelpRequestsAsync(string? status = null, CancellationToken ct = default)
    {
        try
        {
            var url = "admin/help-requests" + (!string.IsNullOrEmpty(status) ? $"?status={Uri.EscapeDataString(status)}" : "");
            return await _http.GetFromJsonAsync<JsonElement>(url, ct);
        }
        catch { return null; }
    }

    public async Task<bool> UpdateHelpStatusAsync(long id, string status, string? adminNote, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"admin/help-requests/{id}/status", new
            {
                status,
                admin_note = adminNote,
                assigned_to = (string?)null
            }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<JsonElement?> GetAuditsAsync(string? machine = null, string? action = null, CancellationToken ct = default)
    {
        try
        {
            var url = "admin/audit?";
            if (!string.IsNullOrEmpty(machine)) url += $"machine={Uri.EscapeDataString(machine)}&";
            if (!string.IsNullOrEmpty(action)) url += $"action={Uri.EscapeDataString(action)}&";
            return await _http.GetFromJsonAsync<JsonElement>(url.TrimEnd('&', '?'), ct);
        }
        catch { return null; }
    }

    public async Task<bool> LogAuditAsync(string action, string targetId, string note, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/authorization/audit", new
            {
                machine_key = Environment.MachineName,
                action,
                target_id = targetId,
                note
            }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<JsonElement?> ResearchAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/research", new { query }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch { return null; }
    }

    // --- FAZ 3.4 API KEY MANAGEMENT ---
    public async Task<JsonElement?> GetApiKeysAsync(CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<JsonElement>("admin/auth/keys", ct); }
        catch { return null; }
    }

    public async Task<JsonElement?> CreateApiKeyAsync(string name, string role, int? expiresDays = null, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/auth/keys/create", new { name, role, expires_days = expiresDays }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch { return null; }
    }

    public async Task<JsonElement?> RotateApiKeyAsync(string keyId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/auth/keys/rotate", new { key_id = keyId }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch { return null; }
    }

    public async Task<bool> RevokeApiKeyAsync(string keyId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/auth/keys/revoke", new { key_id = keyId }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteApiKeyAsync(string keyId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/auth/keys/delete", new { key_id = keyId }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // --- AI CONFIG & RESEARCH ---
    public async Task<JsonElement?> GetAiConfigAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<JsonElement>("admin/ai-config", ct);
        }
        catch { return null; }
    }

    public async Task<bool> UpdateAiConfigAsync(string? geminiKey, string? model, bool? enableDuckDuckGo, bool? enableWebResearch, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/ai-config", new
            {
                gemini_api_key = string.IsNullOrWhiteSpace(geminiKey) ? null : geminiKey,
                model = string.IsNullOrWhiteSpace(model) ? null : model,
                enable_duckduckgo = enableDuckDuckGo,
                enable_web_research = enableWebResearch
            }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }



    // --- FAZ 3.4 DUAL APPROVALS (R2/R3) ---
    public async Task<JsonElement?> RequestApprovalAsync(string machineKey, string requestedMode, string? reason, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/authorization/request-approval", new
            {
                machine_key = machineKey,
                requested_mode = requestedMode,
                reason
            }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch { return null; }
    }

    public async Task<JsonElement?> GetApprovalsAsync(string? status = null, CancellationToken ct = default)
    {
        try
        {
            var url = "admin/authorization/approvals" + (!string.IsNullOrEmpty(status) ? $"?status={Uri.EscapeDataString(status)}" : "");
            return await _http.GetFromJsonAsync<JsonElement>(url, ct);
        }
        catch { return null; }
    }

    public async Task<JsonElement?> ApproveRequestAsync(string requestId, string? by = null, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/authorization/approve", new { request_id = requestId, by }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch { return null; }
    }

    public async Task<bool> RejectRequestAsync(string requestId, string? by = null, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/authorization/reject", new { request_id = requestId, by }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ValidateApprovalCodeAsync(string machineKey, string mode, string code, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/authorization/validate-code", new { machine_key = machineKey, mode, code }, ct);
            if (!resp.IsSuccessStatusCode) return false;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.TryGetProperty("valid", out var v) && v.GetBoolean();
        }
        catch { return false; }
    }

    // --- FAZ 3.5 SEED & REMOTE REPAIRS ---
    public async Task<bool> SeedDemoDataAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsync("admin/server/seed", null, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<string?> DispatchRemoteRepairAsync(string machineKey, string playbookId, string? initiatedBy = "Admin", string? parameters = null, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("admin/remote-repair/dispatch", new
            {
                machine_key = machineKey,
                playbook_id = playbookId,
                initiated_by = initiatedBy,
                parameters
            }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.TryGetProperty("repair_id", out var r) ? r.GetString() : null;
        }
        catch { return null; }
    }

    public async Task<JsonElement?> GetRemoteRepairsAsync(string? machineKey = null, CancellationToken ct = default)
    {
        try
        {
            var url = "admin/remote-repairs" + (!string.IsNullOrEmpty(machineKey) ? $"?machine_key={Uri.EscapeDataString(machineKey)}" : "");
            return await _http.GetFromJsonAsync<JsonElement>(url, ct);
        }
        catch { return null; }
    }
}
