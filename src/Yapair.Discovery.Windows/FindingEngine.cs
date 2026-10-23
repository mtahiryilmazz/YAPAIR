using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows;

/// <summary>
/// Deterministic rule layer over collected evidence. It is intentionally
/// independent of the printer module: printers, devices, services, security,
/// storage, networking, and update state all become the same Finding shape.
/// </summary>
internal static class FindingEngine
{
    public static IReadOnlyList<Finding> Build(
        IReadOnlyList<PrinterInfo> printers,
        SourceStatus spoolerStatus,
        ServiceInfo? spooler,
        SourceStatus pnpStatus,
        IReadOnlyList<PnpDeviceInfo> pnpDevices,
        IReadOnlyList<IppCheckInfo> ippChecks,
        EventLogSection eventLogs,
        IReadOnlyList<Finding> adapterFindings)
    {
        var findings = new List<Finding>(adapterFindings);

        if (spoolerStatus is SourceStatus.Success or SourceStatus.Partial
            && spooler is not null
            && !string.Equals(spooler.Status, "Running", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding
            {
                Source = SourceNames.Spooler,
                Code = "spooler_not_running",
                Domain = HealthDomain.Printers,
                Severity = FindingSeverity.Error,
                Subject = "Print Spooler",
                Summary = $"Yazdırma Biriktiricisi (Spooler) çalışmıyor; durum: {spooler.Status}."
                    + " Yazdırma kuyruğu ve yazıcı işlemleri bu nedenle başarısız olabilir.",
                Evidence = [$"service={spooler.Name}", $"status={spooler.Status}", $"start_type={spooler.StartType}"],
                PlaybookId = "printers.spooler.restart.v1",
            });
        }

        foreach (var printer in printers)
        {
            if (printer.WorkOffline)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Printers,
                    Code = "printer_offline",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Error,
                    Subject = printer.Name,
                    Summary = $"'{printer.Name}' yazıcısı çevrimdışı olarak işaretlenmiş; yazdırma işleri kuyrukta bekleyebilir.",
                    Evidence = [$"printer={printer.Name}", $"port={printer.PortName}", "work_offline=true"],
                    PlaybookId = "printers.device.review.v1",
                });
            }

            if (printer.ErrorState is not "no_error" and not "unknown")
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Printers,
                    Code = "printer_error_state",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Error,
                    Subject = printer.Name,
                    Summary = $"'{printer.Name}' yazıcısı hata durumu bildiriyor: {printer.ErrorState}.",
                    Evidence = [$"printer={printer.Name}", $"error_state={printer.ErrorState}", $"status={printer.Status}"],
                    PlaybookId = "printers.device.review.v1",
                });
            }

            if (printer.QueueObserved
                && printer.QueuedJobCount > 0
                && (printer.WorkOffline || printer.ErrorState is not "no_error" and not "unknown"))
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Printers,
                    Code = "printer_queue_stalled",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Warning,
                    Subject = printer.Name,
                    Summary = $"'{printer.Name}' yazıcısında {printer.QueuedJobCount} iş kuyrukta ve yazıcı hata/çevrimdışı durumunda.",
                    Evidence = [$"printer={printer.Name}", $"queued_jobs={printer.QueuedJobCount}"],
                    PlaybookId = "printers.queue.inspect.v1",
                });
            }
        }

        foreach (var check in ippChecks)
        {
            if (check.Status is not (SourceStatus.Success or SourceStatus.Partial))
            {
                continue;
            }

            if (check.PrinterState == 5)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Ipp,
                    Code = "ipp_printer_stopped",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Error,
                    Subject = check.PrinterName,
                    Summary = $"'{check.PrinterName}' cihazı IPP üzerinden 'stopped' durumunu bildiriyor.",
                    Evidence = [$"printer_state={check.PrinterState}", $"uri={check.Uri}"],
                    PlaybookId = "printers.device.review.v1",
                });
            }

            if (check.AcceptingJobs == false)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Ipp,
                    Code = "ipp_not_accepting_jobs",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Warning,
                    Subject = check.PrinterName,
                    Summary = $"'{check.PrinterName}' cihazı yeni yazdırma işi kabul etmiyor.",
                    Evidence = [$"printer-is-accepting-jobs=false", $"uri={check.Uri}"],
                    PlaybookId = "printers.device.review.v1",
                });
            }

            var reasons = check.PrinterStateReasons
                .Where(reason => !string.Equals(reason, "none", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (reasons.Length > 0)
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.Ipp,
                    Code = "ipp_device_reason",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Error,
                    Subject = check.PrinterName,
                    Summary = $"'{check.PrinterName}' cihazı sorun nedeni bildiriyor: {string.Join(", ", reasons)}.",
                    Evidence = reasons.Select(reason => $"reason={reason}").ToArray(),
                    PlaybookId = "printers.device.review.v1",
                });
            }
        }

        if (pnpStatus is SourceStatus.Success or SourceStatus.Partial)
        {
            foreach (var device in pnpDevices.Where(static device => device.ConfigManagerErrorCode != 0))
            {
                findings.Add(new Finding
                {
                    Source = SourceNames.PnpDevices,
                    Code = "device_configuration_error",
                    Domain = DomainFor(device.Class),
                    Severity = FindingSeverity.Error,
                    Subject = string.IsNullOrWhiteSpace(device.FriendlyName) ? device.DeviceId : device.FriendlyName,
                    Summary = $"'{DeviceLabel(device)}' aygıtı yapılandırma hatası bildiriyor"
                        + $" (kod {device.ConfigManagerErrorCode}: {device.ErrorMeaning}).",
                    Evidence =
                    [
                        $"device_id={device.DeviceId}",
                        $"class={device.Class}",
                        $"config_manager_error_code={device.ConfigManagerErrorCode}",
                    ],
                    PlaybookId = "devices.driver.review.v1",
                });
            }
        }

        AddEventFindings(findings, eventLogs);

        return findings
            .OrderByDescending(static finding => finding.Severity)
            .ThenBy(static finding => finding.Domain)
            .ThenBy(static finding => finding.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static finding => finding.Subject, StringComparer.OrdinalIgnoreCase)
            .GroupBy(static finding => $"{finding.Source}|{finding.Code}|{finding.Subject}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static void AddEventFindings(List<Finding> findings, EventLogSection eventLogs)
    {
        var errorEvents = eventLogs.Records
            .Where(static record => record.LevelCode is 1 or 2)
            .ToArray();

        if (errorEvents.Length >= 5)
        {
            findings.Add(new Finding
            {
                Source = SourceNames.EventLogs,
                Code = "recent_error_events",
                Domain = HealthDomain.System,
                Severity = FindingSeverity.Warning,
                Subject = "event_log",
                Summary = $"Son {eventLogs.LookbackHours} saatte {errorEvents.Length} hata/kritik olay kaydedildi."
                    + " Tek başına kök neden değildir; ilgili kaynak ve olay kimlikleri incelenmelidir.",
                Evidence = errorEvents
                    .Take(5)
                    .Select(record => $"{record.LogName}/{record.Provider}/{record.EventId}")
                    .ToArray(),
                PlaybookId = null,
            });
        }

        var serviceCrashes = eventLogs.Records
            .Count(static record => record.EventId is 7031 or 7034);
        if (serviceCrashes > 0)
        {
            findings.Add(new Finding
            {
                Source = SourceNames.EventLogs,
                Code = "service_crash",
                Domain = HealthDomain.Services,
                Severity = FindingSeverity.Error,
                Subject = "Service Control Manager",
                Summary = $"Son {eventLogs.LookbackHours} saatte {serviceCrashes} hizmet beklenmedik şekilde sonlandı (7031/7034).",
                Evidence = ["event_ids=7031,7034"],
                PlaybookId = "services.crash.review.v1",
            });
        }

        if (eventLogs.Records.Any(static record => record.EventId == 1001))
        {
            findings.Add(new Finding
            {
                Source = SourceNames.EventLogs,
                Code = "system_crash",
                Domain = HealthDomain.System,
                Severity = FindingSeverity.Critical,
                Subject = "Windows",
                Summary = "Son olay kayıtlarında sistem çökmesi (BugCheck, olay 1001) görülüyor.",
                Evidence = ["event_id=1001"],
                PlaybookId = "system.crash.review.v1",
            });
        }

        if (eventLogs.Records.Any(static record => record.EventId == 4101))
        {
            findings.Add(new Finding
            {
                Source = SourceNames.EventLogs,
                Code = "display_driver_crash",
                Domain = HealthDomain.Devices,
                Severity = FindingSeverity.Warning,
                Subject = "Display",
                Summary = "Görüntü sürücüsü yanıt vermeyi durdurdu ve kurtarıldı (olay 4101).",
                Evidence = ["event_id=4101"],
                PlaybookId = "devices.display_driver.review.v1",
            });
        }
    }

    private static HealthDomain DomainFor(string pnpClass) => pnpClass switch
    {
        "Printer" or "PrintQueue" => HealthDomain.Printers,
        "AudioEndpoint" or "MEDIA" => HealthDomain.Audio,
        "Net" => HealthDomain.Network,
        "DiskDrive" or "SCSIAdapter" or "Volume" => HealthDomain.Storage,
        _ => HealthDomain.Devices,
    };

    private static string DeviceLabel(PnpDeviceInfo device) =>
        !string.IsNullOrWhiteSpace(device.FriendlyName)
            ? device.FriendlyName
            : string.IsNullOrWhiteSpace(device.DeviceId) ? device.Class : device.DeviceId;
}
