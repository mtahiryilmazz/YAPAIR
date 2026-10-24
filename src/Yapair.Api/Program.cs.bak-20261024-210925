using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Yapair.Api;

var builder = WebApplication.CreateBuilder(args);

static string? FindEnvFile(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "supabase", ".env.local");
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}

var envFile = Environment.GetEnvironmentVariable("YAPAIR_ENV_FILE")
    ?? FindEnvFile(builder.Environment.ContentRootPath)
    ?? FindEnvFile(Directory.GetCurrentDirectory())
    ?? FindEnvFile(AppContext.BaseDirectory);

if (envFile is null)
    throw new InvalidOperationException("supabase/.env.local bulunamadi. YAPAIR_ENV_FILE ile mutlak yol ver.");
EnvLoader.Load(envFile);

var supabaseUrl = Environment.GetEnvironmentVariable("YAPAIR_SUPABASE_URL")
    ?? throw new InvalidOperationException("YAPAIR_SUPABASE_URL tanimli degil");
var supabaseKey = Environment.GetEnvironmentVariable("YAPAIR_SUPABASE_SERVICE_ROLE_KEY")
    ?? throw new InvalidOperationException("YAPAIR_SUPABASE_SERVICE_ROLE_KEY tanimli degil");
var apiKey = Environment.GetEnvironmentVariable("YAPAIR_API_KEY")
    ?? throw new InvalidOperationException("YAPAIR_API_KEY tanimli degil");

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddHttpClient<SupabaseClient>(c =>
{
    c.BaseAddress = new Uri(supabaseUrl.TrimEnd('/') + "/");
    c.DefaultRequestHeaders.Add("apikey", supabaseKey);
    // Authorization: Bearer basligi KALDIRILDI.
    // sb_secret_... formatindaki yeni secret key'ler JWT degildir;
    // Authorization basliginda gonderilirse 401 doner.
    c.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "yapair-sync", version = "v1" }));

app.MapPost("/api/v1/cases", async (HttpRequest req, SupabaseClient sb, ILogger<Program> log, CancellationToken ct) =>
{
    var incoming = req.Headers["X-Yapair-Api-Key"].ToString();
    if (string.IsNullOrEmpty(incoming) || !FixedTimeEquals(incoming, apiKey))
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
app.Run();

static bool FixedTimeEquals(string a, string b)
{
    var ba = Encoding.UTF8.GetBytes(a);
    var bb = Encoding.UTF8.GetBytes(b);
    return CryptographicOperations.FixedTimeEquals(ba, bb);
}
