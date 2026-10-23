using System.Globalization;
using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class StorageAdapter
{
    public AdapterResult<Finding> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var findings = new List<Finding>();

            var volumes = Wmi.Query(
                "SELECT DeviceID, Size, FreeSpace, FileSystem FROM Win32_LogicalDisk WHERE DriveType = 3",
                options.PerSourceTimeout,
                item => new
                {
                    DeviceId = Wmi.String(item, "DeviceID"),
                    Size = Wmi.Int64(item, "Size"),
                    Free = Wmi.Int64(item, "FreeSpace"),
                    FileSystem = Wmi.String(item, "FileSystem"),
                });

            foreach (var volume in volumes)
            {
                if (volume.Size <= 0)
                {
                    continue;
                }

                var freeRatio = (double)volume.Free / volume.Size;
                if (freeRatio >= 0.10)
                {
                    continue;
                }

                var severity = freeRatio < 0.05 || volume.Free < 1L * 1024 * 1024 * 1024
                    ? FindingSeverity.Error
                    : FindingSeverity.Warning;

                findings.Add(new Finding
                {
                    Source = SourceNames.Storage,
                    Code = "disk_low_space",
                    Domain = HealthDomain.Storage,
                    Severity = severity,
                    Subject = volume.DeviceId,
                    Summary = $"{volume.DeviceId} sürücüsünde boş alan az: "
                        + $"{FormatBytes(volume.Free)} boş / {FormatBytes(volume.Size)} toplam "
                        + $"(%){freeRatio * 100:0.0}).",
                    Evidence =
                    [
                        $"drive={volume.DeviceId}",
                        $"filesystem={volume.FileSystem}",
                        $"free_bytes={volume.Free}",
                        $"total_bytes={volume.Size}",
                    ],
                    PlaybookId = "storage.free_space.review.v1",
                });
            }

            var disks = Wmi.Query(
                "SELECT DeviceID, Model, Status FROM Win32_DiskDrive",
                options.PerSourceTimeout,
                item => new
                {
                    DeviceId = Wmi.String(item, "DeviceID"),
                    Model = Wmi.String(item, "Model"),
                    Status = Wmi.String(item, "Status"),
                });

            foreach (var disk in disks)
            {
                if (string.IsNullOrWhiteSpace(disk.Status)
                    || string.Equals(disk.Status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                findings.Add(new Finding
                {
                    Source = SourceNames.Storage,
                    Code = "disk_status_error",
                    Domain = HealthDomain.Storage,
                    Severity = FindingSeverity.Error,
                    Subject = disk.DeviceId,
                    Summary = $"Disk '{disk.DeviceId}' ({disk.Model}) durumu normal değil: {disk.Status}.",
                    Evidence = [$"device={disk.DeviceId}", $"status={disk.Status}"],
                    PlaybookId = "storage.disk_health.review.v1",
                });
            }

            return AdapterResult<Finding>.Ok(findings);
        }
        catch (Exception ex)
        {
            return AdapterFailure.FromException<Finding>(ex);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1}", value, units[unit]);
    }
}
