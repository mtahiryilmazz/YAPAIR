using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yapair.Sync;

public sealed class SyncClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public SyncClient(string baseUrl, string apiKey)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _apiKey = apiKey;
    }

    public async Task<SyncResult> SendBatchAsync(IReadOnlyList<OutboxCase> cases, CancellationToken ct = default)
    {
        var batchId = Guid.NewGuid().ToString("N");
        var payload = new
        {
            batch_id = batchId,
            cases = cases.Select(c => new
            {
                case_id = c.CaseId,
                machine_key = c.MachineKey,
                machine_label = c.MachineLabel,
                created_at = c.CreatedAt,
                normalized_text = c.NormalizedText,
                symptom_code = c.SymptomCode,
                symptom_label = c.SymptomLabel,
                issue_family = c.IssueFamily,
                domain = c.Domain,
                outcome = c.Outcome,
                playbook_id = c.PlaybookId,
                user_confirmation = c.UserConfirmation,
                raw_text = c.RawText
            }).ToArray()
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/cases")
        {
            Content = JsonContent.Create(payload, options: Json)
        };
        req.Headers.Add("X-Yapair-Api-Key", _apiKey);

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return new SyncResult(false, batchId, 0, $"HTTP {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var inserted = doc.RootElement.TryGetProperty("inserted", out var el) ? el.GetInt32() : 0;
        var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "ok" : "ok";
        return new SyncResult(true, batchId, inserted, null, status);
    }
}

public sealed record SyncResult(bool Ok, string BatchId, int Inserted, string? Error, string Status = "ok");
