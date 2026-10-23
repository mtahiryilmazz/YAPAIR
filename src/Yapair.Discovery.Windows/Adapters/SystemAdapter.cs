using Yapair.Core.Contracts;
using Yapair.Discovery.Windows.Internal;

namespace Yapair.Discovery.Windows.Adapters;

internal sealed class SystemAdapter
{
    public AdapterResult<Finding> Read(DiscoveryOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var findings = new List<Finding>();
            var memory = Wmi.Query(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem",
                options.PerSourceTimeout,
                item => new
                {
                    TotalKb = Wmi.Int64(item, "TotalVisibleMemorySize"),
                    FreeKb = Wmi.Int64(item, "FreePhysicalMemory"),
                }).FirstOrDefault();

            if (memory is not null && memory.TotalKb > 0)
            {
                var freeRatio = (double)memory.FreeKb / memory.TotalKb;
                if (freeRatio < 0.10)
                {
                    findings.Add(new Finding
                    {
                        Source = SourceNames.System,
                        Code = "low_memory",
                        Domain = HealthDomain.System,
                        Severity = freeRatio < 0.05 ? FindingSeverity.Error : FindingSeverity.Warning,
                        Subject = "memory",
                        Summary = $"Kullanılabilir fiziksel bellek düşük: {FormatGb(memory.FreeKb)} GB boş / {FormatGb(memory.TotalKb)} GB toplam.",
                        Evidence = [$"free_kb={memory.FreeKb}", $"total_kb={memory.TotalKb}"],
                        PlaybookId = "system.memory.review.v1",
                    });
                }
            }

            return AdapterResult<Finding>.Ok(findings);
        }
        catch (Exception ex)
        {
            return AdapterFailure.FromException<Finding>(ex);
        }
    }

    private static string FormatGb(long kilobytes) =>
        (kilobytes / 1024.0 / 1024.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
}
