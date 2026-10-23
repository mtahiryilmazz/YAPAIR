using System.Text.Json.Serialization;
using Yapair.Core.Contracts;

namespace Yapair.Core.Verification;

public enum VerificationOutcome
{
    Resolved,
    NotResolved,
    Unknown,
    Contradicted,
}

public enum UserConfirmation
{
    NotAsked,
    Resolved,
    NotResolved,
    Unsure,
}

public sealed record ProblemSignal
{
    /// <summary>Stable source name: printers, spooler, or ipp.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Printer name, or "spooler" for the service-level scope.</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>Stable code such as printer_offline, printer_error_state, ipp_stopped.</summary>
    public string Code { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    [JsonIgnore]
    public string Key => $"{Source}|{Scope}|{Code}";
}

public sealed record VerificationResult
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string VerificationId { get; init; } = string.Empty;

    public DateTimeOffset VerifiedAt { get; init; }

    public string? PrinterName { get; init; }

    public VerificationOutcome Outcome { get; init; }

    public UserConfirmation UserConfirmation { get; init; }

    public int Attempts { get; init; }

    public string BaselineSnapshotId { get; init; } = string.Empty;

    public string CurrentSnapshotId { get; init; } = string.Empty;

    public IReadOnlyList<ProblemSignal> BaselineSignals { get; init; } = [];

    public IReadOnlyList<ProblemSignal> CurrentSignals { get; init; } = [];

    public IReadOnlyList<ProblemSignal> ResolvedSignals { get; init; } = [];

    public IReadOnlyList<ProblemSignal> PersistingSignals { get; init; } = [];

    public IReadOnlyList<ProblemSignal> NewSignals { get; init; } = [];

    public IReadOnlyList<ProblemSignal> UnverifiableSignals { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Details { get; init; } = [];
}

public sealed record VerificationOptions
{
    public string? PrinterName { get; init; }

    /// <summary>How many collect-and-ask rounds are allowed when the problem persists.</summary>
    public int MaxAttempts { get; init; } = 2;
}

public interface IVerificationPrompt
{
    Task<UserConfirmation> AskOutcomeAsync(VerificationResult result, CancellationToken cancellationToken);

    Task<bool> AskReDiagnoseAsync(VerificationResult result, CancellationToken cancellationToken);
}

public sealed class NoopVerificationPrompt : IVerificationPrompt
{
    public Task<UserConfirmation> AskOutcomeAsync(VerificationResult result, CancellationToken cancellationToken)
        => Task.FromResult(UserConfirmation.NotAsked);

    public Task<bool> AskReDiagnoseAsync(VerificationResult result, CancellationToken cancellationToken)
        => Task.FromResult(false);
}
