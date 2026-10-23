using Yapair.Core.Contracts;

namespace Yapair.Core.Verification;

/// <summary>
/// Turns deterministic findings into comparable problem signals. A missing source
/// never becomes a signal; it becomes an evidence gap that the comparer reports as
/// unknown rather than "resolved".
/// </summary>
public static class SnapshotProblemDetector
{
    public static IReadOnlyList<ProblemSignal> Detect(SystemSnapshot snapshot, string? printerName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Findings
            .Where(static finding => finding.IsProblem())
            .Where(finding => InScope(finding, printerName))
            .Select(static finding => new ProblemSignal
            {
                Source = finding.Source,
                Scope = finding.Subject,
                Code = finding.Code,
                Detail = finding.Summary,
            })
            .GroupBy(static signal => signal.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    public static bool HasAnyEvidence(SystemSnapshot snapshot, string? printerName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (printerName is null)
        {
            return snapshot.Sources.Any(static source => source.Status is SourceStatus.Success or SourceStatus.Partial);
        }

        var printerObserved = IsSourceObserved(snapshot, SourceNames.Printers)
            && snapshot.Printers.Any(printer => string.Equals(printer.Name, printerName, StringComparison.OrdinalIgnoreCase));
        var ippObserved = snapshot.IppChecks.Any(check =>
            check.Status is SourceStatus.Success or SourceStatus.Partial
            && string.Equals(check.PrinterName, printerName, StringComparison.OrdinalIgnoreCase));

        return printerObserved || ippObserved;
    }

    public static bool HasEvidenceFor(SystemSnapshot snapshot, ProblemSignal signal)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!IsSourceObserved(snapshot, signal.Source))
        {
            return false;
        }

        return signal.Source switch
        {
            SourceNames.Printers => snapshot.Printers.Any(printer =>
                string.Equals(printer.Name, signal.Scope, StringComparison.OrdinalIgnoreCase)),
            SourceNames.Ipp => snapshot.IppChecks.Any(check =>
                check.Status is SourceStatus.Success or SourceStatus.Partial
                && string.Equals(check.PrinterName, signal.Scope, StringComparison.OrdinalIgnoreCase)),
            _ => true,
        };
    }

    public static bool IsSourceObserved(SystemSnapshot snapshot, string sourceName)
    {
        var source = snapshot.Sources.FirstOrDefault(candidate =>
            string.Equals(candidate.Source, sourceName, StringComparison.OrdinalIgnoreCase));

        return source?.Status is SourceStatus.Success or SourceStatus.Partial;
    }

    private static bool InScope(Finding finding, string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return true;
        }

        return finding.Source switch
        {
            SourceNames.Spooler => true,
            SourceNames.Printers or SourceNames.Ipp => string.Equals(finding.Subject, printerName, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
