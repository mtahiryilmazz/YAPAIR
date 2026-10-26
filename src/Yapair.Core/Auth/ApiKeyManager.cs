using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Yapair.Core.Auth;

public sealed class ApiKeyManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly string? _masterEnvKey;
    private readonly List<ApiKeyRecord> _keys = new();
    private readonly object _lock = new();

    public ApiKeyManager(string? filePath = null, string? masterEnvKey = null)
    {
        _masterEnvKey = masterEnvKey ?? Environment.GetEnvironmentVariable("YAPAIR_API_KEY");
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "api-keys.json");

        Load();
    }

    public (ApiKeyRecord Record, string RawKey) CreateKey(string name, UserRole role, TimeSpan? lifetime = null)
    {
        lock (_lock)
        {
            var rawSecret = "yap_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
            var hash = HashKey(rawSecret);
            var keyId = "key_" + Guid.NewGuid().ToString("N")[..12];

            var rec = new ApiKeyRecord
            {
                KeyId = keyId,
                KeyHash = hash,
                Name = name,
                Role = role.ToRoleString(),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = lifetime.HasValue ? DateTimeOffset.UtcNow.Add(lifetime.Value) : null,
                Revoked = false
            };

            _keys.Add(rec);
            Save();
            return (rec, rawSecret);
        }
    }

    public (bool IsValid, UserRole Role, ApiKeyRecord? Record) ValidateKey(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return (false, UserRole.ReadOnly, null);
        var trimmed = rawKey.Trim();

        // 1. Master env key check (Super Admin)
        if (!string.IsNullOrEmpty(_masterEnvKey) && FixedTimeEquals(trimmed, _masterEnvKey))
        {
            var masterRec = new ApiKeyRecord
            {
                KeyId = "master_env",
                Name = "Master Environment Key",
                Role = "admin",
                CreatedAt = DateTimeOffset.MinValue,
                Revoked = false
            };
            return (true, UserRole.Admin, masterRec);
        }

        // 2. Hash check against registered keys
        var incomingHash = HashKey(trimmed);
        lock (_lock)
        {
            var found = _keys.FirstOrDefault(k => FixedTimeEquals(k.KeyHash, incomingHash));
            if (found == null) return (false, UserRole.ReadOnly, null);
            if (found.Revoked) return (false, UserRole.ReadOnly, null);
            if (found.ExpiresAt.HasValue && found.ExpiresAt.Value < DateTimeOffset.UtcNow) return (false, UserRole.ReadOnly, null);

            return (true, found.UserRole, found);
        }
    }

    public (ApiKeyRecord NewRecord, string RawKey)? RotateKey(string keyId)
    {
        lock (_lock)
        {
            var existing = _keys.FirstOrDefault(k => k.KeyId == keyId);
            if (existing == null) return null;

            // Revoke old
            _keys.Remove(existing);
            _keys.Add(existing with { Revoked = true });

            // Create replacement with same name and role
            var (newRec, rawKey) = CreateKey(existing.Name + " (Rotated)", existing.UserRole);
            return (newRec, rawKey);
        }
    }

    public bool RevokeKey(string keyId)
    {
        lock (_lock)
        {
            var idx = _keys.FindIndex(k => k.KeyId == keyId);
            if (idx < 0) return false;

            _keys[idx] = _keys[idx] with { Revoked = true };
            Save();
            return true;
        }
    }

    public bool DeleteKey(string keyId)
    {
        lock (_lock)
        {
            var idx = _keys.FindIndex(k => k.KeyId == keyId);
            if (idx < 0) return false;

            _keys.RemoveAt(idx);
            Save();
            return true;
        }
    }

    public IReadOnlyList<ApiKeyRecord> ListKeys()
    {
        lock (_lock)
        {
            return _keys.OrderByDescending(k => k.CreatedAt).ToList();
        }
    }

    public static string HashKey(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var txt = File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize<List<ApiKeyRecord>>(txt);
                if (list != null)
                {
                    _keys.Clear();
                    _keys.AddRange(list);
                }
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var txt = JsonSerializer.Serialize(_keys, JsonOpts);
            File.WriteAllText(_filePath, txt);
        }
        catch { }
    }
}
