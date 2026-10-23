using System.Text.Json.Nodes;
using Yapair.Core.Contracts;
using Yapair.Core.Serialization;
using Yapair.TestSupport;

namespace Yapair.Core.Tests;

public sealed class SchemaTests
{
    [Fact]
    public void HealthyFixtureMatchesV1Schema()
    {
        var snapshot = SnapshotFixture.CreateHealthy();

        var result = SnapshotSchema.Validate(snapshot);

        Assert.True(result.IsValid, result.DescribeErrors());
    }

    [Fact]
    public void SerializedNamesAreStableSnakeCase()
    {
        var json = SnapshotJson.Serialize(SnapshotFixture.CreateHealthy());

        Assert.Contains("\"schema_version\":\"1.0\"", json);
        Assert.Contains("\"snapshot_id\":\"snap-0123456789ab\"", json);
        Assert.Contains("\"is_64_bit\":true", json);
        Assert.Contains("\"domains_checked\"", json);
        Assert.Contains("\"windows_update\"", json);
        Assert.Contains("\"findings\":[]", json);
    }

    [Fact]
    public void MissingFindingsSectionFailsSchema()
    {
        var node = SnapshotJson.ToNode(SnapshotFixture.CreateHealthy());
        Assert.True(node is JsonObject);
        var obj = node.AsObject();
        Assert.True(obj.Remove("findings"));

        var result = SnapshotSchema.Validate(obj);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void UnknownStatusFailsSchema()
    {
        var obj = SnapshotJson.ToNode(SnapshotFixture.CreateHealthy()).AsObject();
        obj["status"] = "healthy";

        var result = SnapshotSchema.Validate(obj);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void FindingWithoutPlaybookIdIsStillValid()
    {
        var snapshot = SnapshotFixture.CreateHealthy() with
        {
            Findings =
            [
                new Finding
                {
                    Source = SourceNames.EventLogs,
                    Code = "recent_error_events",
                    Domain = HealthDomain.System,
                    Severity = FindingSeverity.Warning,
                    Subject = "event_log",
                    Summary = "Test",
                    PlaybookId = null,
                },
            ],
        };

        var result = SnapshotSchema.Validate(snapshot);

        Assert.True(result.IsValid, result.DescribeErrors());
    }
}
