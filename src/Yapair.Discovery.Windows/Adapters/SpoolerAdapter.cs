using System.ComponentModel;
using System.ServiceProcess;
using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class SpoolerAdapter
{
    public AdapterResult<ServiceInfo> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var service = new ServiceController("Spooler");
            service.Refresh();

            var pid = ReadProcessId(options);
            var info = new ServiceInfo
            {
                Name = service.ServiceName,
                DisplayName = service.DisplayName,
                Status = service.Status.ToString(),
                StartType = service.StartType.ToString(),
                ProcessId = pid,
            };

            return AdapterResult<ServiceInfo>.Ok(info);
        }
        catch (InvalidOperationException)
        {
            return AdapterResult<ServiceInfo>.Failed("spooler_not_found", "The Print Spooler service is not installed.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or Win32Exception)
        {
            return AdapterResult<ServiceInfo>.Unknown("spooler_access_denied", "The Print Spooler service could not be queried with the current permissions.");
        }
    }

    private static int? ReadProcessId(DiscoveryOptions options)
    {
        try
        {
            var processIds = Wmi.Query(
                "SELECT ProcessId FROM Win32_Service WHERE Name = 'Spooler'",
                options.PerSourceTimeout,
                item => Wmi.Int64(item, "ProcessId"));

            var value = processIds.FirstOrDefault();
            return value is > 0 and <= int.MaxValue ? (int)value : null;
        }
        catch (Exception)
        {
            // The service state is still useful even if the PID is unavailable.
            return null;
        }
    }
}
