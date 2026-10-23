using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;

namespace Yapair.Discovery.Windows;

/// <summary>
/// Reads a stable hardware identifier once and immediately reduces it to a hash.
/// The raw SMBIOS UUID/serial is not retained or written to the case store.
/// </summary>
public sealed class WindowsMachineIdentityProvider
{
    public MachineIdentity GetIdentity(TimeSpan timeout)
    {
        var hardwareId = ReadHardwareId(timeout);
        return MachineIdentity.Create(Environment.MachineName, hardwareId);
    }

    private static string? ReadHardwareId(TimeSpan timeout)
    {
        try
        {
            var uuid = Wmi.Query(
                "SELECT UUID FROM Win32_ComputerSystemProduct",
                timeout,
                item => Wmi.String(item, "UUID")).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(uuid))
            {
                return uuid;
            }
        }
        catch (Exception)
        {
            // Fall back to the BIOS serial, then to the computer name.
        }

        try
        {
            var serial = Wmi.Query(
                "SELECT SerialNumber FROM Win32_BIOS",
                timeout,
                item => Wmi.String(item, "SerialNumber")).FirstOrDefault();

            return string.IsNullOrWhiteSpace(serial) ? null : serial;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
