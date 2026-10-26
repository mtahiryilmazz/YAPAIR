using System.Diagnostics;
using System.Runtime.Versioning;

namespace Yapair.Core.Playbooks;

[SupportedOSPlatform("windows")]
public sealed class NetworkAdapterResetExecutor : IRepairExecutor
{
    public string PlaybookId => "network.adapter.reset.v1";
    public RiskLevel Risk => RiskLevel.R2;

    public async Task<RepairResult> ExecuteAsync(CancellationToken ct = default)
    {
        try
        {
            // Restart-NetAdapter for active up adapters via PowerShell
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"$adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -or $_.Status -eq 'Disconnected' }; if ($adapters) { $adapters | Restart-NetAdapter; Write-Output ($adapters.Name -join ', ') } else { Write-Output 'none' }\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);

            var completed = await Task.WhenAny(proc.WaitForExitAsync(ct), Task.Delay(TimeSpan.FromSeconds(30), ct));
            if (completed != proc.WaitForExitAsync(ct))
            {
                try { proc.Kill(); } catch { }
                return new RepairResult(false, "Ag bagdastiricisi sifirlama zaman asimina ugradi.");
            }

            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();

            if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            {
                return new RepairResult(false, "Ag bagdastirici sifirlama hatasi: " + stderr);
            }

            var names = string.IsNullOrWhiteSpace(stdout) ? "varsayilan" : stdout;
            return new RepairResult(true, $"Ag bagdastiricisi basariyla yeniden baslatildi ({names}).");
        }
        catch (Exception ex)
        {
            return new RepairResult(false, "Ag bagdastiricisi onarim hatasi: " + ex.Message);
        }
    }
}
