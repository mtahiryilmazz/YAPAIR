namespace Yapair.Storage;

public sealed record CaseRecord
{
    public string CaseId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string? RawText { get; init; }
    public string NormalizedText { get; init; } = string.Empty;
    public string SymptomCode { get; init; } = string.Empty;
    public string SymptomLabel { get; init; } = string.Empty;
    public string IssueFamily { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string? SnapshotId { get; init; }
    public string? Outcome { get; init; }
    public string? PlaybookId { get; init; }
    public string? UserConfirmation { get; init; }

    /// <summary>Pseudonymous machine key (hash); raw HWID is never stored.</summary>
    public string? MachineKey { get; init; }

    /// <summary>Computer name label for support staff. Not used as the key.</summary>
    public string? MachineLabel { get; init; }
}

public sealed record CaseSubcluster(
    string Code,
    string Label,
    int Count,
    IReadOnlyList<string> SampleTexts);

public sealed record CaseCluster(
    string Family,
    string Label,
    int Count,
    IReadOnlyList<CaseSubcluster> Subclusters,
    IReadOnlyList<string> SampleTexts,
    IReadOnlyList<string> CaseIds);

public sealed record CaseClusterReport(
    DateTimeOffset GeneratedAt,
    double Threshold,
    int CaseCount,
    IReadOnlyList<CaseCluster> Clusters);
