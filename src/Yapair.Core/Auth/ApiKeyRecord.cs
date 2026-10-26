using System.Text.Json.Serialization;

namespace Yapair.Core.Auth;

public sealed record ApiKeyRecord
{
    [JsonPropertyName("key_id")]
    public string KeyId { get; init; } = "";

    [JsonPropertyName("key_hash")]
    public string KeyHash { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("role")]
    public string Role { get; init; } = "technician";

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; init; }

    [JsonPropertyName("revoked")]
    public bool Revoked { get; init; }

    [JsonIgnore]
    public UserRole UserRole => UserRoleExtensions.ParseRole(Role);
}
