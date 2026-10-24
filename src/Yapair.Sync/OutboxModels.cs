namespace Yapair.Sync;

public sealed class OutboxCase
{
    public string CaseId { get; init; } = default!;
    public string? MachineKey { get; init; }
    public string? MachineLabel { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string NormalizedText { get; init; } = default!;
    public string SymptomCode { get; init; } = default!;
    public string SymptomLabel { get; init; } = default!;
    public string IssueFamily { get; init; } = default!;
    public string Domain { get; init; } = default!;
    public string? Outcome { get; init; }
    public string? PlaybookId { get; init; }
    public string? UserConfirmation { get; init; }
    public string? RawText { get; init; }
    public DateTimeOffset EnqueuedAt { get; init; }
    public int Attempts { get; init; }
    public DateTimeOffset? LastAttemptAt { get; init; }
    public string? LastError { get; init; }
}

public sealed class OutboxStats
{
    public int Pending { get; init; }
    public int Failed { get; init; }
    public int Sent { get; init; }
}
