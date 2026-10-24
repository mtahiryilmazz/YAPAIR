$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

# 1) ResearchService.cs
$research = @'
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Yapair.Api;

public sealed class ResearchService
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(30)
    }) { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _apiKey;
    private readonly string _model;

    public ResearchService(string apiKey, string model)
    {
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-2.5-flash-lite" : model;
    }

    public async Task<ResearchResult> ResearchAsync(string query, CancellationToken ct = default)
    {
        var results = await SearchDuckDuckGoAsync(query, 5, ct);
        var context = BuildContext(results);
        var prompt = $"""
            Sen bir Windows destek uzmanısın. Kullanıcının sorununu ve web arama sonuçlarını kullanarak
            kısa, uygulanabilir bir çözüm önerisi yaz. Türkçe yaz.

            KULLANICI SORUNU: {query}

            WEB ARAMA SONUÇLARI:
            {context}

            Yanıt formatı:
            ÖZET: (1-2 cümle)
            ADIMLAR:
            1. ...
            2. ...
            KOMUT: (varsa tek satır cmd/powershell, yoksa "yok")
            RİSK: R0/R1/R2/R3
            """;
        var answer = await CallGeminiAsync(prompt, ct);
        return new ResearchResult(query, answer, results);
    }

    private async Task<List<SearchHit>> SearchDuckDuckGoAsync(string query, int max, CancellationToken ct)
    {
        var hits = new List<SearchHit>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://lite.duckduckgo.com/lite/")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["q"] = query })
            };
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Yapair/1.0");
            using var resp = await Http.SendAsync(req, ct);
            var html = await resp.Content.ReadAsStringAsync(ct);

            var titleRx = new Regex("""class=['"]result-link['"][^>]*>([^<]+)</a>""", RegexOptions.IgnoreCase);
            var snippetRx = new Regex("""<td\s+class=['"]result-snippet['"]\s*>\s*(.*?)\s*</td>""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var titles = titleRx.Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()).ToList();
            var snippets = snippetRx.Matches(html).Select(m => StripTags(m.Groups[1].Value)).ToList();

            for (int i = 0; i < Math.Min(Math.Min(titles.Count, snippets.Count), max); i++)
                hits.Add(new SearchHit(titles[i], snippets[i]));
        }
        catch { }
        return hits;
    }

    private async Task<string> CallGeminiAsync(string prompt, CancellationToken ct)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";
        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.3, maxOutputTokens = 1024 }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-goog-api-key", _apiKey);
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return $"Gemini hata: HTTP {(int)resp.StatusCode}";
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content")
            .GetProperty("parts")[0].GetProperty("text").GetString() ?? "(boş)";
    }

    private static string BuildContext(List<SearchHit> hits)
    {
        if (hits.Count == 0) return "(web araması sonuç vermedi)";
        var sb = new StringBuilder();
        for (int i = 0; i < hits.Count; i++)
            sb.AppendLine($"{i + 1}. {hits[i].Title}\n   {hits[i].Snippet}\n");
        return sb.ToString();
    }

    private static string StripTags(string html)
    {
        var noTags = Regex.Replace(html, "<.*?>", " ");
        return WebUtility.HtmlDecode(Regex.Replace(noTags, @"\s+", " ")).Trim();
    }
}

public sealed record SearchHit(string Title, string Snippet);
public sealed record ResearchResult(string Query, string Answer, List<SearchHit> Sources);
'@
[IO.File]::WriteAllText((Join-Path $PWD "src\Yapair.Api\ResearchService.cs"), $research, [Text.UTF8Encoding]::new($false))
Write-Host "ResearchService.cs yazildi" -ForegroundColor Green

# 2) AdminEndpoints.cs'e research endpoint ekle
$adminPath = Join-Path $PWD "src\Yapair.Api\AdminEndpoints.cs"
$admin = Get-Content $adminPath -Raw
if ($admin -notmatch 'admin/research') {
    $route = @'

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
'@
    $admin = $admin -replace '(\s+return app;)', ($route + '$1')

    $helper = @'

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
'@
    $admin = $admin -replace '(\s+private static string Esc\(string\? s\))', ($helper + '$1')

    if ($admin -notmatch 'ResearchRequest') {
        $admin = $admin -replace 'public sealed record IdsRequest\(List<string>\? Ids\);',
            "public sealed record IdsRequest(List<string>? Ids);`r`npublic sealed record ResearchRequest(string Query);"
    }
    Set-Content -Path $adminPath -Value $admin -Encoding UTF8 -NoNewline
    Write-Host "AdminEndpoints.cs guncellendi (research endpoint)" -ForegroundColor Green
} else { Write-Host "AdminEndpoints.cs zaten research iceriyor" -ForegroundColor Yellow }

# 3) Build
dotnet build "src\Yapair.Api\Yapair.Api.csproj" --nologo -v q
Write-Host "API build tamam" -ForegroundColor Cyan