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
