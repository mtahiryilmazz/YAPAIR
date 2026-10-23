using System.ServiceProcess;
using Yapair.Core.Contracts;
using Yapair.Core.Serialization;

namespace Yapair.Discovery.Windows.Tests;

[Trait("Category", "Integration")]
public sealed class WindowsDiscoveryIntegrationTests
{
    [Fact]
    public async Task ReadOnlyCollectionProducesValidSnapshotWithoutChangingSpoolerState()
    {
        using var spooler = new ServiceController("Spooler");
        spooler.Refresh();
        var statusBefore = spooler.Status;

        var snapshot = await new WindowsDiscovery().CollectAsync(new DiscoveryOptions
        {
            PerSourceTimeout = TimeSpan.FromSeconds(20),
            EventLookbackHours = 24,
            MaxEvents = 10,
        });

        spooler.Refresh();
        Assert.Equal(statusBefore, spooler.Status);

        var validation = SnapshotSchema.Validate(snapshot);
        Assert.True(validation.IsValid, validation.DescribeErrors());

        Assert.Equal("redacted", snapshot.Privacy.DocumentNames);
        Assert.Equal("masked", snapshot.Privacy.NetworkAddresses);
        Assert.DoesNotContain(snapshot.Sources, source => source.Source == SourceNames.Ipp && source.Status == SourceStatus.Success);
        Assert.Contains(snapshot.Sources, source => source.Source == SourceNames.Printers);
        Assert.Contains(snapshot.Sources, source => source.Source == SourceNames.Services);
        Assert.Contains(snapshot.Sources, source => source.Source == SourceNames.Security);
        Assert.Contains(snapshot.Sources, source => source.Source == SourceNames.Network);
        Assert.Contains(snapshot.DomainsChecked, domain => domain == HealthDomain.Printers);
        Assert.Contains(snapshot.DomainsChecked, domain => domain == HealthDomain.Security);
        Assert.NotNull(snapshot.Spooler);
        Assert.All(snapshot.Printers, printer => Assert.False(printer.Name.Contains("Fatura", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task IppForUnknownPrinterIsReportedAsFailedAndDoesNotThrow()
    {
        var snapshot = await new WindowsDiscovery().CollectAsync(new DiscoveryOptions
        {
            PerSourceTimeout = TimeSpan.FromSeconds(20),
            MaxEvents = 0,
            IppPrinterName = "YAPAIR_Test_Printer_That_Does_Not_Exist",
        });

        var ippSource = snapshot.Sources.Single(source => source.Source == SourceNames.Ipp);
        Assert.Equal(SourceStatus.Failed, ippSource.Status);
        Assert.Equal("ipp_printer_not_found", ippSource.ErrorCode);
        Assert.Contains(snapshot.Limitations, limitation => limitation.Contains("IPP", StringComparison.Ordinal));
    }
}
