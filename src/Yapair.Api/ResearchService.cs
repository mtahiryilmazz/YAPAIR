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
            Sen bir Windows destek uzmanÄ±sÄ±n. KullanÄ±cÄ±nÄ±n sorununu ve web arama sonuÃ§larÄ±nÄ± kullanarak
            kÄ±sa, uygulanabilir bir Ã§Ã¶zÃ¼m Ã¶nerisi yaz. TÃ¼rkÃ§e yaz.

            KULLANICI SORUNU: {query}

            WEB ARAMA SONUÃ‡LARI:
            {context}

            YanÄ±t formatÄ±:
            Ã–ZET: (1-2 cÃ¼mle)
            ADIMLAR:
            1. ...
            2. ...
            KOMUT: (varsa tek satÄ±r cmd/powershell, yoksa "yok")
            RÄ°SK: R0/R1/R2/R3
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
            .GetProperty("parts")[0].GetProperty("text").GetString() ?? "(boÅŸ)";
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