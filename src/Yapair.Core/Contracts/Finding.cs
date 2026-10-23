namespace Yapair.Core.Contracts;

/// <summary>
/// Broad Windows health domains. The engine is domain-agnostic: printer support
/// is one module, not the product boundary.
/// </summary>
public enum HealthDomain
{
    System,
    Services,
    Devices,
    Printers,
    Audio,
    Network,
    Security,
    Storage,
    WindowsUpdate,
}

public enum FindingSeverity
{
    Info,
    Warning,
    Error,
    Critical,
}

/// <summary>
/// A deterministic, evidence-backed finding. It is a candidate diagnosis, not an
/// authorisation to change the system.
/// </summary>
public sealed record Finding
{
    /// <summary>Source adapter that produced the evidence (printers, spooler, services, ...).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Stable, language-neutral identifier, e.g. service_not_running.</summary>
    public string Code { get; init; } = string.Empty;

    public HealthDomain Domain { get; init; }

    public FindingSeverity Severity { get; init; }

    /// <summary>Printer, service, device, disk, or adapter the finding belongs to.</summary>
    public string Subject { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>Optional catalogue entry that can address this finding after approval.</summary>
    public string? PlaybookId { get; init; }
}

public static class FindingRules
{
    public static bool IsProblem(this Finding finding) => finding.Severity is FindingSeverity.Warning
        or FindingSeverity.Error
        or FindingSeverity.Critical;
}
