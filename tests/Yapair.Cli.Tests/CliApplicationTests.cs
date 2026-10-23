using System.Text.Json;
using Yapair.Core.Serialization;
using Yapair.TestSupport;

namespace Yapair.Cli.Tests;

[Trait("Category", "Integration")]
public sealed class CliApplicationTests
{
    [Fact]
    public async Task HelpReturnsUsage()
    {
        var result = await RunAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("YAPAIR", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("verify", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownOptionReturnsUsageError()
    {
        var result = await RunAsync("discover", "--does-not-exist");

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("Bilinmeyen", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VersionReturnsAssemblyVersion()
    {
        var result = await RunAsync("version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("yapair 0.1.0", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SchemaCommandReturnsValidJson()
    {
        var result = await RunAsync("schema");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", document.RootElement.GetProperty("$schema").GetString());
    }

    [Fact]
    public async Task PlaybookCatalogueListsPrinterAndSystemEntries()
    {
        var result = await RunAsync("playbooks", "--format", "json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        var playbooks = document.RootElement.EnumerateArray().ToArray();
        Assert.Contains(playbooks, playbook => playbook.GetProperty("id").GetString() == "printers.spooler.restart.v1");
        Assert.Contains(playbooks, playbook => playbook.GetProperty("id").GetString() == "security.uac.review.v1");
        Assert.All(playbooks, playbook => Assert.False(string.IsNullOrWhiteSpace(playbook.GetProperty("post_checks").ToString())));
    }

    [Fact]
    public async Task VerifyWithoutBaselineIsUsageError()
    {
        var result = await RunAsync("verify");

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--baseline", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyWithInvalidBaselineReturnsInternalError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"yapair-bad-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{\"not\":\"a snapshot\"}");

        try
        {
            var result = await RunAsync("verify", "--baseline", path);

            Assert.Equal(4, result.ExitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DiscoverSummaryReturnsFindingsSection()
    {
        var result = await RunAsync("discover", "--format", "summary", "--timeout-ms", "15000");

        Assert.Contains(result.ExitCode, new[] { 0, 2 });
        Assert.Contains("YAPAIR Tanılama Raporu", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Bulgular", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Taranan alanlar", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverJsonMatchesEmbeddedSchema()
    {
        var result = await RunAsync("discover", "--format", "json", "--timeout-ms", "15000");

        Assert.Contains(result.ExitCode, new[] { 0, 2 });
        var snapshot = SnapshotJson.Deserialize(result.Stdout);
        var validation = SnapshotSchema.Validate(snapshot);
        Assert.True(validation.IsValid, validation.DescribeErrors());
        Assert.NotEmpty(snapshot.DomainsChecked);
        Assert.NotEmpty(snapshot.Sources);
    }

    [Fact]
    public async Task OutputFileIsWrittenAtomically()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"yapair-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "report.json");

        try
        {
            var result = await RunAsync("discover", "--format", "json", "--pretty", "--timeout-ms", "15000", "--output", path);

            Assert.Contains(result.ExitCode, new[] { 0, 2 });
            Assert.True(File.Exists(path));
            var snapshot = SnapshotJson.Deserialize(await File.ReadAllTextAsync(path));
            Assert.True(SnapshotSchema.Validate(snapshot).IsValid);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task<CliResult> RunAsync(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var stdin = new StringReader(string.Empty);

        var exitCode = await CliApplication.RunAsync(
            args,
            stdout,
            stderr,
            stdin,
            CancellationToken.None);

        return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliResult(int ExitCode, string Stdout, string Stderr);
}
