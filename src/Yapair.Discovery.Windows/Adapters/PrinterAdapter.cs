using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;
using Yapair.Discovery.Windows.Mapping;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class PrinterAdapter
{
    public AdapterResult<PrinterInfo> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var printers = Wmi.Query("SELECT * FROM Win32_Printer", options.PerSourceTimeout, Map);
            return AdapterResult<PrinterInfo>.Ok(printers);
        }
        catch (Exception ex)
        {
            return AdapterFailure.FromException<PrinterInfo>(ex);
        }
    }

    private static PrinterInfo Map(System.Management.ManagementBaseObject item)
    {
        var portName = Wmi.String(item, "PortName");
        var statusCode = Wmi.Int32(item, "PrinterStatus");
        var detectedErrorState = Wmi.Int32(item, "DetectedErrorState");
        var extendedStatus = Wmi.Int32(item, "ExtendedPrinterStatus");
        var isNetwork = Wmi.Boolean(item, "Network") || WindowsStatusMap.LooksLikeNetworkPort(portName);

        return new PrinterInfo
        {
            Name = Wmi.String(item, "Name"),
            DriverName = Wmi.String(item, "DriverName"),
            PortName = portName,
            IsDefault = Wmi.Boolean(item, "Default"),
            IsShared = Wmi.Boolean(item, "Shared"),
            IsNetwork = isNetwork,
            IsLocal = Wmi.Boolean(item, "Local"),
            WorkOffline = Wmi.Boolean(item, "WorkOffline"),
            StatusCode = statusCode,
            Status = WindowsStatusMap.PrinterStatus(statusCode),
            DetectedErrorState = detectedErrorState,
            ErrorState = WindowsStatusMap.DetectedErrorState(detectedErrorState),
            ExtendedStatus = extendedStatus,
            QueuedJobCount = 0,
            QueueObserved = false,
        };
    }
}
