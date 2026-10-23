using Yapair.Core.Contracts;

namespace Yapair.TestSupport;

/// <summary>Minimal valid snapshot used by contract, redaction, and verification tests.</summary>
public static class SnapshotFixture
{
    public static readonly DateTimeOffset FixedTime = new(2026, 10, 23, 7, 0, 0, TimeSpan.Zero);

    public static SystemSnapshot CreateHealthy(string snapshotId = "snap-0123456789ab") => new()
    {
        SnapshotId = snapshotId,
        CollectedAt = FixedTime,
        Status = SnapshotStatus.Success,
        Os = new OsInfo
        {
            ProductName = "Windows 11 Pro",
            DisplayVersion = "24H2",
            Build = "26100.1000",
            Architecture = "X64",
            Is64Bit = true,
            IsElevated = false,
            IsDomainJoined = false,
        },
        Spooler = new ServiceInfo
        {
            Name = "Spooler",
            DisplayName = "Print Spooler",
            Status = "Running",
            StartType = "Automatic",
            ProcessId = 1200,
        },
        Printers =
        [
            new PrinterInfo
            {
                Name = "Test Printer",
                DriverName = "Test Driver",
                PortName = "192.168.10.20",
                IsDefault = true,
                IsNetwork = true,
                StatusCode = 3,
                Status = "idle",
                DetectedErrorState = 2,
                ErrorState = "no_error",
                ExtendedStatus = 3,
                QueuedJobCount = 0,
                QueueObserved = true,
            },
        ],
        PrintJobs = [],
        Ports =
        [
            new PortInfo
            {
                Name = "192.168.10.20",
                Protocol = "tcp_ip",
                Host = "192.168.10.20",
                PortNumber = 9100,
                SnmpEnabled = true,
                Source = "combined",
            },
        ],
        Drivers =
        [
            new DriverInfo
            {
                Name = "Test Driver",
                Version = "4",
                Platform = "Windows x64",
                InfName = "test.inf",
            },
        ],
        PnpDevices = [],
        EventLogs = new EventLogSection
        {
            LookbackHours = 24,
            Logs =
            [
                new EventLogInfo
                {
                    Name = "System",
                    Status = SourceStatus.Success,
                    EventsRead = 0,
                },
            ],
            Records = [],
        },
        IppChecks = [],
        Findings = [],
        DomainsChecked =
        [
            HealthDomain.System,
            HealthDomain.Services,
            HealthDomain.Devices,
            HealthDomain.Printers,
            HealthDomain.Audio,
            HealthDomain.Network,
            HealthDomain.Security,
            HealthDomain.Storage,
            HealthDomain.WindowsUpdate,
        ],
        Evidence =
        [
            new EvidenceFact
            {
                Source = SourceNames.System,
                Fact = "finding_count",
                Value = "0",
                ObservedAt = FixedTime,
                Quality = EvidenceQuality.Direct,
            },
        ],
        Sources =
        [
            Source(SourceNames.Os),
            Source(SourceNames.Spooler),
            Source(SourceNames.Printers),
            Source(SourceNames.PrintJobs),
            Source(SourceNames.Ports),
            Source(SourceNames.Drivers),
            Source(SourceNames.PnpDevices),
            Source(SourceNames.EventLogs),
            Source(SourceNames.System),
            Source(SourceNames.Services),
            Source(SourceNames.Security),
            Source(SourceNames.Network),
            Source(SourceNames.Storage),
            Source(SourceNames.WindowsUpdate),
            new SourceResult
            {
                Source = SourceNames.Ipp,
                Status = SourceStatus.Disabled,
                ObservedAt = FixedTime,
                Message = "IPP not requested.",
            },
            new SourceResult
            {
                Source = SourceNames.Snmp,
                Status = SourceStatus.Disabled,
                ObservedAt = FixedTime,
                Message = "SNMP disabled.",
            },
            new SourceResult
            {
                Source = SourceNames.NetworkDiscovery,
                Status = SourceStatus.Disabled,
                ObservedAt = FixedTime,
                Message = "Network discovery disabled.",
            },
        ],
        Privacy = new PrivacyReport(),
        Limitations = ["Test fixture."],
    };

    public static SystemSnapshot WithPrinterProblem(string snapshotId = "snap-0123456789ab") =>
        CreateHealthy(snapshotId) with
        {
            Printers =
            [
                CreateHealthy(snapshotId).Printers[0] with
                {
                    WorkOffline = true,
                    ErrorState = "paper_jam",
                    DetectedErrorState = 8,
                },
            ],
            Findings =
            [
                new Finding
                {
                    Source = SourceNames.Printers,
                    Code = "printer_offline",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Error,
                    Subject = "Test Printer",
                    Summary = "Test Printer çevrimdışı.",
                    Evidence = ["work_offline=true"],
                    PlaybookId = "printers.device.review.v1",
                },
            ],
            Evidence =
            [
                new EvidenceFact
                {
                    Source = SourceNames.System,
                    Fact = "finding_count",
                    Value = "1",
                    ObservedAt = FixedTime,
                    Quality = EvidenceQuality.Direct,
                },
            ],
        };

    public static SystemSnapshot WithAllSourcesFailed(string snapshotId = "snap-0123456789ab") =>
        CreateHealthy(snapshotId) with
        {
            Status = SnapshotStatus.Failed,
            Findings = [],
            Sources = CreateHealthy(snapshotId).Sources
                .Select(source => source.Status == SourceStatus.Disabled
                    ? source
                    : source with { Status = SourceStatus.Failed, ErrorCode = "test_failure" })
                .ToArray(),
        };

    private static SourceResult Source(string name) => new()
    {
        Source = name,
        Status = SourceStatus.Success,
        ObservedAt = FixedTime,
        DurationMs = 1,
        ItemCount = 1,
    };
}
