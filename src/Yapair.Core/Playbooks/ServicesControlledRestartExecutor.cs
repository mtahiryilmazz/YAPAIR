using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Yapair.Core.Playbooks;

[SupportedOSPlatform("windows")]
public sealed class ServicesControlledRestartExecutor : IRepairExecutor
{
    public string PlaybookId => "services.controlled_restart.v1";
    public RiskLevel Risk => RiskLevel.R1;

    private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Spooler",
        "Audiosrv",
        "AudioEndpointBuilder",
        "wuauserv",
        "BITS",
        "W32Time",
        "LanmanWorkstation",
        "Themes"
    };

    private readonly string _serviceName;

    public ServicesControlledRestartExecutor(string serviceName = "Spooler")
    {
        _serviceName = serviceName;
    }

    public async Task<RepairResult> ExecuteAsync(CancellationToken ct = default)
    {
        if (!Allowlist.Contains(_serviceName))
        {
            return new RepairResult(false, $"'{_serviceName}' hizmeti guvenli yeniden baslatma allowlist'inde yer almiyor.");
        }

        try
        {
            using var sc = new ServiceController(_serviceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                if (!sc.CanStop)
                    return new RepairResult(false, $"'{_serviceName}' hizmeti durdurulamaz olarak yapilandirilmis.");

                sc.Stop();
                await WaitAsync(sc, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30), ct);
            }

            sc.Start();
            await WaitAsync(sc, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30), ct);

            return new RepairResult(true, $"'{_serviceName}' hizmeti kontrollu olarak basariyla yeniden baslatildi.");
        }
        catch (System.TimeoutException tex)
        {
            return new RepairResult(false, $"'{_serviceName}' zaman asimi: {tex.Message}");
        }
        catch (InvalidOperationException iex)
        {
            return new RepairResult(false, $"'{_serviceName}' servisi bulunamadi: {iex.Message}");
        }
        catch (Exception ex)
        {
            return new RepairResult(false, $"'{_serviceName}' yeniden baslatma hatasi: {ex.Message}");
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
        throw new System.TimeoutException($"Servis {sc.ServiceName} {want} durumuna ulasmadi");
    }
}
