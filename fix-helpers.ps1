$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

# ---------- AuditLogger.cs ----------
@'
using System.IO;
using System.Text.Json;

namespace Yapair.Desktop.WinUI;

public static class AuditLogger
{
    private static readonly string LocalLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YAPAIR", "audit.log");

    public static async Task LogModeChangeAsync(string repoRoot, string modeBefore, string modeAfter)
    {
        var entry = new
        {
            ts = DateTimeOffset.UtcNow.ToString("O"),
            machine = Environment.MachineName,
            mode_before = modeBefore,
            mode_after = modeAfter
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LocalLogPath)!);
            File.AppendAllText(LocalLogPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch { }

        try
        {
            var env = LoadEnv(repoRoot);
            if (!env.TryGetValue("YAPAIR_API_KEY", out var apiKey)) return;
            var baseUrl = env.TryGetValue("YAPAIR_API_BASE_URL", out var u) && !string.IsNullOrEmpty(u)
                ? u : "http://localhost:5080";

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var payload = JsonSerializer.Serialize(new
            {
                machine_key = Environment.MachineName,
                mode_before = modeBefore,
                mode_after = modeAfter,
                note = "UI mode change"
            });
            using var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            content.Headers.Add("X-Yapair-Api-Key", apiKey);
            await http.PostAsync(baseUrl.TrimEnd('/') + "/admin/authorization/audit", content);
        }
        catch { }
    }

    private static Dictionary<string, string> LoadEnv(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "supabase", ".env.local");
        var dict = new Dictionary<string, string>();
        if (!File.Exists(path)) return dict;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var i = line.IndexOf('=');
            if (i < 1) continue;
            dict[line[..i].Trim()] = line[(i + 1)..].Trim().Trim('"').Trim('\'');
        }
        return dict;
    }
}
'@ | Set-Content "src\Yapair.Desktop.WinUI\AuditLogger.cs" -Encoding UTF8
Write-Host "AuditLogger.cs yazildi" -ForegroundColor Green

# ---------- ResearchCache.cs ----------
@'
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
'@ | Set-Content "src\Yapair.Desktop.WinUI\ResearchCache.cs" -Encoding UTF8
Write-Host "ResearchCache.cs yazildi" -ForegroundColor Green

# ---------- Dogrulama ----------
Write-Host ""
Write-Host "Dogrulama:" -ForegroundColor Cyan
$files = @("AuditLogger.cs","ResearchCache.cs","MainWindow.xaml","MainWindow.xaml.cs",
           "App.xaml.cs","App.xaml","ChatMessage.cs","CliBridge.cs","IntentDetector.cs","RelayCommand.cs")
foreach ($f in $files) {
    $path = "src\Yapair.Desktop.WinUI\$f"
    $exists = Test-Path $path
    $size = if ($exists) { (Get-Item $path).Length } else { 0 }
    $color = if ($exists -and $size -gt 0) { "Green" } else { "Red" }
    Write-Host ("  {0,-22} {1,8} byte" -f $f, $size) -ForegroundColor $color
}

# ---------- Build ----------
Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan