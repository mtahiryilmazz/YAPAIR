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
        _apiKey = apiKey.Trim();
        _model = NormalizeModel(model);
    }

    private static string NormalizeModel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "gemini-1.5-flash";
        var m = raw.Trim().ToLowerInvariant();
        if (m.Contains("1.5-pro") || m == "gemini-pro") return "gemini-1.5-pro";
        if (m.Contains("2.0") || m.Contains("2.5")) return "gemini-2.0-flash";
        if (m.Contains("1.5-flash") || m.Contains("flash")) return "gemini-1.5-flash";
        return "gemini-1.5-flash";
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
        var answer = await CallGeminiWithFallbackAsync(prompt, ct);
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

    private async Task<string> CallGeminiWithFallbackAsync(string prompt, CancellationToken ct)
    {
        // 1. Önce seçilen veya normalize edilen modeli dene
        var (ok, answer, errCode, errDetail) = await TryCallGeminiAsync(_model, prompt, ct);
        if (ok) return answer;

        // 2. Eğer 404 (model bulunamadı) alındıysa en stabil genel model olan 'gemini-1.5-flash' ile fallback dene
        if (errCode == 404 && _model != "gemini-1.5-flash")
        {
            var (fallbackOk, fallbackAns, _, fallbackErr) = await TryCallGeminiAsync("gemini-1.5-flash", prompt, ct);
            if (fallbackOk) return fallbackAns;
            return $"Gemini API Hatası: {fallbackErr}";
        }

        return $"Gemini API Hatası (HTTP {errCode}): {errDetail}";
    }

    private async Task<(bool Success, string Answer, int StatusCode, string ErrorDetail)> TryCallGeminiAsync(string model, string prompt, CancellationToken ct)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";
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
        var statusCode = (int)resp.StatusCode;

        if (!resp.IsSuccessStatusCode)
        {
            var detail = $"HTTP {statusCode}";
            try
            {
                using var errDoc = JsonDocument.Parse(body);
                if (errDoc.RootElement.TryGetProperty("error", out var ep) &&
                    ep.TryGetProperty("message", out var mp))
                {
                    detail = mp.GetString() ?? detail;
                }
            }
            catch { }
            return (false, "", statusCode, detail);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content")
                .GetProperty("parts")[0].GetProperty("text").GetString() ?? "(boş yanıt)";
            return (true, text, statusCode, "");
        }
        catch (Exception ex)
        {
            return (false, "", statusCode, "JSON çözümleme hatası: " + ex.Message);
        }
    }

    private static string BuildContext(List<SearchHit> hits)
    {
        if (hits.Count == 0) return "(web aramasÄ± sonuÃ§ vermedi)";
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