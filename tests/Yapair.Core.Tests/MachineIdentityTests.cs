using Yapair.Core.Contracts;

namespace Yapair.Core.Tests;

public sealed class MachineIdentityTests
{
    [Fact]
    public void HardwareIdProducesStablePseudonymousKey()
    {
        var first = MachineIdentity.Create("PC-01", "4C4C4544-0037-3910-8051-B4C04F4A4E31");
        var second = MachineIdentity.Create("PC-01", "4C4C4544-0037-3910-8051-B4C04F4A4E31");

        Assert.Equal(first.Key, second.Key);
        Assert.Equal("PC-01", first.Label);
        Assert.Equal(64, first.Key.Length);
        Assert.Matches("^[0-9a-f]{64}$", first.Key);
        Assert.Equal(12, first.ShortKey.Length);
    }

    [Fact]
    public void DifferentHardwareProducesDifferentKey()
    {
        var first = MachineIdentity.Create("PC-01", "HWID-A");
        var second = MachineIdentity.Create("PC-01", "HWID-B");

        Assert.NotEqual(first.Key, second.Key);
    }

    [Fact]
    public void ComputerNameIsOnlyAFallbackWhenHardwareIdIsMissing()
    {
        var fallback = MachineIdentity.Create("PC-01", null);

        Assert.Equal("PC-01", fallback.Label);
        Assert.Equal(64, fallback.Key.Length);
    }
}
