using System.IO;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Yapair.Core.Playbooks;

[SupportedOSPlatform("windows")]
public sealed class PrinterQueueClearExecutor : IRepairExecutor
{
    public string PlaybookId => "printers.queue.clear.v1";
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

            var spoolPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "spool", "PRINTERS");

            int deletedCount = 0;
            if (Directory.Exists(spoolPath))
            {
                foreach (var file in Directory.GetFiles(spoolPath, "*.*"))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".shd" || ext == ".spl" || ext == ".tmp")
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch { }
                    }
                }
            }

            sc.Start();
            await WaitAsync(sc, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30), ct);

            return new RepairResult(true, $"Yazdirma biriktiricisi sifirlandi. {deletedCount} takilmis kuyruk dosyasi temizlendi.");
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
            return new RepairResult(false, "Kuyruk temizleme hatasi: " + ex.Message);
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
