using System.Text.Json.Serialization;

namespace Yapair.Core.Auth;

public sealed record ApprovalRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("machine_key")]
    public string MachineKey { get; init; } = "";

    [JsonPropertyName("requested_mode")]
    public string RequestedMode { get; init; } = "";

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";

    [JsonPropertyName("approval_code")]
    public string? ApprovalCode { get; init; }

    [JsonPropertyName("requested_at")]
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("approved_by")]
    public string? ApprovedBy { get; init; }

    [JsonPropertyName("approved_at")]
    public DateTimeOffset? ApprovedAt { get; init; }
}
