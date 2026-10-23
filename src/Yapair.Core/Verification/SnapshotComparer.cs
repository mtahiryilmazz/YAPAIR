using Yapair.Core.Contracts;

namespace Yapair.Core.Verification;

/// <summary>
/// Compares a baseline diagnostic snapshot with a fresh one. The comparison is
/// deliberately conservative: missing evidence yields Unknown, never Resolved.
/// </summary>
public static class SnapshotComparer
{
    public static VerificationResult Compare(
        SystemSnapshot baseline,
        SystemSnapshot current,
        string? printerName,
        UserConfirmation userConfirmation = UserConfirmation.NotAsked)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        var baselineSignals = SnapshotProblemDetector.Detect(baseline, printerName);
        var currentSignals = SnapshotProblemDetector.Detect(current, printerName);
        var baselineKeys = baselineSignals.Select(static signal => signal.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentKeys = currentSignals.Select(static signal => signal.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var persisting = baselineSignals.Where(signal => currentKeys.Contains(signal.Key)).ToArray();
        var resolved = baselineSignals
            .Where(signal => !currentKeys.Contains(signal.Key) && SnapshotProblemDetector.HasEvidenceFor(current, signal))
            .ToArray();
        var unverifiable = baselineSignals
            .Where(signal => !currentKeys.Contains(signal.Key) && !SnapshotProblemDetector.HasEvidenceFor(current, signal))
            .ToArray();
        var added = currentSignals.Where(signal => !baselineKeys.Contains(signal.Key)).ToArray();

        var hasEvidence = SnapshotProblemDetector.HasAnyEvidence(current, printerName);
        VerificationOutcome outcome;
        string summary;

        if (!hasEvidence)
        {
            outcome = VerificationOutcome.Unknown;
            summary = "The current snapshot does not contain enough evidence to verify the selected scope.";
        }
        else if (baselineSignals.Count == 0)
        {
            outcome = VerificationOutcome.Unknown;
            summary = "No baseline problem signal was found, so there is nothing to verify.";
        }
        else if (currentSignals.Count == 0)
        {
            if (unverifiable.Length > 0)
            {
                outcome = VerificationOutcome.Unknown;
                summary = "The baseline problem cannot be checked because the current snapshot is missing the relevant source.";
            }
            else
            {
                outcome = VerificationOutcome.Resolved;
                summary = "All baseline problem signals are absent from the current snapshot.";
            }
        }
        else
        {
            outcome = VerificationOutcome.NotResolved;
            summary = $"{persisting.Length} baseline signal(s) persist and {added.Length} new signal(s) were observed.";
        }

        var result = new VerificationResult
        {
            VerificationId = ("ver-" + Guid.NewGuid().ToString("N"))[..16],
            VerifiedAt = current.CollectedAt,
            PrinterName = printerName,
            Outcome = outcome,
            UserConfirmation = userConfirmation,
            Attempts = 1,
            BaselineSnapshotId = baseline.SnapshotId,
            CurrentSnapshotId = current.SnapshotId,
            BaselineSignals = baselineSignals,
            CurrentSignals = currentSignals,
            ResolvedSignals = resolved,
            PersistingSignals = persisting,
            NewSignals = added,
            UnverifiableSignals = unverifiable,
            Summary = summary,
            Details = BuildDetails(baselineSignals, currentSignals, resolved, persisting, added, unverifiable),
        };

        return userConfirmation == UserConfirmation.NotAsked
            ? result
            : ApplyUserConfirmation(result, userConfirmation);
    }

    public static VerificationResult ApplyUserConfirmation(VerificationResult result, UserConfirmation userConfirmation)
    {
        ArgumentNullException.ThrowIfNull(result);

        var (outcome, summary) = (result.Outcome, userConfirmation) switch
        {
            (_, UserConfirmation.NotAsked) => (result.Outcome, result.Summary),
            (VerificationOutcome.Resolved, UserConfirmation.Resolved) => (VerificationOutcome.Resolved, "Automated checks and the user both report the issue as resolved."),
            (VerificationOutcome.NotResolved, UserConfirmation.Resolved) => (VerificationOutcome.Contradicted, "The user reports the issue as resolved, but automated checks still observe problem signals."),
            (VerificationOutcome.Unknown, UserConfirmation.Resolved) => (VerificationOutcome.Resolved, "The user confirmed the issue is resolved; automated evidence was incomplete."),
            (_, UserConfirmation.NotResolved) => (VerificationOutcome.NotResolved, "The user reports the issue is not resolved; re-diagnosis is recommended because the cause may be different."),
            (VerificationOutcome.Resolved, UserConfirmation.Unsure) => (VerificationOutcome.Unknown, "The user is unsure and automated checks alone cannot confirm the outcome."),
            (VerificationOutcome.NotResolved, UserConfirmation.Unsure) => (VerificationOutcome.NotResolved, result.Summary),
            (VerificationOutcome.Unknown, UserConfirmation.Unsure) => (VerificationOutcome.Unknown, result.Summary),
            _ => (result.Outcome, result.Summary),
        };

        var details = result.Details.ToList();
        if (userConfirmation != UserConfirmation.NotAsked)
        {
            details.Add($"user_confirmation={ToCode(userConfirmation)}");
        }

        return result with
        {
            Outcome = outcome,
            UserConfirmation = userConfirmation,
            Summary = summary,
            Details = details,
        };
    }

    private static IReadOnlyList<string> BuildDetails(
        IReadOnlyList<ProblemSignal> baselineSignals,
        IReadOnlyList<ProblemSignal> currentSignals,
        IReadOnlyList<ProblemSignal> resolved,
        IReadOnlyList<ProblemSignal> persisting,
        IReadOnlyList<ProblemSignal> added,
        IReadOnlyList<ProblemSignal> unverifiable)
    {
        var details = new List<string>();
        details.AddRange(baselineSignals.Select(signal => $"baseline: {Describe(signal)}"));
        details.AddRange(currentSignals.Select(signal => $"current: {Describe(signal)}"));
        details.AddRange(resolved.Select(signal => $"resolved: {Describe(signal)}"));
        details.AddRange(persisting.Select(signal => $"persisting: {Describe(signal)}"));
        details.AddRange(added.Select(signal => $"new: {Describe(signal)}"));
        details.AddRange(unverifiable.Select(signal => $"unverifiable: {Describe(signal)}"));
        return details;
    }

    private static string Describe(ProblemSignal signal) => $"{signal.Source}/{signal.Scope}/{signal.Code} ({signal.Detail})";

    private static string ToCode(UserConfirmation confirmation) => confirmation switch
    {
        UserConfirmation.Resolved => "resolved",
        UserConfirmation.NotResolved => "not_resolved",
        UserConfirmation.Unsure => "unsure",
        _ => "not_asked",
    };
}
