using System.Text.Json.Serialization;

namespace Yapair.Core.Contracts;

/// <summary>
/// Versioned, platform-neutral diagnostic snapshot. This is the only shape that
/// leaves the discovery layer; it is validated against the embedded JSON Schema
/// before it is written or shared.
/// </summary>
public sealed record SystemSnapshot
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Opaque, local-only identifier. Contains no machine or user identity.</summary>
    public string SnapshotId { get; init; } = string.Empty;

    public DateTimeOffset CollectedAt { get; init; }

    public SnapshotStatus Status { get; init; }

    public OsInfo Os { get; init; } = new();

    public ServiceInfo? Spooler { get; init; }

    public IReadOnlyList<PrinterInfo> Printers { get; init; } = [];

    public IReadOnlyList<PrintJobInfo> PrintJobs { get; init; } = [];

    public IReadOnlyList<PortInfo> Ports { get; init; } = [];

    public IReadOnlyList<DriverInfo> Drivers { get; init; } = [];

    public IReadOnlyList<PnpDeviceInfo> PnpDevices { get; init; } = [];

    public EventLogSection EventLogs { get; init; } = new();

    public IReadOnlyList<IppCheckInfo> IppChecks { get; init; } = [];

    /// <summary>Deterministic findings across all enabled health domains.</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>Domains for which at least one source was attempted.</summary>
    public IReadOnlyList<HealthDomain> DomainsChecked { get; init; } = [];

    public IReadOnlyList<EvidenceFact> Evidence { get; init; } = [];

    /// <summary>Per-adapter outcome, including sources that were unavailable or disabled.</summary>
    public IReadOnlyList<SourceResult> Sources { get; init; } = [];

    public PrivacyReport Privacy { get; init; } = new();

    public IReadOnlyList<string> Limitations { get; init; } = [];
}

public enum SnapshotStatus
{
    Success,
    Partial,
    Failed,
}

public enum SourceStatus
{
    Success,
    Partial,
    Unknown,
    Failed,
    Disabled,
}

public enum EvidenceQuality
{
    Direct,
    Indirect,
    Stale,
    Unknown,
}

public sealed record OsInfo
{
    public string Platform { get; init; } = "windows";
    public string ProductName { get; init; } = string.Empty;
    public string DisplayVersion { get; init; } = string.Empty;
    public string Build { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;

    [JsonPropertyName("is_64_bit")]
    public bool Is64Bit { get; init; }

    public bool IsElevated { get; init; }
    public bool IsDomainJoined { get; init; }
}

public sealed record ServiceInfo
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StartType { get; init; } = string.Empty;
    public int? ProcessId { get; init; }
}

public sealed record PrinterInfo
{
    public string Name { get; init; } = string.Empty;
    public string DriverName { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsShared { get; init; }
    public bool IsNetwork { get; init; }
    public bool IsLocal { get; init; }
    public bool WorkOffline { get; init; }
    public int StatusCode { get; init; }
    public string Status { get; init; } = string.Empty;
    public int DetectedErrorState { get; init; }
    public string ErrorState { get; init; } = string.Empty;
    public int ExtendedStatus { get; init; }
    public int QueuedJobCount { get; init; }

    /// <summary>False when the job source failed; a zero count is then not evidence of an empty queue.</summary>
    public bool QueueObserved { get; init; }
}

public sealed record PrintJobInfo
{
    public string PrinterName { get; init; } = string.Empty;
    public int JobId { get; init; }
    public string Document { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int StatusMask { get; init; }
    public IReadOnlyList<string> StatusFlags { get; init; } = [];
    public int TotalPages { get; init; }
    public int PagesPrinted { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public double? AgeMinutes { get; init; }
}

public sealed record PortInfo
{
    public string Name { get; init; } = string.Empty;
    public string Protocol { get; init; } = "unknown";
    public string? Host { get; init; }
    public int? PortNumber { get; init; }
    public bool? SnmpEnabled { get; init; }
    public string Source { get; init; } = "registry";
}

public sealed record DriverInfo
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public string InfName { get; init; } = string.Empty;
}

public sealed record PnpDeviceInfo
{
    public string FriendlyName { get; init; } = string.Empty;
    public string Class { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public IReadOnlyList<string> HardwareIds { get; init; } = [];
    public string Status { get; init; } = string.Empty;
    public int ConfigManagerErrorCode { get; init; }
    public string ErrorMeaning { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
}

public sealed record EventLogSection
{
    public int LookbackHours { get; init; }
    public IReadOnlyList<EventLogInfo> Logs { get; init; } = [];
    public IReadOnlyList<EventRecordInfo> Records { get; init; } = [];
}

public sealed record EventLogInfo
{
    public string Name { get; init; } = string.Empty;
    public SourceStatus Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public int EventsRead { get; init; }
}

public sealed record EventRecordInfo
{
    public string LogName { get; init; } = string.Empty;
    public int EventId { get; init; }

    /// <summary>Stable Windows event level: 1 critical, 2 error, 3 warning, 4 information, 5 verbose.</summary>
    public int LevelCode { get; init; }

    public string Level { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public DateTimeOffset TimeCreated { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record IppCheckInfo
{
    public string PrinterName { get; init; } = string.Empty;
    public string Uri { get; init; } = string.Empty;
    public SourceStatus Status { get; init; }
    public int? HttpStatus { get; init; }
    public int? IppStatus { get; init; }
    public string? IppStatusName { get; init; }
    public string? Message { get; init; }
    public string? ReportedPrinterName { get; init; }
    public int? PrinterState { get; init; }
    public string? PrinterStateText { get; init; }
    public IReadOnlyList<string> PrinterStateReasons { get; init; } = [];
    public bool? AcceptingJobs { get; init; }
    public int? QueuedJobCount { get; init; }
    public string? MakeAndModel { get; init; }
    public long DurationMs { get; init; }
}

public sealed record EvidenceFact
{
    public string Source { get; init; } = string.Empty;
    public string Fact { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public EvidenceQuality Quality { get; init; }
}

public sealed record SourceResult
{
    public string Source { get; init; } = string.Empty;
    public SourceStatus Status { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public long DurationMs { get; init; }
    public int ItemCount { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
}

public sealed record PrivacyReport
{
    public string PolicyVersion { get; init; } = "1.0";
    public string DocumentNames { get; init; } = "redacted";
    public string NetworkAddresses { get; init; } = "masked";
    public string UserIdentifiers { get; init; } = "omitted";
    public string MachineIdentifiers { get; init; } = "masked";
    public int RedactedFields { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>Stable source identifiers used by reports, evidence, and tests.</summary>
public static class SourceNames
{
    public const string Os = "os";
    public const string Spooler = "spooler";
    public const string Printers = "printers";
    public const string PrintJobs = "print_jobs";
    public const string Ports = "ports";
    public const string Drivers = "drivers";
    public const string PnpDevices = "pnp_devices";
    public const string EventLogs = "event_logs";
    public const string Ipp = "ipp";
    public const string Snmp = "snmp";
    public const string NetworkDiscovery = "network_discovery";
    public const string System = "system";
    public const string Services = "services";
    public const string Security = "security";
    public const string Network = "network";
    public const string Audio = "audio";
    public const string Storage = "storage";
    public const string WindowsUpdate = "windows_update";
}

public static class SnapshotStatusRules
{
    /// <summary>
    /// Disabled sources never degrade a report. A report is failed only when every
    /// enabled source failed; a single failure otherwise makes it partial.
    /// </summary>
    public static SnapshotStatus Aggregate(IEnumerable<SourceResult> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var enabled = sources.Where(static source => source.Status != SourceStatus.Disabled).ToArray();
        if (enabled.Length == 0 || enabled.All(static source => source.Status == SourceStatus.Failed))
        {
            return SnapshotStatus.Failed;
        }

        return enabled.Any(static source => source.Status is SourceStatus.Failed or SourceStatus.Partial or SourceStatus.Unknown)
            ? SnapshotStatus.Partial
            : SnapshotStatus.Success;
    }
}
