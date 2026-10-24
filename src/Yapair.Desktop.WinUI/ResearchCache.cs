using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Yapair.Desktop.WinUI;

public static class ResearchCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YAPAIR", "research-cache");

    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private static readonly object Lock = new();
    private static DateTime _lastRequest = DateTime.MinValue;
    private static readonly Queue<DateTime> _recentRequests = new();

    public static bool TryGetCached(string query, out string? answer)
    {
        answer = null;
        try
        {
            var path = CachePath(query);
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var cachedAt = doc.RootElement.GetProperty("cached_at").GetDateTimeOffset();
            if (DateTimeOffset.UtcNow - cachedAt > Ttl) return false;
            answer = doc.RootElement.GetProperty("answer").GetString();
            return !string.IsNullOrEmpty(answer);
        }
        catch { return false; }
    }

    public static void Save(string query, string answer)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var path = CachePath(query);
            var payload = new
            {
                query,
                answer,
                cached_at = DateTimeOffset.UtcNow.ToString("O")
            };
            File.WriteAllText(path, JsonSerializer.Serialize(payload));
        }
        catch { }
    }

    public static async Task<bool> WaitIfNeededAsync(CancellationToken ct = default)
    {
        int count;
        DateTime last;
        lock (Lock)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-60);
            while (_recentRequests.Count > 0 && _recentRequests.Peek() < cutoff)
                _recentRequests.Dequeue();
            count = _recentRequests.Count;
            last = _lastRequest;
        }

        if (count >= 14) return false;

        var sinceLast = DateTime.UtcNow - last;
        if (sinceLast < TimeSpan.FromSeconds(4))
            await Task.Delay(TimeSpan.FromSeconds(4) - sinceLast, ct);

        lock (Lock)
        {
            _lastRequest = DateTime.UtcNow;
            _recentRequests.Enqueue(_lastRequest);
        }
        return true;
    }

    private static string CachePath(string query)
    {
        var norm = query.ToLowerInvariant().Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(norm)))[..32];
        return Path.Combine(CacheDir, hash + ".json");
    }
}
