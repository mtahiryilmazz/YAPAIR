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
        app.MapPost("/admin/research", async (HttpRequest req, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            var env = LoadEnvFile();
            if (!env.TryGetValue("GEMINI_API_KEY", out var geminiKey) || string.IsNullOrEmpty(geminiKey))
                return Results.BadRequest(new { error = "gemini_key_missing" });
            ResearchRequest? body;
            try { body = await req.ReadFromJsonAsync<ResearchRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.Query)) return Results.BadRequest(new { error = "empty_query" });
            var model = env.TryGetValue("GEMINI_MODEL", out var m) ? m : "gemini-2.5-flash-lite";
            var svc = new ResearchService(geminiKey, model);
            try { return Results.Ok(await svc.ResearchAsync(body.Query, ct)); }
            catch (Exception ex) { return Results.Json(new { error = "research_failed", detail = ex.Message }, statusCode: 500); }
        });
        app.MapGet("/admin/machine/{key}", async (HttpRequest req, SupabaseClient sb, string key, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            try
            {
                var row = await sb.GetMachineAsync(key, ct);
                return row is null ? Results.NotFound(new { error = "not_found" }) : Results.Ok(row);
            }
            catch (Exception ex) { return Results.Json(new { error = "machine_detail_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/machines/update", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            MachineUpdateRequest? body;
            try { body = await req.ReadFromJsonAsync<MachineUpdateRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrEmpty(body.MachineKey))
                return Results.BadRequest(new { error = "missing_machine_key" });
            try
            {
                await sb.UpdateMachineAsync(body.MachineKey, body.MachineLabel, body.Notes, body.Owner, ct);
                return Results.Ok(new { status = "ok" });
            }
            catch (Exception ex) { return Results.Json(new { error = "update_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/machines/delete", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteMachinesAsync(ids, ct), "deleted");
        });

        app.MapPost("/admin/batches/delete", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteBatchesAsync(ids, ct), "deleted");
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
    private static Dictionary<string, string> LoadEnvFile()
    {
        var dict = new Dictionary<string, string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var p = Path.Combine(dir.FullName, "supabase", ".env.local");
            if (File.Exists(p))
            {
                foreach (var raw in File.ReadAllLines(p))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    var i = line.IndexOf('=');
                    if (i < 1) continue;
                    dict[line[..i].Trim()] = line[(i + 1)..].Trim().Trim('"').Trim('\'');
                }
                return dict;
            }
            dir = dir.Parent;
        }
        return dict;
    }
    private static string Esc(string? s) => string.IsNullOrEmpty(s) ? "" : System.Net.WebUtility.HtmlEncode(s);

    private static bool IsLocalhost(HttpRequest req)
    {
        var ip = req.HttpContext.Connection.RemoteIpAddress;
        return ip is null || System.Net.IPAddress.IsLoopback(ip);
    }
}

public sealed record AdminTestRequest(string? SymptomCode, string? MachineLabel);
public sealed record IdsRequest(List<string>? Ids);
public sealed record ResearchRequest(string Query);
public sealed record MachineUpdateRequest(string MachineKey, string? MachineLabel, string? Notes, string? Owner);
public sealed record AuthAuditRequest(string? MachineKey, string? ModeBefore, string? ModeAfter, string? Note);
