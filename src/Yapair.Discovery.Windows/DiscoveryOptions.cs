namespace Yapair.Discovery.Windows;

/// <summary>
/// Bounded, read-only discovery settings. Every source gets its own timeout;
/// one slow WMI provider must not hang the whole report.
/// </summary>
public sealed record DiscoveryOptions
{
    public TimeSpan PerSourceTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan IppTimeout { get; init; } = TimeSpan.FromSeconds(4);

    public int EventLookbackHours { get; init; } = 24;

    public int MaxEvents { get; init; } = 25;

    /// <summary>Printer name selected by the user for an opt-in IPP check.</summary>
    public string? IppPrinterName { get; init; }

    public string IppPath { get; init; } = "/ipp/print";

    /// <summary>Accept a self-signed HTTPS certificate when a printer redirects IPP to IPPS.</summary>
    public bool IppAllowInsecureTls { get; init; }

    public bool IncludeDocumentNames { get; init; }

    public bool ShowNetworkAddresses { get; init; }
}
