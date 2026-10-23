using Yapair.Core.Playbooks;

namespace Yapair.Core.Tests;

public sealed class PlaybookCatalogTests
{
    [Fact]
    public void IdentifiersAreUniqueAndVersioned()
    {
        var ids = PlaybookCatalog.All.Select(playbook => playbook.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(ids, id => Assert.Matches(@"^[a-z0-9_.]+\.v\d+$", id));
    }

    [Fact]
    public void EveryPlaybookHasPreconditionsPostChecksAndRollbackStatement()
    {
        Assert.All(PlaybookCatalog.All, playbook =>
        {
            Assert.NotEmpty(playbook.Steps);
            Assert.NotEmpty(playbook.Preconditions);
            Assert.NotEmpty(playbook.PostChecks);
            Assert.False(string.IsNullOrWhiteSpace(playbook.Rollback));
            Assert.False(string.IsNullOrWhiteSpace(playbook.Summary));
        });
    }

    [Fact]
    public void RiskyPlaybooksRequireApproval()
    {
        Assert.All(
            PlaybookCatalog.All.Where(playbook => playbook.Risk != PlaybookRisk.R0),
            playbook => Assert.True(playbook.RequiresApproval, playbook.Id));
    }

    [Fact]
    public void SecurityWeakeningIsNotMarkedAutomatic()
    {
        var uac = PlaybookCatalog.Find("security.uac.review.v1");

        Assert.NotNull(uac);
        Assert.Equal(PlaybookRisk.R0, uac.Risk);
        Assert.DoesNotContain(uac.Steps, step => step.Contains("set", StringComparison.OrdinalIgnoreCase));
    }
}
