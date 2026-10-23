using System.Diagnostics;
using Yapair.Core.Contracts;
using Yapair.Core.Privacy;
using Yapair.Discovery.Windows.Ipp;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class IppAdapter
{
    public async Task<AdapterResult<IppCheckInfo>> ReadAsync(
        DiscoveryOptions options,
        IReadOnlyList<PrinterInfo> printers,
        IReadOnlyList<PortInfo> ports,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.IppPrinterName))
        {
            return AdapterResult<IppCheckInfo>.Disabled("IPP check was not requested.");
        }

        var printer = printers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, options.IppPrinterName, StringComparison.OrdinalIgnoreCase));

        if (printer is null)
        {
            return AdapterResult<IppCheckInfo>.Failed(
                "ipp_printer_not_found",
                "The selected printer was not present in the local printer list.");
        }

        var host = ResolveHost(printer, ports);
        if (host is null)
        {
            return AdapterResult<IppCheckInfo>.Failed(
                "ipp_host_not_found",
                "No IP address could be resolved from the selected printer port.");
        }

        var path = NormalizePath(options.IppPath);
        var httpUri = new Uri($"http://{host}:631{path}");
        var displayUri = $"ipp://{host}:631{path}";
        var stopwatch = Stopwatch.StartNew();
        var call = await IppClient.GetPrinterAttributesAsync(
            httpUri,
            options.IppTimeout,
            options.IppAllowInsecureTls,
            cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        return MapResult(printer.Name, call.EffectiveUri ?? displayUri, call, stopwatch.ElapsedMilliseconds);
    }

    private static AdapterResult<IppCheckInfo> MapResult(
        string printerName,
        string printerUri,
        IppCallResult call,
        long durationMs)
    {
        var message = call.Message;
        var ippStatus = message?.StatusCode;
        var state = message?.GetInt("printer-state");
        var reasons = message?.GetStrings("printer-state-reasons") ?? [];
        var check = new IppCheckInfo
        {
            PrinterName = printerName,
            Uri = printerUri,
            Status = SourceStatus.Unknown,
            HttpStatus = call.HttpStatus,
            IppStatus = ippStatus,
            IppStatusName = message?.StatusName,
            Message = call.ErrorMessage,
            ReportedPrinterName = message?.GetString("printer-name"),
            PrinterState = state,
            PrinterStateText = state is null ? null : IppClient.PrinterStateName(state.Value),
            PrinterStateReasons = reasons,
            AcceptingJobs = message?.GetBool("printer-is-accepting-jobs"),
            QueuedJobCount = message?.GetInt("queued-job-count"),
            MakeAndModel = message?.GetString("printer-make-and-model"),
            DurationMs = durationMs,
        };

        if (message is null)
        {
            var status = call.ErrorCode is "ipp_timeout" or "ipp_unreachable" or "ipp_transport_error" or "ipp_tls_certificate"
                ? SourceStatus.Unknown
                : SourceStatus.Failed;
            return new AdapterResult<IppCheckInfo>
            {
                Status = status,
                Items = [check with { Status = status }],
                ErrorCode = call.ErrorCode,
                Message = call.ErrorMessage,
            };
        }

        if (ippStatus is 0x0000)
        {
            if (state is null)
            {
                check = check with
                {
                    Status = SourceStatus.Partial,
                    Message = "IPP responded successfully but did not include printer-state.",
                };
                return new AdapterResult<IppCheckInfo>
                {
                    Status = SourceStatus.Partial,
                    Items = [check],
                    ErrorCode = "ipp_missing_state",
                    Message = check.Message,
                };
            }

            check = check with { Status = SourceStatus.Success };
            return AdapterResult<IppCheckInfo>.Ok(check);
        }

        check = check with
        {
            Status = SourceStatus.Partial,
            Message = $"IPP returned {message.StatusName}.",
        };
        return new AdapterResult<IppCheckInfo>
        {
            Status = SourceStatus.Partial,
            Items = [check],
            ErrorCode = "ipp_error_status",
            Message = check.Message,
        };
    }

    private static string? ResolveHost(PrinterInfo printer, IReadOnlyList<PortInfo> ports)
    {
        if (SensitiveDataMasker.IsIpv4(printer.PortName))
        {
            return printer.PortName;
        }

        var port = ports.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, printer.PortName, StringComparison.OrdinalIgnoreCase));

        if (port?.Host is { Length: > 0 } host && SensitiveDataMasker.IsIpv4(host))
        {
            return host;
        }

        if (printer.PortName.StartsWith("IP_", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = printer.PortName[3..];
            return SensitiveDataMasker.IsIpv4(candidate) ? candidate : null;
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/ipp/print";
        }

        return path.StartsWith('/') ? path : $"/{path}";
    }
}
