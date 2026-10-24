$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

# ============ SupabaseClient.cs ============
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
        { Content = JsonContent.Create(row, options: Json) };
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machines upsert", ct);
    }

    public async Task<int> InsertCasesAsync(IReadOnlyList<CaseRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/cases?on_conflict=case_id")
        { Content = JsonContent.Create(rows, options: Json) };
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "cases insert", ct);
        return rows.Count;
    }

    public async Task InsertReceiptAsync(SyncReceipt receipt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/sync_receipts")
        { Content = JsonContent.Create(receipt, options: Json) };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "sync_receipts insert", ct);
    }

    public async Task<object> GetAdminStatsAsync(bool includeArchived, CancellationToken ct = default)
    {
        const string caseSelect = "case_id,machine_key,normalized_text,symptom_code,symptom_label,issue_family,domain,outcome,user_confirmation,playbook_id,received_at,archived_at,machines(machine_label)";
        var filter = includeArchived ? "" : "&archived_at=is.null";
        var cases = await QueryTableAsync("cases", caseSelect, "received_at", 100, ct, filter);
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
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"case query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
        var row = doc.RootElement[0];
        var dict = new Dictionary<string, object?>();
        foreach (var p in row.EnumerateObject()) dict[p.Name] = ConvertElement(p.Value);
        return dict;
    }

    public async Task<int> ArchiveCasesAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
        => await PatchArchivedAsync(ids, DateTimeOffset.UtcNow, ct);

    public async Task<int> UnarchiveCasesAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
        => await PatchArchivedAsync(ids, null, ct);

    public async Task<int> DeleteCasesAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return 0;
        var url = "rest/v1/cases?case_id=in.(" + BuildIdList(ids) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "cases delete", ct);
        return ids.Count;
    }

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
'@

# ============ AdminEndpoints.cs ============
$admin = @'
using System.Text;
using System.Text.Json;

namespace Yapair.Api;

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        app.UseStaticFiles();
        app.MapGet("/", () => Results.Redirect("/admin/index.html"));

        app.MapGet("/admin/stats", async (HttpRequest req, SupabaseClient sb, string? archived, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            try
            {
                var includeArchived = string.Equals(archived, "1", StringComparison.OrdinalIgnoreCase);
                var stats = await sb.GetAdminStatsAsync(includeArchived, ct);
                return Results.Ok(stats);
            }
            catch (Exception ex) { return Results.Json(new { error = "stats_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapGet("/admin/case/{id}", async (HttpRequest req, SupabaseClient sb, string id, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            try
            {
                var row = await sb.GetCaseAsync(id, ct);
                return row is null ? Results.NotFound(new { error = "not_found" }) : Results.Ok(row);
            }
            catch (Exception ex) { return Results.Json(new { error = "detail_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/cases/archive", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.ArchiveCasesAsync(ids, ct), "archived");
        });

        app.MapPost("/admin/cases/unarchive", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.UnarchiveCasesAsync(ids, ct), "unarchived");
        });

        app.MapPost("/admin/cases/delete", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteCasesAsync(ids, ct), "deleted");
        });

        app.MapGet("/admin/print", async (HttpRequest req, SupabaseClient sb, string? id, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            try
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var row = await sb.GetCaseAsync(id, ct);
                    if (row is null) return Results.NotFound();
                    return Results.Content(BuildPrintHtml(new[] { row }, true), "text/html; charset=utf-8");
                }
                return Results.BadRequest(new { error = "missing_id" });
            }
            catch (Exception ex) { return Results.Json(new { error = "print_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/test-send", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            AdminTestRequest? body;
            try { body = await req.ReadFromJsonAsync<AdminTestRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null) return Results.BadRequest(new { error = "empty_body" });
            var symptomCode = body.SymptomCode ?? "printer_offline";
            var caseId = "gui-" + Guid.NewGuid().ToString("N")[..12];
            var batchId = "gui-batch-" + Guid.NewGuid().ToString("N")[..12];
            var now = DateTimeOffset.UtcNow;
            try
            {
                await sb.UpsertMachineAsync(new MachineRow { MachineKey = "gui-test-machine", MachineLabel = body.MachineLabel ?? "GUI-TEST-PC", LastSeen = now }, ct);
                var rows = new List<CaseRow> {
                    new() { CaseId = caseId, MachineKey = "gui-test-machine", CreatedAt = now,
                        NormalizedText = symptomCode.Replace('_', ' '), SymptomCode = symptomCode,
                        SymptomLabel = symptomCode.Replace('_', ' '), IssueFamily = "printers",
                        Domain = "devices", Outcome = "resolved",
                        PlaybookId = "printers.spooler.restart.v1", UserConfirmation = "resolved" }
                };
                var inserted = await sb.InsertCasesAsync(rows, ct);
                await sb.InsertReceiptAsync(new SyncReceipt { BatchId = batchId, CaseCount = inserted, ApiVersion = "v1-admin" }, ct);
                return Results.Ok(new { status = "ok", batch_id = batchId, case_id = caseId, inserted });
            }
            catch (Exception ex) { return Results.Json(new { error = "send_failed", detail = ex.Message }, statusCode: 500); }
        });

        return app;
    }

    private static async Task<IResult> HandleIdsAction(HttpRequest req, Func<IReadOnlyList<string>, Task<int>> action, string verb)
    {
        IdsRequest? body;
        try { body = await req.ReadFromJsonAsync<IdsRequest>(); }
        catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
        if (body?.Ids is null || body.Ids.Count == 0) return Results.BadRequest(new { error = "empty_ids" });
        if (body.Ids.Count > 500) return Results.BadRequest(new { error = "too_many_ids", max = 500 });
        try
        {
            var n = await action(body.Ids);
            return Results.Ok(new Dictionary<string, object> { ["status"] = "ok", [verb] = n });
        }
        catch (Exception ex) { return Results.Json(new { error = "action_failed", detail = ex.Message }, statusCode: 500); }
    }

    private static string BuildPrintHtml(IEnumerable<Dictionary<string, object?>> rows, bool detail)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>YAPAIR Rapor</title>");
        sb.AppendLine("<style>body{font-family:system-ui;margin:24px;color:#111}h1{font-size:20px}table{width:100%;border-collapse:collapse;font-size:11px}th,td{border:1px solid #ccc;padding:6px;text-align:left}th{background:#f0f0f0}.detail{display:grid;grid-template-columns:180px 1fr;gap:6px 14px}.detail dt{font-weight:600}</style></head><body>");
        sb.AppendLine("<h1>YAPAIR Vaka Raporu</h1>");
        var list = rows.ToList();
        if (detail && list.Count == 1)
        {
            var r = list[0];
            sb.AppendLine("<dl class=\"detail\">");
            foreach (var k in new[] { "case_id", "machine_key", "normalized_text", "symptom_label", "symptom_code", "issue_family", "domain", "playbook_id", "outcome", "user_confirmation", "received_at", "archived_at" })
                sb.AppendLine($"<dt>{Esc(k)}</dt><dd>{Esc(Str(r, k))}</dd>");
            sb.AppendLine("</dl>");
        }
        sb.AppendLine("<script>if(new URLSearchParams(location.search).get('auto')==='1')setTimeout(()=>window.print(),300)</script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string? Str(Dictionary<string, object?> r, string k) => r.TryGetValue(k, out var v) ? v?.ToString() : null;
    private static string Esc(string? s) => string.IsNullOrEmpty(s) ? "" : System.Net.WebUtility.HtmlEncode(s);

    private static bool IsLocalhost(HttpRequest req)
    {
        var ip = req.HttpContext.Connection.RemoteIpAddress;
        return ip is null || System.Net.IPAddress.IsLoopback(ip);
    }
}

public sealed record AdminTestRequest(string? SymptomCode, string? MachineLabel);
public sealed record IdsRequest(List<string>? Ids);
'@

# ============ Yaz + build ============
[IO.File]::WriteAllText((Join-Path $PWD "src\Yapair.Api\SupabaseClient.cs"), $supabase, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $PWD "src\Yapair.Api\AdminEndpoints.cs"), $admin, [Text.UTF8Encoding]::new($false))

Write-Host "SupabaseClient.cs : $($supabase.Length) char" -ForegroundColor Green
Write-Host "AdminEndpoints.cs : $($admin.Length) char" -ForegroundColor Green
Write-Host ""
Get-Content "src\Yapair.Api\SupabaseClient.cs" -TotalCount 3
Get-Content "src\Yapair.Api\AdminEndpoints.cs" -TotalCount 3
Write-Host ""
Write-Host "Build ediliyor..." -ForegroundColor Yellow
dotnet build "src\Yapair.Api\Yapair.Api.csproj" --nologo -v q