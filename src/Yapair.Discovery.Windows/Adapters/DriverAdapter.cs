using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class DriverAdapter
{
    public AdapterResult<DriverInfo> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var drivers = Wmi.Query("SELECT * FROM Win32_PrinterDriver", options.PerSourceTimeout, Map)
                .OrderBy(static driver => driver.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return AdapterResult<DriverInfo>.Ok(drivers);
        }
        catch (Exception ex)
        {
            return AdapterFailure.FromException<DriverInfo>(ex);
        }
    }

    private static DriverInfo Map(System.Management.ManagementBaseObject item)
    {
        var rawName = Wmi.String(item, "Name");
        var name = rawName;
        var comma = rawName.IndexOf(',', StringComparison.Ordinal);
        if (comma > 0)
        {
            name = rawName[..comma].Trim();
        }

        var infName = Wmi.String(item, "InfName");
        var fileName = System.IO.Path.GetFileName(infName);

        return new DriverInfo
        {
            Name = name,
            Version = Wmi.String(item, "Version"),
            Platform = Wmi.String(item, "SupportedPlatform"),
            InfName = string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName,
        };
    }
}
