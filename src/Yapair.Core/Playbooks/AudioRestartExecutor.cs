using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Yapair.Core.Playbooks;

[SupportedOSPlatform("windows")]
public sealed class AudioRestartExecutor : IRepairExecutor
{
    public string PlaybookId => "audio.restart.v1";
    public RiskLevel Risk => RiskLevel.R1;

    public async Task<RepairResult> ExecuteAsync(CancellationToken ct = default)
    {
        try
        {
            // Windows audio architecture: Audiosrv depends on AudioEndpointBuilder
            using var audioSrv = new ServiceController("Audiosrv");
            using var endpointBuilder = new ServiceController("AudioEndpointBuilder");

            if (audioSrv.Status != ServiceControllerStatus.Stopped)
            {
                if (audioSrv.CanStop)
                {
                    audioSrv.Stop();
                    await WaitAsync(audioSrv, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20), ct);
                }
            }

            if (endpointBuilder.Status != ServiceControllerStatus.Stopped)
            {
                if (endpointBuilder.CanStop)
                {
                    endpointBuilder.Stop();
                    await WaitAsync(endpointBuilder, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20), ct);
                }
            }

            endpointBuilder.Start();
            await WaitAsync(endpointBuilder, ServiceControllerStatus.Running, TimeSpan.FromSeconds(20), ct);

            audioSrv.Start();
            await WaitAsync(audioSrv, ServiceControllerStatus.Running, TimeSpan.FromSeconds(20), ct);

            return new RepairResult(true, "Windows ses hizmetleri (AudioEndpointBuilder, Audiosrv) basariyla yeniden baslatildi");
        }
        catch (System.TimeoutException tex)
        {
            return new RepairResult(false, "Ses servisi zaman asimi: " + tex.Message);
        }
        catch (InvalidOperationException iex)
        {
            return new RepairResult(false, "Ses servisi bulunamadi veya erisilemedi: " + iex.Message);
        }
        catch (Exception ex)
        {
            return new RepairResult(false, "Ses servisi onarim hatasi: " + ex.Message);
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
