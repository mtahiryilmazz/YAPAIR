# fix-api-broken.ps1
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$ts = Get-Date -Format "yyyyMMdd-HHmmss"
foreach ($f in @("src\Yapair.Api\SupabaseClient.cs", "src\Yapair.Api\AdminEndpoints.cs")) {
    if (Test-Path $f) { Move-Item $f "$f.broken-$ts" -Force }
}
Write-Host "Bozuk dosyalar yedeklendi (.broken-$ts)" -ForegroundColor Yellow

$supabase = @'
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

    public SupabaseClient(HttpClient http) => _http = http;

    public async Task<bool> BatchExistsAsync(string batchId, CancellationToken ct = default)
    {
        var url = $"rest/v1/sync_receipts?batch_id=eq.{Uri.EscapeDataString(batchId)}&select=batch_id&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return false;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0;
    }

    public async Task UpsertMachineAsync(MachineRow row, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/machines?on_conflict=machine_key")
        {
            Content = JsonContent.Create(row, options: Json)
        };
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machines upsert", ct);
    }

    public async Task<int> InsertCasesAsync(IReadOnlyList<CaseRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/cases?on_conflict=case_id")
        {
            Content = JsonContent.Create(rows, options: Json)
        };
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "cases insert", ct);
        return rows.Count;
    }

    public async Task InsertReceiptAsync(SyncReceipt receipt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/sync_receipts")
        {
            Content = JsonContent.Create(receipt, options: Json)
        };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "sync_receipts insert", ct);
    }

    public async Task<object> GetAdminStatsAsync(bool includeArchived, CancellationToken ct = default)
    {
        const string caseSelect = "case_id,machine_key,normalized_text,symptom_code,symptom_label,issue_family,domain,outcome,user_confirmation,playbook_id,received_at,archived_at,machines(machine_label)";
        var caseFilter = includeArchived ? "" : "&archived_at=is.null";
        var cases = await QueryTableAsync("cases", caseSelect, "received_at", 100, ct, caseFilter);
        var batches = await QueryTableAsync("sync_receipts", "batch_id,case_count,api_version,received_at", "received_at", 10, ct);
        var machines = await QueryTableAsync("machines", "machine_key,machine_label,first_seen,last_seen,notes,owner", "last_seen", 20, ct);

        return new
        {
            cases = new { total = cases.Total, recent = cases.Rows },
            batches = new { total = batches.Total, recent = batches.Rows },
            machines = new { total = machines.Total, recent = machines.Rows }
        };
    }

    public async Task<Dictionary<string, object?>?> GetCaseAsync(string caseId, CancellationToken ct = default)
    {
        const string select = "case_id,machine_key,normalized_text,symptom_code,symptom_label,issue_family,domain,outcome,user_confirmation,playbook_id,raw_text,received_at,archived_at,machines(machine_label)";
        var url = $"rest/v1/cases?case_id=eq.{Uri.EscapeDataString(caseId)}&select={select}&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"case query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;
        var row = doc.RootElement[0];
        var dict = new Dictionary<string, object?>();
        foreach (var prop in row.EnumerateObject())
            dict[prop.Name] = ConvertElement(prop.Value);
        return dict;
    }

    public async Task<Dictionary<string, object?>?> GetMachineAsync(string machineKey, CancellationToken ct = default)
    {
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
        foreach (var prop in row.EnumerateObject())
            dict[prop.Name] = ConvertElement(prop.Value);
        return dict;
    }

    public async Task UpdateMachineAsync(string machineKey, string? machineLabel, string? notes, string? owner, CancellationToken ct = default)
    {
        var url = $"rest/v1/machines?machine_key=eq.{Uri.EscapeDataString(machineKey)}";
        var body = JsonSerializer.Serialize(new { machine_label = machineLabel, notes, owner }, Json);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machine update", ct);
    }

    public async Task<int> ArchiveCasesAsync(IReadOnlyList<string> caseIds, CancellationToken ct = default)
        => await PatchArchivedAsync(caseIds, DateTimeOffset.UtcNow, ct);

    public async Task<int> UnarchiveCasesAsync(IReadOnlyList<string> caseIds, CancellationToken ct = default)
        => await PatchArchivedAsync(caseIds, null, ct);

    public async Task<int> DeleteCasesAsync(IReadOnlyList<string> caseIds, CancellationToken ct = default)
    {
        if (caseIds.Count == 0) return 0;
        var url = "rest/v1/cases?case_id=in.(" + BuildIdList(caseIds) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "cases delete", ct);
        return caseIds.Count;
    }

    public async Task<int> DeleteMachinesAsync(IReadOnlyList<string> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0) return 0;
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
        var url = "rest/v1/sync_receipts?batch_id=in.(" + BuildIdList(batchIds) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "batches delete", ct);
        return batchIds.Count;
    }

    public async Task InsertAuthAuditAsync(string? machineKey, string? modeBefore, string? modeAfter, string? note, CancellationToken ct = default)
    {
        var row = new { machine_key = machineKey, mode_before = modeBefore, mode_after = modeAfter, note };
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/authorization_audit")
        {
            Content = JsonContent.Create(row, options: Json)
        };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "authorization_audit insert", ct);
    }

    private async Task<int> PatchArchivedAsync(IReadOnlyList<string> caseIds, DateTimeOffset? archivedAt, CancellationToken ct)
    {
        if (caseIds.Count == 0) return 0;
        var url = "rest/v1/cases?case_id=in.(" + BuildIdList(caseIds) + ")";
        var body = JsonSerializer.Serialize(new { archived_at = archivedAt }, Json);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, archivedAt is null ? "cases unarchive" : "cases archive", ct);
        return caseIds.Count;
    }

    private static string BuildIdList(IReadOnlyList<string> ids)
    {
        var escaped = ids.Select(id => "\"" + id.Replace("\"", "") + "\"").ToArray();
        return Uri.EscapeDataString(string.Join(",", escaped));
    }

    private async Task<(int Total, List<Dictionary<string, object?>> Rows)> QueryTableAsync(
        string table, string select, string orderBy, int limit, CancellationToken ct, string extraFilter = "")
    {
        var url = $"rest/v1/{table}?select={select}&order={orderBy}.desc&limit={limit}{extraFilter}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"{table} query failed: HTTP {(int)resp.StatusCode} {body}");

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
            var dict = new Dictionary<string, object?>();
            foreach (var prop in row.EnumerateObject())
                dict[prop.Name] = ConvertElement(prop.Value);
            rows.Add(dict);
        }
        return (total, rows);
    }

    private static object? ConvertElement(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ConvertObject(e),
            JsonValueKind.Array => ConvertArray(e),
            _ => e.ToString()
        };
    }

    private static Dictionary<string, object?> ConvertObject(JsonElement e)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in e.EnumerateObject())
            d[p.Name] = ConvertElement(p.Value);
        return d;
    }

    private static List<object?> ConvertArray(JsonElement e)
    {
        var list = new List<object?>();
        foreach (var item in e.EnumerateArray())
            list.Add(ConvertElement(item));
        return list;
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp, string what, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"{what} basarisiz: HTTP {(int)resp.StatusCode} {body}");
    }
}
'@
[IO.File]::WriteAllText((Join-Path $PWD "src\Yapair.Api\SupabaseClient.cs"), $supabase, [Text.UTF8Encoding]::new($false))
Write-Host "SupabaseClient.cs yazildi ($($supabase.Length) karakter)" -ForegroundColor Green

Write-Host ""
Write-Host "Kontrol:" -ForegroundColor Cyan
Get-Content "src\Yapair.Api\SupabaseClient.cs" -TotalCount 3
Write-Host ""
Write-Host "Simdi AdminEndpoints.cs icin bir sonraki adimi calistir." -ForegroundColor Yellow