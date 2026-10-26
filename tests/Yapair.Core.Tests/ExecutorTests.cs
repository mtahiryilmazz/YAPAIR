using System.Runtime.Versioning;
using Yapair.Core.Playbooks;

namespace Yapair.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class ExecutorTests
{
    [Fact]
    public void AllExecutorsMatchPlaybookCatalogDefinitions()
    {
        var executors = new IRepairExecutor[]
        {
            new SpoolerRestartExecutor(),
            new AudioRestartExecutor(),
            new PrinterQueueClearExecutor(),
            new ServicesControlledRestartExecutor(),
            new NetworkAdapterResetExecutor()
        };

        foreach (var executor in executors)
        {
            var playbook = PlaybookCatalog.Find(executor.PlaybookId);
            Assert.NotNull(playbook);

            var expectedRisk = executor.Risk switch
            {
                RiskLevel.R0 => PlaybookRisk.R0,
                RiskLevel.R1 => PlaybookRisk.R1,
                RiskLevel.R2 => PlaybookRisk.R2,
                RiskLevel.R3 => PlaybookRisk.R3,
                _ => PlaybookRisk.R3
            };

            Assert.Equal(expectedRisk, playbook.Risk);
            Assert.True(playbook.RequiresApproval);
        }
    }

    [Fact]
    public async Task ServicesControlledRestartRejectsDisallowedService()
    {
        var executor = new ServicesControlledRestartExecutor("MaliciousOrUnsafeService");
        var result = await executor.ExecuteAsync();

        Assert.False(result.Success);
        Assert.Contains("allowlist", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
