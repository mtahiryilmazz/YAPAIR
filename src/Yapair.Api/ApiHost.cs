using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Hosting.WindowsServices;
using Yapair.Core.Auth;

namespace Yapair.Api;

public static class ApiHost
{
    public static WebApplication BuildApp(string[]? args = null, string? listenUrl = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        if (!Environment.UserInteractive && WindowsServiceHelpers.IsWindowsService())
        {
            builder.Host.UseWindowsService();
        }

        var url = listenUrl ?? Environment.GetEnvironmentVariable("YAPAIR_LISTEN_URL") ?? "http://localhost:5080";
        builder.WebHost.UseUrls(url);

        // Env file loading (safe, non-crashing)
        var envFile = Environment.GetEnvironmentVariable("YAPAIR_ENV_FILE")
            ?? FindEnvFile(builder.Environment.ContentRootPath)
            ?? FindEnvFile(Directory.GetCurrentDirectory())
            ?? FindEnvFile(AppContext.BaseDirectory);

        if (envFile != null && File.Exists(envFile))
        {
            try { EnvLoader.Load(envFile); }
            catch { }
        }

        var supabaseUrl = Environment.GetEnvironmentVariable("YAPAIR_SUPABASE_URL");
        var supabaseKey = Environment.GetEnvironmentVariable("YAPAIR_SUPABASE_SERVICE_ROLE_KEY");
        var apiKey = Environment.GetEnvironmentVariable("YAPAIR_API_KEY") ?? "yap_default_master_key";

        builder.Services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        builder.Services.AddSingleton<ApiKeyManager>();
        builder.Services.AddSingleton<ApprovalManager>();
        builder.Services.AddSingleton<LocalDatabase>();
        builder.Services.AddSingleton<AiConfigManager>();

        builder.Services.AddHttpClient<SupabaseClient>((sp, c) =>
        {
            var isConfigured = !string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey);
            if (isConfigured)
            {
                c.BaseAddress = new Uri(supabaseUrl!.TrimEnd('/') + "/");
                c.DefaultRequestHeaders.Add("apikey", supabaseKey);
                c.DefaultRequestHeaders.Add("Accept", "application/json");
            }
            else
            {
                // Local mode: client will route to LocalDatabase
                c.BaseAddress = new Uri("http://localhost/");
                c.DefaultRequestHeaders.Add("x-local-mode", "true");
            }
        });

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "yapair-api",
            version = "v1",
            mode = !string.IsNullOrWhiteSpace(supabaseUrl) ? "supabase_cloud" : "local_embedded"
        }));

        app.MapPost("/api/v1/cases", async (HttpRequest req, SupabaseClient sb, ApiKeyManager keyMgr, ILogger<WebApplication> log, CancellationToken ct) =>
        {
            var incoming = req.Headers["X-Yapair-Api-Key"].ToString();
            if (string.IsNullOrEmpty(incoming))
            {
                var authHeader = req.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    incoming = authHeader.Substring(7).Trim();
            }

            var (isValid, _, _) = keyMgr.ValidateKey(incoming);
            if (!isValid && (string.IsNullOrEmpty(incoming) || !FixedTimeEquals(incoming, apiKey)))
                return Results.Unauthorized();

            CaseBatch? batch;
            try { batch = await req.ReadFromJsonAsync<CaseBatch>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }

            if (batch is null || batch.Cases.Count == 0) return Results.BadRequest(new { error = "empty_batch" });
            if (string.IsNullOrWhiteSpace(batch.BatchId) || batch.BatchId.Length > 64)
                return Results.BadRequest(new { error = "invalid_batch_id" });
            if (batch.Cases.Count > 1000) return Results.BadRequest(new { error = "batch_too_large", max = 1000 });

            if (await sb.BatchExistsAsync(batch.BatchId, ct))
                return Results.Ok(new { status = "duplicate", batch_id = batch.BatchId, inserted = 0 });

            foreach (var g in batch.Cases.Where(c => !string.IsNullOrWhiteSpace(c.MachineKey)).GroupBy(c => c.MachineKey!))
            {
                await sb.UpsertMachineAsync(new MachineRow
                {
                    MachineKey = g.Key,
                    MachineLabel = g.First().MachineLabel ?? "unknown",
                    LastSeen = DateTimeOffset.UtcNow
                }, ct);
            }

            var rows = batch.Cases.Select(c => new CaseRow
            {
                CaseId = c.CaseId,
                MachineKey = c.MachineKey,
                CreatedAt = c.CreatedAt,
                NormalizedText = c.NormalizedText,
                SymptomCode = c.SymptomCode,
                SymptomLabel = c.SymptomLabel,
                IssueFamily = c.IssueFamily,
                Domain = c.Domain,
                Outcome = c.Outcome,
                PlaybookId = c.PlaybookId,
                UserConfirmation = c.UserConfirmation,
                RawText = c.RawText
            }).ToList();

            var inserted = await sb.InsertCasesAsync(rows, ct);

            await sb.InsertReceiptAsync(new SyncReceipt
            {
                BatchId = batch.BatchId,
                CaseCount = inserted,
                ApiVersion = "v1"
            }, ct);

            log.LogInformation("batch {BatchId} yazildi: {Count}", batch.BatchId, inserted);
            return Results.Ok(new { status = "ok", batch_id = batch.BatchId, inserted });
        });

        app.MapAdminEndpoints();
        return app;
    }

    private static string? FindEnvFile(string startDir)
    {
        try
        {
            var dir = new DirectoryInfo(startDir);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "supabase", ".env.local");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        catch { }
        return null;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
