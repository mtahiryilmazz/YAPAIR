using Yapair.Core.Contracts;
using Yapair.TestSupport;

namespace Yapair.Core.Tests;

public sealed class SnapshotStatusRulesTests
{
    [Fact]
    public void AllSuccessfulIsSuccess()
    {
        var status = SnapshotStatusRules.Aggregate(
        [
            Source(SourceStatus.Success),
            Source(SourceStatus.Disabled),
        ]);

        Assert.Equal(SnapshotStatus.Success, status);
    }

    [Fact]
    public void OnePartialIsPartial()
    {
        var status = SnapshotStatusRules.Aggregate(
        [
            Source(SourceStatus.Success),
            Source(SourceStatus.Partial),
        ]);

        Assert.Equal(SnapshotStatus.Partial, status);
    }

    [Fact]
    public void AllEnabledFailedIsFailed()
    {
        var status = SnapshotStatusRules.Aggregate(
        [
            Source(SourceStatus.Failed),
            Source(SourceStatus.Disabled),
        ]);

        Assert.Equal(SnapshotStatus.Failed, status);
    }

    [Fact]
    public void AllSourcesDisabledIsFailed()
    {
        var status = SnapshotStatusRules.Aggregate(
        [
            Source(SourceStatus.Disabled),
            Source(SourceStatus.Disabled),
        ]);

        Assert.Equal(SnapshotStatus.Failed, status);
    }

    private static SourceResult Source(SourceStatus status) => new()
    {
        Source = "test",
        Status = status,
        ObservedAt = SnapshotFixture.FixedTime,
    };
}
