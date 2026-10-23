using System.Net.NetworkInformation;
using System.Security.Principal;
using Microsoft.Win32;
using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class OsAdapter
{
    public AdapterResult<OsInfo> Read(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadataError = false;
        string productName = string.Empty;
        string displayVersion = string.Empty;
        string build = string.Empty;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                productName = key.GetValue("ProductName") as string ?? string.Empty;
                displayVersion = key.GetValue("DisplayVersion") as string
                    ?? key.GetValue("ReleaseId") as string
                    ?? key.GetValue("CurrentVersion") as string
                    ?? string.Empty;
                build = key.GetValue("CurrentBuildNumber") as string
                    ?? key.GetValue("CurrentBuild") as string
                    ?? string.Empty;

                var ubr = key.GetValue("UBR");
                if (ubr is not null && build.Length > 0)
                {
                    build = $"{build}.{Convert.ToInt32(ubr, System.Globalization.CultureInfo.InvariantCulture)}";
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            metadataError = true;
        }

        var os = new OsInfo
        {
            ProductName = productName,
            DisplayVersion = displayVersion,
            Build = build,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            Is64Bit = Environment.Is64BitOperatingSystem,
            IsElevated = IsElevated(),
            IsDomainJoined = IsDomainJoined(),
        };

        return metadataError
            ? AdapterResult<OsInfo>.Partial([os], "os_metadata_unavailable", "Some operating system metadata could not be read.")
            : AdapterResult<OsInfo>.Ok(os);
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static bool IsDomainJoined()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(IPGlobalProperties.GetIPGlobalProperties().DomainName);
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }
}
