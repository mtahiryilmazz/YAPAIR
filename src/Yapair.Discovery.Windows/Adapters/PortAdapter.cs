using Microsoft.Win32;
using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;
using Yapair.Discovery.Windows.Mapping;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class PortAdapter
{
    private const string PortsRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Ports";

    public AdapterResult<PortInfo> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Exception? registryError = null;
        var ports = new Dictionary<string, PortInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var name in ReadRegistryPortNames())
            {
                ports[name] = new PortInfo
                {
                    Name = name,
                    Protocol = WindowsStatusMap.PortProtocol(name),
                    Host = WindowsStatusMap.PortProtocol(name) == "tcp_ip" && IsIpv4(name) ? name : null,
                    Source = "registry",
                };
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            registryError = ex;
        }

        Exception? wmiError = null;
        try
        {
            var tcpPorts = Wmi.Query("SELECT * FROM Win32_TCPIPPrinterPort", options.PerSourceTimeout, Map);
            foreach (var tcpPort in tcpPorts)
            {
                if (ports.TryGetValue(tcpPort.Name, out var existing))
                {
                    ports[tcpPort.Name] = existing with
                    {
                        Protocol = "tcp_ip",
                        Host = tcpPort.Host,
                        PortNumber = tcpPort.PortNumber,
                        SnmpEnabled = tcpPort.SnmpEnabled,
                        Source = "combined",
                    };
                }
                else
                {
                    ports[tcpPort.Name] = tcpPort;
                }
            }
        }
        catch (Exception ex)
        {
            wmiError = ex;
        }

        var ordered = ports.Values
            .OrderBy(static port => port.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (registryError is not null && wmiError is not null)
        {
            var (code, message) = Wmi.MapException(wmiError);
            return AdapterResult<PortInfo>.Unknown(code, message);
        }

        if (registryError is not null || wmiError is not null)
        {
            var error = registryError ?? wmiError!;
            var (code, message) = Wmi.MapException(error);
            return AdapterResult<PortInfo>.Partial(ordered, code, message);
        }

        return AdapterResult<PortInfo>.Ok(ordered);
    }

    private static IReadOnlyList<string> ReadRegistryPortNames()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(PortsRegistryPath);
        if (key is null)
        {
            return [];
        }

        return key.GetValueNames()
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PortInfo Map(System.Management.ManagementBaseObject item)
    {
        var name = Wmi.String(item, "Name");
        var host = Wmi.String(item, "HostAddress");
        var portNumber = Wmi.Int32(item, "PortNumber");

        return new PortInfo
        {
            Name = name,
            Protocol = "tcp_ip",
            Host = string.IsNullOrWhiteSpace(host) ? null : host,
            PortNumber = portNumber is > 0 and <= 65535 ? portNumber : null,
            SnmpEnabled = Wmi.Boolean(item, "SNMPEnabled"),
            Source = "wmi",
        };
    }

    private static bool IsIpv4(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var octet)
                || octet is < 0 or > 255)
            {
                return false;
            }
        }

        return true;
    }
}
