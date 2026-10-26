using System.Text;
using System.Text.Json;
using Yapair.Core.Auth;

namespace Yapair.Api;

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        app.UseStaticFiles();
        app.MapGet("/", () => Results.Redirect("/admin/index.html"));
        app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));
        app.MapGet("/admin/index.html", () =>
        {
            var htmlPath = ResolveAdminHtmlPath();
            if (htmlPath != null && File.Exists(htmlPath))
            {
                return Results.Content(File.ReadAllText(htmlPath), "text/html; charset=utf-8");
            }
            return Results.Content(
                "<!DOCTYPE html><html><head><meta charset='utf-8'><title>YAPAIR Admin</title>" +
                "<style>body{font-family:sans-serif;padding:40px;background:#18181b;color:#f4f4f5}</style></head>" +
                "<body><h2>YAPAIR Admin Web Konsolu</h2><p>index.html dosyası bulunamadı. Lütfen wwwroot klasörünün mevcut olduğundan emin olun.</p></body></html>",
                "text/html; charset=utf-8");
        });

        // --- Core Stats & Detail (ReadOnly+) ---
        app.MapGet("/admin/stats", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, string? archived, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.ReadOnly, out _)) return Results.Unauthorized();
            try
            {
                var includeArchived = string.Equals(archived, "1", StringComparison.OrdinalIgnoreCase);
                var stats = await sb.GetAdminStatsAsync(includeArchived, ct);
                return Results.Ok(stats);
            }
            catch (Exception ex) { return Results.Json(new { error = "stats_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapGet("/admin/case/{id}", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, string id, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.ReadOnly, out _)) return Results.Unauthorized();
            try
            {
                var row = await sb.GetCaseAsync(id, ct);
                return row is null ? Results.NotFound(new { error = "not_found" }) : Results.Ok(row);
            }
            catch (Exception ex) { return Results.Json(new { error = "detail_failed", detail = ex.Message }, statusCode: 500); }
        });

        // --- Case Archive & Mutations (Technician+) ---
        app.MapPost("/admin/cases/archive", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.ArchiveCasesAsync(ids, ct), "archived");
        });

        app.MapPost("/admin/cases/unarchive", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.UnarchiveCasesAsync(ids, ct), "unarchived");
        });

        // --- Case & Resource Deletions (Admin Only) ---
        app.MapPost("/admin/cases/delete", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteCasesAsync(ids, ct), "deleted");
        });

        app.MapPost("/admin/machines/delete", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteMachinesAsync(ids, ct), "deleted");
        });

        app.MapPost("/admin/batches/delete", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteBatchesAsync(ids, ct), "deleted");
        });

        // --- Machine Update (Technician+) ---
        app.MapGet("/admin/machine/{key}", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, string key, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.ReadOnly, out _)) return Results.Unauthorized();
            try
            {
                var row = await sb.GetMachineAsync(key, ct);
                return row is null ? Results.NotFound(new { error = "not_found" }) : Results.Ok(row);
            }
            catch (Exception ex) { return Results.Json(new { error = "machine_detail_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/machines/update", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _)) return Results.Unauthorized();
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

        // --- Help Requests (Public submit, Technician management) ---
        app.MapPost("/api/v1/help-request", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            HelpRequest? body;
            try { body = await req.ReadFromJsonAsync<HelpRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.ProblemText))
                return Results.BadRequest(new { error = "empty_problem_text" });
            try
            {
                var id = await sb.InsertHelpRequestAsync(body.MachineKey, body.MachineLabel, body.ProblemText, body.CaseId, ct);
                return Results.Ok(new { status = "ok", id });
            }
            catch (Exception ex) { return Results.Json(new { error = "help_request_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapGet("/admin/help-requests", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, string? status, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.ReadOnly, out _)) return Results.Unauthorized();
            try
            {
                var rows = await sb.GetHelpRequestsAsync(status, ct);
                return Results.Ok(new { total = rows.Count, items = rows });
            }
            catch (Exception ex) { return Results.Json(new { error = "help_list_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/help-requests/{id}/status", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, long id, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _)) return Results.Unauthorized();
            HelpStatusUpdate? body;
            try { body = await req.ReadFromJsonAsync<HelpStatusUpdate>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.Status))
                return Results.BadRequest(new { error = "empty_status" });
            try
            {
                await sb.UpdateHelpStatusAsync(id, body.Status, body.AdminNote, body.AssignedTo, ct);
                return Results.Ok(new { status = "ok" });
            }
            catch (Exception ex) { return Results.Json(new { error = "help_update_failed", detail = ex.Message }, statusCode: 500); }
        });

        // --- Audit Trails ---
        app.MapPost("/admin/authorization/audit", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _)) return Results.Unauthorized();
            AuthAuditRequest? body;
            try { body = await req.ReadFromJsonAsync<AuthAuditRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null) return Results.BadRequest(new { error = "empty_body" });

            var clientIp = req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            try
            {
                var id = await sb.InsertAuthAuditAsync(
                    body.MachineKey ?? Environment.MachineName,
                    body.ModeBefore,
                    body.ModeAfter,
                    body.Action ?? "mode_change",
                    body.TargetId,
                    clientIp,
                    body.Note,
                    ct);
                return Results.Ok(new { status = "ok", id });
            }
            catch (Exception ex) { return Results.Json(new { error = "audit_insert_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapGet("/admin/audit", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, string? machine, string? action, string? since, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.ReadOnly, out _)) return Results.Unauthorized();
            try
            {
                var rows = await sb.GetAuthAuditsAsync(machine, action, since, ct);
                return Results.Ok(new { total = rows.Count, items = rows });
            }
            catch (Exception ex) { return Results.Json(new { error = "audit_list_failed", detail = ex.Message }, statusCode: 500); }
        });

        // --- AI Configuration & Research (Masked, Secure) ---
        app.MapGet("/admin/ai-config", (HttpRequest req, AiConfigManager aiConfigMgr, ApiKeyManager keyMgr) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _) && !IsLocalhost(req)) return Results.Unauthorized();
            return Results.Ok(aiConfigMgr.GetDto());
        });

        app.MapPost("/admin/ai-config", async (HttpRequest req, AiConfigManager aiConfigMgr, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _) && !IsLocalhost(req)) return Results.Unauthorized();
            AiConfigUpdateRequest? body;
            try { body = await req.ReadFromJsonAsync<AiConfigUpdateRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null) return Results.BadRequest(new { error = "empty_body" });

            aiConfigMgr.UpdateConfig(body);
            return Results.Ok(new { status = "ok", config = aiConfigMgr.GetDto() });
        });

        app.MapGet("/api/v1/ai-config/status", (AiConfigManager aiConfigMgr) =>
        {
            var dto = aiConfigMgr.GetDto();
            return Results.Ok(new
            {
                has_key = dto.HasKey,
                model = dto.Model,
                enable_duckduckgo = dto.EnableDuckDuckGo,
                enable_web_research = dto.EnableWebResearch
            });
        });

        app.MapPost("/api/v1/research", async (HttpRequest req, AiConfigManager aiConfigMgr, CancellationToken ct) =>
        {
            ResearchRequest? body;
            try { body = await req.ReadFromJsonAsync<ResearchRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.Query)) return Results.BadRequest(new { error = "empty_query" });

            var geminiKey = aiConfigMgr.GetEffectiveApiKey();
            if (string.IsNullOrEmpty(geminiKey))
            {
                var env = LoadEnvFile();
                env.TryGetValue("GEMINI_API_KEY", out geminiKey);
            }

            var cfg = aiConfigMgr.GetConfig();
            if (string.IsNullOrEmpty(geminiKey))
            {
                return Results.Ok(new ResearchResult(
                    body.Query,
                    "⚠️ Gemini API anahtarı henüz tanımlanmamış.\nAdmin panelinden (AI Ayarları) Gemini API anahtarınızı tanımladığınızda internet destekli akıllı çözümler otomatik olarak burada görünecektir.",
                    new List<SearchHit>()));
            }

            var model = string.IsNullOrWhiteSpace(cfg.Model) ? "gemini-2.5-flash" : cfg.Model;
            var svc = new ResearchService(geminiKey, model);
            try
            {
                var result = await svc.ResearchAsync(body.Query, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "research_failed", detail = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/admin/research", async (HttpRequest req, AiConfigManager aiConfigMgr, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _) && !IsLocalhost(req)) return Results.Unauthorized();
            ResearchRequest? body;
            try { body = await req.ReadFromJsonAsync<ResearchRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.Query)) return Results.BadRequest(new { error = "empty_query" });

            var geminiKey = aiConfigMgr.GetEffectiveApiKey();
            if (string.IsNullOrEmpty(geminiKey))
            {
                var env = LoadEnvFile();
                env.TryGetValue("GEMINI_API_KEY", out geminiKey);
            }

            if (string.IsNullOrEmpty(geminiKey))
                return Results.BadRequest(new { error = "gemini_key_missing", message = "Gemini API anahtarı ayarlanmamış. Lütfen Admin panelinden girin." });

            var cfg = aiConfigMgr.GetConfig();
            var model = string.IsNullOrWhiteSpace(cfg.Model) ? "gemini-2.5-flash" : cfg.Model;
            var svc = new ResearchService(geminiKey, model);
            try { return Results.Ok(await svc.ResearchAsync(body.Query, ct)); }
            catch (Exception ex) { return Results.Json(new { error = "research_failed", detail = ex.Message }, statusCode: 500); }
        });

        // --- Print & Test ---
        app.MapGet("/admin/print", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, string? id, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.ReadOnly, out _)) return Results.Unauthorized();
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

        // ==========================================
        // FAZ 3.4: API KEY MANAGEMENT (Admin Only)
        // ==========================================
        app.MapGet("/admin/auth/keys", (HttpRequest req, ApiKeyManager keyMgr) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _)) return Results.Unauthorized();
            return Results.Ok(keyMgr.ListKeys());
        });

        app.MapPost("/admin/auth/keys/create", async (HttpRequest req, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _)) return Results.Unauthorized();
            CreateKeyRequest? body;
            try { body = await req.ReadFromJsonAsync<CreateKeyRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "empty_name" });

            var role = UserRoleExtensions.ParseRole(body.Role);
            var lifetime = body.ExpiresDays.HasValue && body.ExpiresDays.Value > 0
                ? TimeSpan.FromDays(body.ExpiresDays.Value)
                : (TimeSpan?)null;

            var (rec, rawKey) = keyMgr.CreateKey(body.Name.Trim(), role, lifetime);
            return Results.Ok(new { status = "ok", key = rec, raw_key = rawKey });
        });

        app.MapPost("/admin/auth/keys/rotate", async (HttpRequest req, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _)) return Results.Unauthorized();
            KeyActionRequest? body;
            try { body = await req.ReadFromJsonAsync<KeyActionRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.KeyId))
                return Results.BadRequest(new { error = "empty_key_id" });

            var result = keyMgr.RotateKey(body.KeyId);
            if (result == null) return Results.NotFound(new { error = "key_not_found" });

            var (newRec, newRaw) = result.Value;
            return Results.Ok(new { status = "ok", key = newRec, raw_key = newRaw });
        });

        app.MapPost("/admin/auth/keys/revoke", async (HttpRequest req, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _) && !IsLocalhost(req)) return Results.Unauthorized();
            KeyActionRequest? body;
            try { body = await req.ReadFromJsonAsync<KeyActionRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.KeyId))
                return Results.BadRequest(new { error = "empty_key_id" });

            var ok = keyMgr.RevokeKey(body.KeyId);
            return ok ? Results.Ok(new { status = "ok" }) : Results.NotFound(new { error = "key_not_found" });
        });

        app.MapPost("/admin/auth/keys/delete", async (HttpRequest req, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out _) && !IsLocalhost(req)) return Results.Unauthorized();
            KeyActionRequest? body;
            try { body = await req.ReadFromJsonAsync<KeyActionRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.KeyId))
                return Results.BadRequest(new { error = "empty_key_id" });

            var ok = keyMgr.DeleteKey(body.KeyId);
            return ok ? Results.Ok(new { status = "ok" }) : Results.NotFound(new { error = "key_not_found" });
        });

        // ==========================================
        // FAZ 3.4: R2/R3 DUAL APPROVALS
        // ==========================================
        app.MapPost("/admin/authorization/request-approval", async (HttpRequest req, ApprovalManager apprMgr, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _)) return Results.Unauthorized();
            ApprovalCreateRequest? body;
            try { body = await req.ReadFromJsonAsync<ApprovalCreateRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.MachineKey) || string.IsNullOrWhiteSpace(body.RequestedMode))
                return Results.BadRequest(new { error = "missing_parameters" });

            var record = apprMgr.CreateRequest(body.MachineKey.Trim(), body.RequestedMode.Trim(), body.Reason);
            return Results.Ok(new { status = "ok", request = record });
        });

        app.MapGet("/admin/authorization/approvals", (HttpRequest req, ApprovalManager apprMgr, ApiKeyManager keyMgr, string? status) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _)) return Results.Unauthorized();
            var list = apprMgr.ListRequests(status);
            return Results.Ok(new { total = list.Count, items = list });
        });

        app.MapPost("/admin/authorization/approve", async (HttpRequest req, ApprovalManager apprMgr, ApiKeyManager keyMgr, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out var callerRole)) return Results.Unauthorized();
            ApprovalActionRequest? body;
            try { body = await req.ReadFromJsonAsync<ApprovalActionRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.RequestId))
                return Results.BadRequest(new { error = "missing_request_id" });

            var approver = string.IsNullOrWhiteSpace(body.By) ? callerRole.ToRoleString() : body.By.Trim();
            var approved = apprMgr.Approve(body.RequestId, approver);
            if (approved == null) return Results.NotFound(new { error = "request_not_found" });

            // Audit
            try
            {
                await sb.InsertAuthAuditAsync(
                    approved.MachineKey,
                    null,
                    approved.RequestedMode,
                    "second_approval_granted",
                    approved.Id,
                    req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    $"Onaylayan: {approver}, Kod: {approved.ApprovalCode}, Neden: {approved.Reason}",
                    ct);
            }
            catch { }

            return Results.Ok(new { status = "ok", request = approved, code = approved.ApprovalCode });
        });

        app.MapPost("/admin/authorization/reject", async (HttpRequest req, ApprovalManager apprMgr, ApiKeyManager keyMgr, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Admin, out var callerRole)) return Results.Unauthorized();
            ApprovalActionRequest? body;
            try { body = await req.ReadFromJsonAsync<ApprovalActionRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.RequestId))
                return Results.BadRequest(new { error = "missing_request_id" });

            var rejectedBy = string.IsNullOrWhiteSpace(body.By) ? callerRole.ToRoleString() : body.By.Trim();
            var rejected = apprMgr.Reject(body.RequestId, rejectedBy);
            if (rejected == null) return Results.NotFound(new { error = "request_not_found" });

            try
            {
                await sb.InsertAuthAuditAsync(
                    rejected.MachineKey,
                    null,
                    rejected.RequestedMode,
                    "second_approval_rejected",
                    rejected.Id,
                    req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    $"Reddeden: {rejectedBy}",
                    ct);
            }
            catch { }

            return Results.Ok(new { status = "ok", request = rejected });
        });

        app.MapPost("/admin/authorization/validate-code", async (HttpRequest req, ApprovalManager apprMgr, CancellationToken ct) =>
        {
            ValidateApprovalCodeRequest? body;
            try { body = await req.ReadFromJsonAsync<ValidateApprovalCodeRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.MachineKey) || string.IsNullOrWhiteSpace(body.Mode) || string.IsNullOrWhiteSpace(body.Code))
                return Results.BadRequest(new { error = "missing_parameters" });

            var valid = apprMgr.ValidateApprovalCode(body.MachineKey, body.Mode, body.Code);
            return Results.Ok(new { valid });
        });

        // ==========================================
        // FAZ 3.5: SEED DATA & REMOTE REPAIR
        // ==========================================
        app.MapPost("/admin/server/seed", (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out _) && !IsLocalhost(req)) return Results.Unauthorized();
            sb.SeedDemoData(true);
            return Results.Ok(new { status = "ok", message = "Demo verileri başarıyla yüklendi." });
        });

        app.MapPost("/admin/remote-repair/dispatch", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.Technician, out var callerRole) && !IsLocalhost(req)) return Results.Unauthorized();
            RemoteRepairDispatchRequest? body;
            try { body = await req.ReadFromJsonAsync<RemoteRepairDispatchRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.MachineKey) || string.IsNullOrWhiteSpace(body.PlaybookId))
                return Results.BadRequest(new { error = "missing_parameters" });

            var actor = callerRole.ToRoleString();
            var repairId = await sb.DispatchRemoteRepairAsync(body.MachineKey, body.PlaybookId, body.InitiatedBy ?? actor, body.Parameters, ct);
            return Results.Ok(new { status = "ok", repair_id = repairId });
        });

        app.MapGet("/api/v1/remote-repair/pending", async (HttpRequest req, SupabaseClient sb, string? machine_key, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(machine_key)) return Results.BadRequest(new { error = "missing_machine_key" });
            var items = await sb.GetPendingRemoteRepairsAsync(machine_key, ct);
            return Results.Ok(new { count = items.Count, items });
        });

        app.MapPost("/api/v1/remote-repair/result", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            RemoteRepairResultRequest? body;
            try { body = await req.ReadFromJsonAsync<RemoteRepairResultRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.RepairId) || string.IsNullOrWhiteSpace(body.Status))
                return Results.BadRequest(new { error = "missing_parameters" });

            var ok = await sb.UpdateRemoteRepairResultAsync(body.RepairId, body.Status, body.Output, ct);
            return ok ? Results.Ok(new { status = "ok" }) : Results.NotFound(new { error = "repair_not_found" });
        });

        app.MapGet("/admin/remote-repairs", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, string? machine_key, CancellationToken ct) =>
        {
            if (!Authorize(req, keyMgr, UserRole.ReadOnly, out _) && !IsLocalhost(req)) return Results.Unauthorized();
            var items = await sb.GetRemoteRepairsAsync(machine_key, ct);
            return Results.Ok(new { count = items.Count, items });
        });

        return app;
    }

    private static UserRole? GetCallerRole(HttpRequest req, ApiKeyManager keyMgr)
    {
        var authHeader = req.Headers["Authorization"].ToString();
        var apiKeyHeader = req.Headers["X-Yapair-Api-Key"].ToString();
        string? token = null;

        if (!string.IsNullOrEmpty(apiKeyHeader)) token = apiKeyHeader;
        else if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = authHeader.Substring(7).Trim();

        if (!string.IsNullOrEmpty(token))
        {
            var (valid, role, _) = keyMgr.ValidateKey(token);
            if (valid) return role;
            return null; // Explicit invalid token
        }

        // Loopback local dev fallback: full admin
        if (IsLocalhost(req)) return UserRole.Admin;
        return null;
    }

    private static bool Authorize(HttpRequest req, ApiKeyManager keyMgr, UserRole requiredRole, out UserRole callerRole)
    {
        var role = GetCallerRole(req, keyMgr);
        if (role.HasValue && role.Value.HasPermission(requiredRole))
        {
            callerRole = role.Value;
            return true;
        }
        callerRole = UserRole.ReadOnly;
        return false;
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

    private static string? ResolveAdminHtmlPath()
    {
        var p1 = Path.Combine(AppContext.BaseDirectory, "wwwroot", "admin", "index.html");
        if (File.Exists(p1)) return p1;

        var p2 = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(p2)) return p2;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c1 = Path.Combine(dir.FullName, "src", "Yapair.Api", "wwwroot", "admin", "index.html");
            if (File.Exists(c1)) return c1;

            var c2 = Path.Combine(dir.FullName, "publish", "admin", "wwwroot", "admin", "index.html");
            if (File.Exists(c2)) return c2;

            var c3 = Path.Combine(dir.FullName, "wwwroot", "admin", "index.html");
            if (File.Exists(c3)) return c3;

            dir = dir.Parent;
        }

        return null;
    }
}

public sealed record AdminTestRequest(string? SymptomCode, string? MachineLabel);
public sealed record IdsRequest(List<string>? Ids);
public sealed record ResearchRequest(string Query);
public sealed record MachineUpdateRequest(string MachineKey, string? MachineLabel, string? Notes, string? Owner);
public sealed record AuthAuditRequest(string? MachineKey, string? ModeBefore, string? ModeAfter, string? Action, string? TargetId, string? Note);
public sealed record HelpRequest(string? MachineKey, string? MachineLabel, string? CaseId, string ProblemText);
public sealed record HelpStatusUpdate(string Status, string? AdminNote, string? AssignedTo);
public sealed record CreateKeyRequest(string Name, string Role, int? ExpiresDays);
public sealed record KeyActionRequest(string KeyId);
public sealed record ApprovalCreateRequest(string MachineKey, string RequestedMode, string? Reason);
public sealed record ApprovalActionRequest(string RequestId, string? By);
public sealed record ValidateApprovalCodeRequest(string MachineKey, string Mode, string Code);
public sealed record RemoteRepairDispatchRequest(string MachineKey, string PlaybookId, string? InitiatedBy, string? Parameters);
public sealed record RemoteRepairResultRequest(string RepairId, string Status, string? Output);
