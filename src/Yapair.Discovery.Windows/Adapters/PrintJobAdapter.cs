using System.Globalization;
using System.Management;
using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;
using Yapair.Discovery.Windows.Mapping;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class PrintJobAdapter
{
    public AdapterResult<PrintJobInfo> Read(DiscoveryOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var jobs = Wmi.Query(
                "SELECT * FROM Win32_PrintJob",
                options.PerSourceTimeout,
                item => Map(item, now));

            return AdapterResult<PrintJobInfo>.Ok(jobs);
        }
        catch (Exception ex)
        {
            return AdapterFailure.FromException<PrintJobInfo>(ex);
        }
    }

    private static PrintJobInfo Map(ManagementBaseObject item, DateTimeOffset now)
    {
        var statusMask = Wmi.Int32(item, "StatusMask");
        var submittedAt = ParseSubmittedAt(Wmi.String(item, "TimeSubmitted"));
        var size = Wmi.Int64(item, "Size") + (Wmi.Int64(item, "SizeHigh") << 32);

        return new PrintJobInfo
        {
            PrinterName = Wmi.String(item, "HostPrintQueue"),
            JobId = Wmi.Int32(item, "JobId"),
            Document = Wmi.String(item, "Document"),
            Status = Wmi.String(item, "JobStatus") is { Length: > 0 } jobStatus
                ? jobStatus
                : Wmi.String(item, "Status"),
            StatusMask = statusMask,
            StatusFlags = WindowsStatusMap.PrintJobStatusFlags(statusMask),
            TotalPages = Wmi.Int32(item, "TotalPages"),
            PagesPrinted = Wmi.Int32(item, "PagesPrinted"),
            SizeBytes = size < 0 ? 0 : size,
            SubmittedAt = submittedAt,
            AgeMinutes = submittedAt is null
                ? null
                : Math.Max(0, (now - submittedAt.Value).TotalMinutes),
        };
    }

    private static DateTimeOffset? ParseSubmittedAt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var local = ManagementDateTimeConverter.ToDateTime(value);
            return new DateTimeOffset(local);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return null;
        }
    }
}
