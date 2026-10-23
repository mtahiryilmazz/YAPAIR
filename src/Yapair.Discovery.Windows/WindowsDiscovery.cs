using System.Diagnostics;
using System.Globalization;
using Yapair.Core.Contracts;
using Yapair.Core.Privacy;
using Yapair.Core.Serialization;
using Yapair.Discovery.Windows.Adapters;

namespace Yapair.Discovery.Windows;

/// <summary>
/// Read-only Windows collector. The orchestrator runs each adapter under a hard
/// timeout, records an explicit outcome for it, and never converts an unavailable
/// source into a negative finding.
/// </summary>
public sealed class WindowsDiscovery
{
    private readonly TimeProvider _timeProvider;

    public WindowsDiscovery(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SystemSnapshot> CollectAsync(DiscoveryOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sources = new List<SourceResult>();
        var startedAt = _timeProvider.GetUtcNow();

        var osResult = await RunAsync(
            SourceNames.Os,
            token => new OsAdapter().Read(token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var spoolerResult = await RunAsync(
            SourceNames.Spooler,
            token => new SpoolerAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var printerResult = await RunAsync(
            SourceNames.Printers,
            token => new PrinterAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var jobResult = await RunAsync(
            SourceNames.PrintJobs,
            token => new PrintJobAdapter().Read(options, _timeProvider.GetUtcNow(), token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var portResult = await RunAsync(
            SourceNames.Ports,
            token => new PortAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var driverResult = await RunAsync(
            SourceNames.Drivers,
            token => new DriverAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var pnpResult = await RunAsync(
            SourceNames.PnpDevices,
            token => new PnpAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var eventResult = await RunAsync(
            SourceNames.EventLogs,
            token => new EventLogAdapter().Read(options, _timeProvider.GetUtcNow(), token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var systemResult = await RunAsync(
            SourceNames.System,
            token => new SystemAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var servicesResult = await RunAsync(
            SourceNames.Services,
            token => new ServicesAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var securityResult = await RunAsync(
            SourceNames.Security,
            token => new SecurityAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var networkResult = await RunAsync(
            SourceNames.Network,
            token => new NetworkAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var storageResult = await RunAsync(
            SourceNames.Storage,
            token => new StorageAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var updateResult = await RunAsync(
            SourceNames.WindowsUpdate,
            token => new WindowsUpdateAdapter().Read(options, token),
            options,
            sources,
            cancellationToken).ConfigureAwait(false);

        var queueObserved = jobResult.Status is SourceStatus.Success or SourceStatus.Partial;
        var jobs = jobResult.Items;
        var printers = ApplyQueueCounts(printerResult.Items, jobs, queueObserved);

        AdapterResult<IppCheckInfo> ippResult;
        if (string.IsNullOrWhiteSpace(options.IppPrinterName))
        {
            ippResult = AdapterResult<IppCheckInfo>.Disabled(
                "IPP check was not requested; pass --ipp <printer name> to query the selected device.");
            AddDisabled(sources, SourceNames.Ipp, ippResult.Message);
        }
        else
        {
            ippResult = await RunAsync(
                SourceNames.Ipp,
                token => new IppAdapter().ReadAsync(options, printers, portResult.Items, token),
                options,
                sources,
                cancellationToken).ConfigureAwait(false);
        }

        AddDisabled(
            sources,
            SourceNames.Snmp,
            "SNMP is disabled in Sprint 1; it is not used as a primary evidence source.");
        AddDisabled(
            sources,
            SourceNames.NetworkDiscovery,
            "Subnet scanning, mDNS, and WSD discovery are disabled in Sprint 1 by design.");

        var os = osResult.Items.FirstOrDefault() ?? new OsInfo();
        var spooler = spoolerResult.Items.FirstOrDefault();
        var eventBundle = eventResult.Items.FirstOrDefault();
        var eventLogs = new EventLogSection
        {
            LookbackHours = options.EventLookbackHours,
            Logs = eventBundle?.Logs ?? [],
            Records = eventBundle?.Records ?? [],
        };

        var adapterFindings = servicesResult.Items
            .Concat(securityResult.Items)
            .Concat(networkResult.Items)
            .Concat(storageResult.Items)
            .Concat(systemResult.Items)
            .Concat(updateResult.Items)
            .ToArray();

        var findings = FindingEngine.Build(
            printers,
            spoolerResult.Status,
            spooler,
            pnpResult.Status,
            pnpResult.Items,
            ippResult.Items,
            eventLogs,
            adapterFindings);

        var collectedAt = _timeProvider.GetUtcNow();
        var snapshot = new SystemSnapshot
        {
            SnapshotId = ("snap-" + Guid.NewGuid().ToString("N"))[..17],
            CollectedAt = collectedAt,
            Status = SnapshotStatusRules.Aggregate(sources),
            Os = os,
            Spooler = spooler,
            Printers = printers,
            PrintJobs = jobs,
            Ports = portResult.Items,
            Drivers = driverResult.Items,
            PnpDevices = pnpResult.Items,
            EventLogs = eventLogs,
            IppChecks = ippResult.Items,
            Findings = findings,
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
            Evidence = BuildEvidence(
                startedAt,
                os,
                osResult.Status,
                printerResult.Status,
                printers,
                portResult.Status,
                spoolerResult.Status,
                spooler,
                jobs,
                jobResult.Status,
                portResult.Items,
                pnpResult.Items,
                pnpResult.Status,
                eventLogs,
                eventResult.Status,
                ippResult.Items,
                ippResult.Status,
                findings),
            Sources = sources,
            Limitations = BuildLimitations(options, jobResult.Status, ippResult.Status),
            Privacy = new PrivacyReport(),
        };

        var redacted = new SnapshotRedactor(new RedactionOptions
        {
            IncludeDocumentNames = options.IncludeDocumentNames,
            ShowNetworkAddresses = options.ShowNetworkAddresses,
        }).Redact(snapshot);

        var validation = SnapshotSchema.Validate(redacted);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Snapshot failed schema validation: {validation.DescribeErrors()}");
        }

        return redacted;
    }

    private static IReadOnlyList<PrinterInfo> ApplyQueueCounts(
        IReadOnlyList<PrinterInfo> printers,
        IReadOnlyList<PrintJobInfo> jobs,
        bool queueObserved)
    {
        var counts = jobs
            .GroupBy(static job => job.PrinterName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return printers
            .Select(printer => printer with
            {
                QueuedJobCount = queueObserved ? counts.GetValueOrDefault(printer.Name) : 0,
                QueueObserved = queueObserved,
            })
            .ToArray();
    }

    private async Task<AdapterResult<T>> RunAsync<T>(
        string source,
        Func<CancellationToken, AdapterResult<T>> read,
        DiscoveryOptions options,
        ICollection<SourceResult> sources,
        CancellationToken cancellationToken)
    {
        var observedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        AdapterResult<T> result;

        var task = Task.Run(() => read(cancellationToken), CancellationToken.None);
        try
        {
            result = await task.WaitAsync(options.PerSourceTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            ObserveFault(task);
            result = AdapterResult<T>.Unknown("timeout", "The source did not complete before its timeout.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            result = AdapterResult<T>.Unknown("timeout", "The source did not complete before its timeout.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result = AdapterFailure.FromException<T>(ex);
        }

        stopwatch.Stop();
        sources.Add(new SourceResult
        {
            Source = source,
            Status = result.Status,
            ObservedAt = observedAt,
            DurationMs = stopwatch.ElapsedMilliseconds,
            ItemCount = result.ItemCountOverride ?? result.Items.Count,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
        });

        return result;
    }

    private async Task<AdapterResult<T>> RunAsync<T>(
        string source,
        Func<CancellationToken, Task<AdapterResult<T>>> read,
        DiscoveryOptions options,
        ICollection<SourceResult> sources,
        CancellationToken cancellationToken)
    {
        var observedAt = _timeProvider.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        AdapterResult<T> result;

        var task = read(cancellationToken);
        try
        {
            result = await task.WaitAsync(options.PerSourceTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            ObserveFault(task);
            result = AdapterResult<T>.Unknown("timeout", "The source did not complete before its timeout.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            result = AdapterResult<T>.Unknown("timeout", "The source did not complete before its timeout.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result = AdapterFailure.FromException<T>(ex);
        }

        stopwatch.Stop();
        sources.Add(new SourceResult
        {
            Source = source,
            Status = result.Status,
            ObservedAt = observedAt,
            DurationMs = stopwatch.ElapsedMilliseconds,
            ItemCount = result.ItemCountOverride ?? result.Items.Count,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
        });

        return result;
    }

    private void AddDisabled(ICollection<SourceResult> sources, string source, string? message)
    {
        sources.Add(new SourceResult
        {
            Source = source,
            Status = SourceStatus.Disabled,
            ObservedAt = _timeProvider.GetUtcNow(),
            DurationMs = 0,
            ItemCount = 0,
            Message = message,
        });
    }

    private static void ObserveFault(Task task)
    {
        _ = task.ContinueWith(
            static failed => _ = failed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static IReadOnlyList<EvidenceFact> BuildEvidence(
        DateTimeOffset observedAt,
        OsInfo os,
        SourceStatus osStatus,
        SourceStatus printerStatus,
        IReadOnlyList<PrinterInfo> printers,
        SourceStatus portStatus,
        SourceStatus spoolerStatus,
        ServiceInfo? spooler,
        IReadOnlyList<PrintJobInfo> jobs,
        SourceStatus jobStatus,
        IReadOnlyList<PortInfo> ports,
        IReadOnlyList<PnpDeviceInfo> pnpDevices,
        SourceStatus pnpStatus,
        EventLogSection eventLogs,
        SourceStatus eventStatus,
        IReadOnlyList<IppCheckInfo> ippChecks,
        SourceStatus ippStatus,
        IReadOnlyList<Finding> findings)
    {
        var facts = new List<EvidenceFact>();
        var osObserved = osStatus is SourceStatus.Success or SourceStatus.Partial;

        Add(
            SourceNames.Os,
            "os_build",
            osObserved && !string.IsNullOrWhiteSpace(os.Build) ? os.Build : "unknown",
            null,
            osObserved ? EvidenceQuality.Direct : EvidenceQuality.Unknown);

        var printersObserved = printerStatus is SourceStatus.Success or SourceStatus.Partial;
        Add(
            SourceNames.Printers,
            "printer_count",
            printersObserved ? printers.Count.ToString(CultureInfo.InvariantCulture) : "unknown",
            null,
            printersObserved ? EvidenceQuality.Direct : EvidenceQuality.Unknown);

        if (spoolerStatus is SourceStatus.Success or SourceStatus.Partial && spooler is not null)
        {
            Add(SourceNames.Spooler, "spooler_state", spooler.Status, null, EvidenceQuality.Direct);
        }
        else
        {
            Add(SourceNames.Spooler, "spooler_state", "unknown", null, EvidenceQuality.Unknown);
        }

        if (jobStatus is SourceStatus.Success or SourceStatus.Partial)
        {
            Add(
                SourceNames.PrintJobs,
                "print_job_count",
                jobs.Count.ToString(CultureInfo.InvariantCulture),
                null,
                EvidenceQuality.Direct);
        }
        else
        {
            Add(SourceNames.PrintJobs, "print_job_count", "unknown", null, EvidenceQuality.Unknown);
        }

        if (pnpStatus is SourceStatus.Success or SourceStatus.Partial)
        {
            Add(
                SourceNames.PnpDevices,
                "pnp_printer_count",
                pnpDevices.Count(static device => string.Equals(device.Class, "Printer", StringComparison.OrdinalIgnoreCase))
                    .ToString(CultureInfo.InvariantCulture),
                null,
                EvidenceQuality.Direct);
            Add(
                SourceNames.PnpDevices,
                "pnp_error_device_count",
                pnpDevices.Count(static device => device.ConfigManagerErrorCode != 0)
                    .ToString(CultureInfo.InvariantCulture),
                null,
                EvidenceQuality.Direct);
        }

        if (eventStatus is SourceStatus.Success or SourceStatus.Partial)
        {
            Add(
                SourceNames.EventLogs,
                "event_record_count",
                eventLogs.Records.Count.ToString(CultureInfo.InvariantCulture),
                null,
                EvidenceQuality.Direct);
        }

        var portsObserved = portStatus is SourceStatus.Success or SourceStatus.Partial;
        Add(
            SourceNames.Ports,
            "port_count",
            portsObserved ? ports.Count.ToString(CultureInfo.InvariantCulture) : "unknown",
            null,
            portsObserved ? EvidenceQuality.Direct : EvidenceQuality.Unknown);

        var disabledChannels = eventLogs.Logs.Count(static log => log.Status == SourceStatus.Disabled);
        if (disabledChannels > 0)
        {
            Add(
                SourceNames.EventLogs,
                "disabled_event_channel_count",
                disabledChannels.ToString(CultureInfo.InvariantCulture),
                null,
                EvidenceQuality.Direct);
        }

        foreach (var printer in printers)
        {
            Add(SourceNames.Printers, "printer_status", printer.Status, printer.Name, EvidenceQuality.Direct);

            if (printer.WorkOffline)
            {
                Add(SourceNames.Printers, "printer_offline", "true", printer.Name, EvidenceQuality.Direct);
            }

            if (printer.ErrorState is not "no_error" and not "unknown")
            {
                Add(SourceNames.Printers, "printer_error_state", printer.ErrorState, printer.Name, EvidenceQuality.Direct);
            }

            if (printer.QueueObserved)
            {
                Add(
                    SourceNames.Printers,
                    "printer_queue_length",
                    printer.QueuedJobCount.ToString(CultureInfo.InvariantCulture),
                    printer.Name,
                    EvidenceQuality.Direct);
            }
        }

        Add(
            SourceNames.System,
            "finding_count",
            findings.Count.ToString(CultureInfo.InvariantCulture),
            null,
            EvidenceQuality.Direct);

        if (findings.Count > 0)
        {
            Add(
                SourceNames.System,
                "highest_finding_severity",
                findings.Max(static finding => finding.Severity).ToString(),
                null,
                EvidenceQuality.Direct);
        }

        if (ippStatus is SourceStatus.Success or SourceStatus.Partial)
        {
            foreach (var check in ippChecks)
            {
                Add(
                    SourceNames.Ipp,
                    "ipp_printer_state",
                    check.PrinterStateText ?? "unknown",
                    check.PrinterName,
                    EvidenceQuality.Direct);

                if (check.PrinterStateReasons.Count > 0)
                {
                    Add(
                        SourceNames.Ipp,
                        "ipp_state_reasons",
                        string.Join(',', check.PrinterStateReasons),
                        check.PrinterName,
                        EvidenceQuality.Direct);
                }
            }
        }

        return facts;

        void Add(string source, string fact, string value, string? subject, EvidenceQuality quality)
        {
            facts.Add(new EvidenceFact
            {
                Source = source,
                Fact = fact,
                Value = value,
                Subject = subject,
                ObservedAt = observedAt,
                Quality = quality,
            });
        }
    }

    private static IReadOnlyList<string> BuildLimitations(
        DiscoveryOptions options,
        SourceStatus jobStatus,
        SourceStatus ippStatus)
    {
        var limitations = new List<string>
        {
            "Read-only discovery only: no repair tool, driver installation, policy change, or registry write is exposed in this build.",
            "The report covers system, services, devices, printers, audio, network, security posture, storage, and Windows Update state; it is not a complete inventory of every Windows setting.",
            "UAC, firewall, and group-policy checks read current policy keys only; the effective Group Policy resultant set is not evaluated yet.",
            "Network checks describe local adapter, gateway, and DNS configuration; no ping, DNS lookup, subnet scan, or internet reachability test is performed.",
            "Physical output, toner level, and paper jams cannot be verified by software; device-reported states are evidence, not proof of printed output.",
            "Printer status comes from Win32_Printer and may be stale while the spooler or device is offline.",
            "Event channels that are disabled or inaccessible are reported as disabled or unknown, never as 'no errors'.",
            "SNMP, mDNS/WSD discovery, subnet scanning, LLM triage, and web RAG are not implemented in Sprint 1.",
            "PnP enumeration covers present devices that report an error plus printer and audio device classes.",
        };

        if (jobStatus is not SourceStatus.Success and not SourceStatus.Partial)
        {
            limitations.Add("The print queue could not be observed; queued_job_count is not evidence that the queue is empty.");
        }

        if (string.IsNullOrWhiteSpace(options.IppPrinterName))
        {
            limitations.Add("IPP was not queried. Select one printer with --ipp <printer name> for device-reported state.");
        }
        else if (ippStatus is not SourceStatus.Success and not SourceStatus.Partial)
        {
            limitations.Add("The selected printer's IPP state could not be observed; the report contains no device-level verdict.");
        }

        return limitations;
    }
}
