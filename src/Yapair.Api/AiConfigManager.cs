using System.Text.Json;

namespace Yapair.Api;

public sealed class AiConfig
{
    public string GeminiApiKey { get; set; } = "";
    public string Model { get; set; } = "gemini-1.5-flash";
    public bool EnableDuckDuckGo { get; set; } = true;
    public bool EnableWebResearch { get; set; } = true;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed record AiConfigDto(bool HasKey, string MaskedKey, string Model, bool EnableDuckDuckGo, bool EnableWebResearch, string? UpdatedAt);
public sealed record AiConfigUpdateRequest(string? GeminiApiKey, string? Model, bool? EnableDuckDuckGo, bool? EnableWebResearch);

public sealed class AiConfigManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly object _lock = new();
    private AiConfig _config = new();

    public AiConfigManager(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "ai-config.json");

        Load();
    }

    public AiConfig GetConfig()
    {
        lock (_lock)
        {
            return new AiConfig
            {
                GeminiApiKey = _config.GeminiApiKey,
                Model = _config.Model,
                EnableDuckDuckGo = _config.EnableDuckDuckGo,
                EnableWebResearch = _config.EnableWebResearch,
                UpdatedAt = _config.UpdatedAt
            };
        }
    }

    public string GetEffectiveApiKey()
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(_config.GeminiApiKey))
                return _config.GeminiApiKey.Trim();

            return Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim() ?? "";
        }
    }

    public AiConfigDto GetDto()
    {
        lock (_lock)
        {
            var effectiveKey = GetEffectiveApiKey();
            var hasKey = !string.IsNullOrWhiteSpace(effectiveKey);
            var masked = MaskKey(effectiveKey);

            return new AiConfigDto(
                HasKey: hasKey,
                MaskedKey: masked,
                Model: string.IsNullOrWhiteSpace(_config.Model) ? "gemini-1.5-flash" : _config.Model,
                EnableDuckDuckGo: _config.EnableDuckDuckGo,
                EnableWebResearch: _config.EnableWebResearch,
                UpdatedAt: _config.UpdatedAt?.ToString("o")
            );
        }
    }

    public void UpdateConfig(AiConfigUpdateRequest req)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(req.GeminiApiKey))
            {
                _config.GeminiApiKey = req.GeminiApiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(req.Model))
            {
                _config.Model = req.Model.Trim();
            }

            if (req.EnableDuckDuckGo.HasValue)
            {
                _config.EnableDuckDuckGo = req.EnableDuckDuckGo.Value;
            }

            if (req.EnableWebResearch.HasValue)
            {
                _config.EnableWebResearch = req.EnableWebResearch.Value;
            }

            _config.UpdatedAt = DateTimeOffset.UtcNow;
            Save();
        }
    }

    public static string MaskKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var trimmed = raw.Trim();
        if (trimmed.Length <= 8) return "********";
        var prefix = trimmed[..Math.Min(6, trimmed.Length)];
        var suffix = trimmed[^Math.Min(4, trimmed.Length)..];
        return $"{prefix}...{suffix}";
    }

    private void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var txt = File.ReadAllText(_filePath);
                    var doc = JsonSerializer.Deserialize<AiConfig>(txt);
                    if (doc != null) _config = doc;
                }
            }
            catch { }

            // Fallback from environment if file empty
            if (string.IsNullOrWhiteSpace(_config.GeminiApiKey))
            {
                var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                if (!string.IsNullOrWhiteSpace(envKey))
                {
                    _config.GeminiApiKey = envKey.Trim();
                }
            }
        }
    }

    private void Save()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var txt = JsonSerializer.Serialize(_config, JsonOpts);
                File.WriteAllText(_filePath, txt);
            }
            catch { }
        }
    }
}
