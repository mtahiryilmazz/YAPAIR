using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;
using Yapair.Discovery.Windows.Mapping;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class PnpAdapter
{
    public AdapterResult<PnpDeviceInfo> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var devices = Wmi.Query<PnpDeviceInfo?>("SELECT * FROM Win32_PnPEntity", options.PerSourceTimeout, Map)
                .OfType<PnpDeviceInfo>()
                .Where(static device => !string.IsNullOrWhiteSpace(device.FriendlyName) || !string.IsNullOrWhiteSpace(device.DeviceId))
                .OrderBy(static device => device.Class, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static device => device.FriendlyName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return AdapterResult<PnpDeviceInfo>.Ok(devices);
        }
        catch (Exception ex)
        {
            return AdapterFailure.FromException<PnpDeviceInfo>(ex);
        }
    }

    private static PnpDeviceInfo? Map(System.Management.ManagementBaseObject item)
    {
        var pnpClass = Wmi.String(item, "PNPClass");
        var errorCode = Wmi.Int32(item, "ConfigManagerErrorCode");
        var present = Wmi.Boolean(item, "Present");

        // Keep every present device that reports a configuration problem, plus
        // the device classes that matter for printing and audio diagnostics. This
        // covers UAC-era "device not installed" errors without enumerating every
        // healthy device.
        var relevant = present
            && (errorCode != 0
                || string.Equals(pnpClass, "Printer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pnpClass, "PrintQueue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pnpClass, "AudioEndpoint", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pnpClass, "MEDIA", StringComparison.OrdinalIgnoreCase));

        if (!relevant)
        {
            return null;
        }

        return new PnpDeviceInfo
        {
            FriendlyName = Wmi.String(item, "Name"),
            Class = pnpClass,
            DeviceId = Wmi.String(item, "DeviceID"),
            HardwareIds = Wmi.StringArray(item, "HardwareID"),
            Status = Wmi.String(item, "Status"),
            ConfigManagerErrorCode = errorCode,
            ErrorMeaning = WindowsStatusMap.ConfigManagerError(errorCode),
            Manufacturer = Wmi.String(item, "Manufacturer"),
            Service = Wmi.String(item, "Service"),
        };
    }
}
