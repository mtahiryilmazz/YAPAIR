using System.Text.Json.Serialization;

namespace Yapair.Api;

public sealed record CaseBatch(
    [property: JsonPropertyName("batch_id")] string BatchId,
    [property: JsonPropertyName("cases")] List<CaseDto> Cases);

public sealed record CaseDto(
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("machine_key")] string? MachineKey,
    [property: JsonPropertyName("machine_label")] string? MachineLabel,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("normalized_text")] string NormalizedText,
    [property: JsonPropertyName("symptom_code")] string SymptomCode,
    [property: JsonPropertyName("symptom_label")] string SymptomLabel,
    [property: JsonPropertyName("issue_family")] string IssueFamily,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("outcome")] string? Outcome,
    [property: JsonPropertyName("playbook_id")] string? PlaybookId,
    [property: JsonPropertyName("user_confirmation")] string? UserConfirmation,
    [property: JsonPropertyName("raw_text")] string? RawText);

public sealed class MachineRow
{
    [JsonPropertyName("machine_key")] public string MachineKey { get; init; } = default!;
    [JsonPropertyName("machine_label")] public string MachineLabel { get; init; } = default!;
    [JsonPropertyName("last_seen")] public DateTimeOffset LastSeen { get; init; }
}

public sealed class CaseRow
{
    [JsonPropertyName("case_id")] public string CaseId { get; init; } = default!;
    [JsonPropertyName("machine_key")] public string? MachineKey { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("normalized_text")] public string NormalizedText { get; init; } = default!;
    [JsonPropertyName("symptom_code")] public string SymptomCode { get; init; } = default!;
    [JsonPropertyName("symptom_label")] public string SymptomLabel { get; init; } = default!;
    [JsonPropertyName("issue_family")] public string IssueFamily { get; init; } = default!;
    [JsonPropertyName("domain")] public string Domain { get; init; } = default!;
    [JsonPropertyName("outcome")] public string? Outcome { get; init; }
    [JsonPropertyName("playbook_id")] public string? PlaybookId { get; init; }
    [JsonPropertyName("user_confirmation")] public string? UserConfirmation { get; init; }
    [JsonPropertyName("raw_text")] public string? RawText { get; init; }
}

public sealed class SyncReceipt
{
    [JsonPropertyName("batch_id")] public string BatchId { get; init; } = default!;
    [JsonPropertyName("case_count")] public int CaseCount { get; init; }
    [JsonPropertyName("api_version")] public string ApiVersion { get; init; } = "v1";
}
