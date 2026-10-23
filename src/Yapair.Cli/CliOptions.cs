namespace Yapair.Cli;

internal enum CliCommand
{
    Discover,
    Verify,
    Playbooks,
    Schema,
    Version,
    Help,
}

internal sealed class CliOptions
{
    public string Format { get; set; } = "json";
    public string? OutputPath { get; set; }
    public bool Pretty { get; set; }
    public int TimeoutMs { get; set; } = 15_000;
    public int Events { get; set; } = 25;
    public int LookbackHours { get; set; } = 24;
    public string? IppPrinter { get; set; }
    public string IppPath { get; set; } = "/ipp/print";
    public bool IppAllowInsecureTls { get; set; }
    public bool ShowNetworkAddresses { get; set; }
    public bool IncludeDocumentNames { get; set; }
    public string? LogPath { get; set; }
    public bool Verbose { get; set; }

    public string? BaselinePath { get; set; }
    public string? PrinterName { get; set; }
    public bool Ask { get; set; }
    public int MaxAttempts { get; set; } = 2;
}

internal sealed record CliParseResult(CliCommand Command, CliOptions Options, string? Error);
