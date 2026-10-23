using Microsoft.Win32;
using Yapair.Core.Contracts;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class WindowsUpdateAdapter
{
    private const string CbsRebootPending = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending";
    private const string WuRebootRequired = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired";
    private const string SessionManager = @"SYSTEM\CurrentControlSet\Control\Session Manager";

    public AdapterResult<Finding> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var evidence = new List<string>();
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            using (var cbs = baseKey.OpenSubKey(CbsRebootPending))
            {
                if (cbs is not null)
                {
                    evidence.Add("component_based_servicing_reboot_pending");
                }
            }

            using (var wu = baseKey.OpenSubKey(WuRebootRequired))
            {
                if (wu is not null)
                {
                    evidence.Add("windows_update_reboot_required");
                }
            }

            using (var session = baseKey.OpenSubKey(SessionManager))
            {
                if (session?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 })
                {
                    evidence.Add("pending_file_rename_operations");
                }
            }

            if (evidence.Count == 0)
            {
                return AdapterResult<Finding>.Ok([]);
            }

            return AdapterResult<Finding>.Ok(new Finding
            {
                Source = SourceNames.WindowsUpdate,
                Code = "pending_reboot",
                Domain = HealthDomain.WindowsUpdate,
                Severity = FindingSeverity.Warning,
                Subject = "Windows",
                Summary = "Yeniden başlatma bekleyen bir Windows güncellemesi veya bileşen işlemi var."
                    + " Bu durum güncellemelerin, sürücü kurulumlarının veya bazı ayarların tamamlanmasını engelleyebilir.",
                Evidence = evidence,
                PlaybookId = "windows_update.pending_reboot.review.v1",
            });
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return AdapterResult<Finding>.Unknown(
                "windows_update_state_unavailable",
                "Pending-reboot state could not be read with the current permissions.");
        }
    }
}
