using System.Diagnostics;

namespace Yapair.Admin.Services;

public static class RemoteDesktopService
{
    public static async Task<bool> ConnectRdpAsync(string hostOrIp, string machineLabel, Func<string, string, Task>? logAudit = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hostOrIp)) return false;
            var target = hostOrIp.Trim();
            Process.Start(new ProcessStartInfo
            {
                FileName = "mstsc.exe",
                Arguments = $"/v:{target}",
                UseShellExecute = true
            });

            if (logAudit != null)
                await logAudit("remote_connect_rdp", $"RDP to {machineLabel} ({target})");

            return true;
        }
        catch { return false; }
    }

    public static async Task<bool> ConnectRustDeskAsync(string rustDeskId, string machineLabel, Func<string, string, Task>? logAudit = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rustDeskId)) return false;
            var url = $"rustdesk://{rustDeskId.Trim()}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            if (logAudit != null)
                await logAudit("remote_connect_rustdesk", $"RustDesk to {machineLabel} ({rustDeskId})");

            return true;
        }
        catch { return false; }
    }

    public static async Task<bool> ConnectAnyDeskAsync(string anyDeskId, string machineLabel, Func<string, string, Task>? logAudit = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(anyDeskId)) return false;
            var url = $"anydesk:{anyDeskId.Trim()}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            if (logAudit != null)
                await logAudit("remote_connect_anydesk", $"AnyDesk to {machineLabel} ({anyDeskId})");

            return true;
        }
        catch { return false; }
    }
}
