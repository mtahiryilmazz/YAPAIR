using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Yapair.Core.Playbooks;

[SupportedOSPlatform("windows")]
public sealed class SpoolerRestartExecutor : IRepairExecutor
{
    public string PlaybookId => "printers.spooler.restart.v1";
    public RiskLevel Risk => RiskLevel.R1;

    public async Task<RepairResult> ExecuteAsync(CancellationToken ct = default)
    {
        try
        {
            using var sc = new ServiceController("Spooler");
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                if (!sc.CanStop) return new RepairResult(false, "Spooler durdurulamaz");
                sc.Stop();
                await WaitAsync(sc, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30), ct);
            }
            sc.Start();
            await WaitAsync(sc, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30), ct);
            return new RepairResult(true, "Spooler yeniden baslatildi");
        }
        catch (System.TimeoutException tex)
        {
            return new RepairResult(false, "Zaman asimi: " + tex.Message);
        }
        catch (InvalidOperationException iex)
        {
            return new RepairResult(false, "Spooler servisi bulunamadi: " + iex.Message);
        }
        catch (Exception ex)
        {
            return new RepairResult(false, ex.Message);
        }
    }

    private static async Task WaitAsync(ServiceController sc, ServiceControllerStatus want, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            sc.Refresh();
            if (sc.Status == want) return;
            await Task.Delay(500, ct);
        }
        throw new System.TimeoutException($"Servis {want} durumuna ulasmadi");
    }
}
