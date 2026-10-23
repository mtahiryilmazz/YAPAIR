using System.Globalization;
using System.Text.RegularExpressions;

namespace Yapair.Storage;

public sealed record NormalizedSymptom(
    string Code,
    string Label,
    string IssueFamily,
    string Domain,
    string NormalizedText,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<string> MatchedTerms,
    double Confidence);

/// <summary>
/// Deterministic Turkish complaint normalizer. It maps free-text variants to a
/// family (printers, audio, network, ...) plus a specific symptom code, so ten
/// users can describe the same failure with different words.
/// </summary>
public static partial class SymptomNormalizer
{
    private static readonly string[] StopWords =
    [
        "bir", "ve", "bu", "su", "o", "da", "de", "ile", "icin", "için", "ama", "fakat",
        "cok", "çok", "az", "var", "yok", "gibi", "daha", "en", "mi", "mu", "mü", "ise",
        "the", "a", "an", "is", "are", "and", "or", "to", "of", "my", "not",
    ];

    private static readonly SymptomRule[] Rules =
    [
        Rule("printer_offline", "Yazıcı çevrimdışı", "printers", "printers",
            "çevrimdışı", "cevrimdisi", "offline", "bağlı değil", "bagli degil", "ulaşılamıyor", "ulasilamiyor"),
        Rule("printer_not_visible", "Yazıcı görünmüyor", "printers", "printers",
            "görünmüyor", "gorunmuyor", "listelenmiyor", "bulunamıyor", "bulunamiyor", "not visible", "not listed", "missing"),
        Rule("printer_output_failure", "Yazıcı çıktı vermiyor", "printers", "printers",
            "yazdırmıyor", "yazdirmiyor", "çıktı gelmiyor", "cikti gelmiyor", "print etmiyor", "not printing", "no output", "baskı yapmıyor", "baski yapmiyor"),
        Rule("printer_jam", "Kâğıt sıkışması", "printers", "printers",
            "kâğıt sıkışması", "kagit sikismasi", "sıkışma", "sikisma", "paper jam", "jam"),
        Rule("printer_queue", "Yazdırma kuyruğu/Spooler sorunu", "printers", "printers",
            "kuyruk", "queue", "spooler", "biriktirici", "takıldı", "takildi"),
        Rule("printer_driver", "Yazıcı sürücüsü sorunu", "printers", "printers",
            "sürücü", "surucu", "driver", "yazıcı sürücüsü", "yazici surucusu"),
        Rule("printer_generic", "Yazıcı sorunu", "printers", "printers",
            "yazıcı", "yazici", "printer", "yazdırma", "yazdirma"),
        Rule("audio_no_sound", "Ses çıkışı yok", "audio", "audio",
            "ses gelmiyor", "ses yok", "hoparlör", "hoparlor", "audio", "sound"),
        Rule("network_connectivity", "Ağ/İnternet bağlantısı sorunu", "network", "network",
            "internet yok", "internet bağlanmıyor", "internet baglanmiyor", "wi-fi", "wifi", "ağ bağlantısı", "ag baglantisi", "dns", "bağlanmıyor", "baglanmiyor"),
        Rule("uac_prompt", "UAC/yönetici izni sorunu", "security", "security",
            "uac", "yönetici izni", "yonetici izni", "admin", "onay istiyor", "izin istiyor"),
        Rule("windows_update_failure", "Windows Update sorunu", "windows_update", "windows_update",
            "güncelleme", "guncelleme", "update", "windows update"),
        Rule("application_launch_failure", "Uygulama açılmıyor/çöküyor", "applications", "system",
            "program açılmıyor", "program acilmiyor", "uygulama açılmıyor", "uygulama acilmiyor", "çöküyor", "cokuyor", "kapanıyor", "kapaniyor", "launch"),
        Rule("performance_slow", "Sistem yavaş/donuyor", "performance", "system",
            "yavaş", "yavas", "donuyor", "donma", "freeze", "slow", "performans"),
        Rule("storage_low_space", "Disk alanı sorunu", "storage", "storage",
            "disk dolu", "yer yok", "depolama", "disk alanı", "disk alani", "storage", "low disk"),
    ];

    public static NormalizedSymptom Normalize(string? text)
    {
        var normalized = NormalizeForMatch(text);
        var tokens = Tokenize(normalized);

        SymptomRule? bestRule = null;
        var bestMatches = new List<string>();
        var bestScore = 0;

        foreach (var rule in Rules)
        {
            var matches = rule.Phrases
                .Where(phrase => ContainsPhrase(normalized, phrase))
                .ToList();

            if (matches.Count == 0)
            {
                continue;
            }

            var score = (matches.Count * 10) + matches.Max(static phrase => phrase.Length);
            if (score > bestScore)
            {
                bestRule = rule;
                bestMatches = matches;
                bestScore = score;
            }
        }

        if (bestRule is null)
        {
            return new NormalizedSymptom(
                "unknown_symptom",
                "Belirsiz sorun",
                "unknown",
                "unknown",
                string.Join(' ', tokens),
                tokens,
                [],
                0);
        }

        return new NormalizedSymptom(
            bestRule.Code,
            bestRule.Label,
            bestRule.IssueFamily,
            bestRule.Domain,
            string.Join(' ', tokens),
            tokens,
            bestMatches,
            Math.Min(1, bestScore / 30.0));
    }

    public static string NormalizeForMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lowered = text.ToLower(new CultureInfo("tr-TR"))
            .Replace('ı', 'i')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ö', 'o')
            .Replace('ç', 'c')
            .Replace('â', 'a')
            .Replace('î', 'i');

        lowered = Punctuation().Replace(lowered, " ");
        lowered = Whitespace().Replace(lowered, " ").Trim();
        return lowered;
    }

    public static IReadOnlyList<string> Tokenize(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return [];
        }

        return normalizedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(static token => token.Length > 1 && !StopWords.Contains(token, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ContainsPhrase(string normalizedText, string phrase)
    {
        if (normalizedText.Length == 0 || phrase.Length == 0)
        {
            return false;
        }

        return $" {normalizedText} ".Contains($" {phrase} ", StringComparison.Ordinal);
    }

    private static SymptomRule Rule(string code, string label, string family, string domain, params string[] phrases) =>
        new(code, label, family, domain, phrases.Select(NormalizeForMatch).Where(static phrase => phrase.Length > 0).ToArray());

    [GeneratedRegex(@"[^a-z0-9\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex Punctuation();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    private sealed record SymptomRule(
        string Code,
        string Label,
        string IssueFamily,
        string Domain,
        IReadOnlyList<string> Phrases);
}
