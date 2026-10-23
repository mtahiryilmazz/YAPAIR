using Yapair.Core.Contracts;

namespace Yapair.Core.Privacy;

public sealed record RedactionOptions
{
    /// <summary>Explicit opt-in for local debugging only. Document names are personal data.</summary>
    public bool IncludeDocumentNames { get; init; }

    /// <summary>Explicit opt-in; otherwise IPv4 addresses are masked to /24.</summary>
    public bool ShowNetworkAddresses { get; init; }

    public string PolicyVersion { get; init; } = "1.0";
}

/// <summary>
/// Produces the only snapshot shape that is allowed to leave discovery. Structured
/// identifiers are removed or masked field by field; free text is sanitized last.
/// </summary>
public sealed class SnapshotRedactor
{
    private readonly RedactionOptions _options;
    private readonly RedactionCounters _counters = new();

    public SnapshotRedactor(RedactionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public SystemSnapshot Redact(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var redacted = snapshot with
        {
            Printers = snapshot.Printers.Select(RedactPrinter).ToArray(),
            PrintJobs = snapshot.PrintJobs.Select(RedactJob).ToArray(),
            Ports = snapshot.Ports.Select(RedactPort).ToArray(),
            PnpDevices = snapshot.PnpDevices.Select(RedactPnpDevice).ToArray(),
            EventLogs = RedactEventLogs(snapshot.EventLogs),
            IppChecks = snapshot.IppChecks.Select(RedactIppCheck).ToArray(),
            Findings = snapshot.Findings.Select(RedactFinding).ToArray(),
            Evidence = snapshot.Evidence.Select(RedactEvidence).ToArray(),
            Sources = snapshot.Sources.Select(RedactSource).ToArray(),
            Limitations = snapshot.Limitations.Select(value => RedactText(value)).ToArray(),
            Privacy = BuildPrivacyReport(),
        };

        return redacted;
    }

    private PrinterInfo RedactPrinter(PrinterInfo printer) => printer with
    {
        Name = RedactText(printer.Name),
        PortName = RedactText(printer.PortName),
    };

    private PrintJobInfo RedactJob(PrintJobInfo job)
    {
        var document = _options.IncludeDocumentNames
            ? RedactText(job.Document, redactDocumentNames: false)
            : "<redacted-document>";

        if (!_options.IncludeDocumentNames)
        {
            _counters.DocumentNames++;
        }

        return job with
        {
            PrinterName = RedactText(job.PrinterName),
            Document = document,
        };
    }

    private PortInfo RedactPort(PortInfo port)
    {
        var host = port.Host;
        if (!_options.ShowNetworkAddresses && SensitiveDataMasker.MaskIpv4(host) is { } maskedHost && maskedHost != host)
        {
            _counters.NetworkAddresses++;
            host = maskedHost;
        }

        return port with
        {
            Name = RedactText(port.Name),
            Host = host,
        };
    }

    private PnpDeviceInfo RedactPnpDevice(PnpDeviceInfo device)
    {
        var deviceId = SensitiveDataMasker.RedactDeviceInstanceId(device.DeviceId);
        if (deviceId != device.DeviceId)
        {
            _counters.Identifiers++;
        }

        var hardwareIds = device.HardwareIds
            .Select(hardwareId =>
            {
                var masked = SensitiveDataMasker.RedactDeviceInstanceId(hardwareId);
                if (masked != hardwareId)
                {
                    _counters.Identifiers++;
                }

                return masked;
            })
            .ToArray();

        return device with
        {
            FriendlyName = RedactText(device.FriendlyName),
            DeviceId = deviceId,
            HardwareIds = hardwareIds,
        };
    }

    private EventLogSection RedactEventLogs(EventLogSection section) => section with
    {
        Logs = section.Logs.Select(log => log with { Message = RedactOptional(log.Message) }).ToArray(),
        Records = section.Records.Select(record => record with { Message = RedactText(record.Message) }).ToArray(),
    };

    private IppCheckInfo RedactIppCheck(IppCheckInfo check) => check with
    {
        Uri = RedactText(check.Uri),
        Message = RedactOptional(check.Message),
        ReportedPrinterName = RedactOptional(check.ReportedPrinterName),
    };

    private Finding RedactFinding(Finding finding) => finding with
    {
        Subject = RedactText(finding.Subject),
        Summary = RedactText(finding.Summary),
        Evidence = finding.Evidence.Select(value => RedactText(value)).ToArray(),
    };

    private EvidenceFact RedactEvidence(EvidenceFact fact) => fact with
    {
        Value = RedactText(fact.Value),
        Subject = RedactOptional(fact.Subject),
    };

    private SourceResult RedactSource(SourceResult source) => source with
    {
        Message = RedactOptional(source.Message),
    };

    private string? RedactOptional(string? value) => value is null ? null : RedactText(value);

    private string RedactText(string value, bool redactDocumentNames = true)
    {
        var result = SensitiveDataMasker.RedactText(
            value,
            maskNetworkAddresses: !_options.ShowNetworkAddresses,
            redactDocumentNames: redactDocumentNames && !_options.IncludeDocumentNames);

        _counters.DocumentNames += result.DocumentNames;
        _counters.NetworkAddresses += result.NetworkAddresses;
        _counters.Identifiers += result.Identifiers;
        return result.Text;
    }

    private PrivacyReport BuildPrivacyReport()
    {
        var notes = new List<string>
        {
            "Printer and PnP device names, IDs, and free-text messages are passed through a free-text sanitizer.",
            "User profile names, MAC addresses, and e-mail addresses are redacted; printer job owners are not collected.",
            "Pnp device instance suffixes are removed so only bus and hardware identity remain.",
            "No computer name, user name, domain name, document content, or serial number is collected.",
        };

        if (_options.IncludeDocumentNames)
        {
            notes.Add("Document names are included because --include-document-names was explicitly supplied.");
        }
        else
        {
            notes.Add("Document names are replaced with <redacted-document>.");
        }

        if (_options.ShowNetworkAddresses)
        {
            notes.Add("IPv4 addresses are included because --show-network-addresses was explicitly supplied.");
        }
        else
        {
            notes.Add("IPv4 addresses are masked to /24 (for example, 192.168.1.*).");
        }

        return new PrivacyReport
        {
            PolicyVersion = _options.PolicyVersion,
            DocumentNames = _options.IncludeDocumentNames ? "included" : "redacted",
            NetworkAddresses = _options.ShowNetworkAddresses ? "included" : "masked",
            UserIdentifiers = "omitted",
            MachineIdentifiers = "masked",
            RedactedFields = _counters.Total,
            Notes = notes,
        };
    }

    private sealed class RedactionCounters
    {
        public int DocumentNames { get; set; }
        public int NetworkAddresses { get; set; }
        public int Identifiers { get; set; }
        public int Total => DocumentNames + NetworkAddresses + Identifiers;
    }
}
