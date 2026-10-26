using Yapair.Api;

namespace Yapair.Core.Tests;

public sealed class LocalDatabaseTests : IDisposable
{
    private readonly string _tempFile;

    public LocalDatabaseTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"yapair_test_db_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
    }

    [Fact]
    public void LocalDatabase_EnsureSeedData_CreatesRealisticData()
    {
        var db = new LocalDatabase(_tempFile);
        db.EnsureSeedData(force: true);

        Assert.NotEmpty(db.Cases);
        Assert.True(db.Cases.Count >= 5);
        Assert.NotEmpty(db.Machines);
        Assert.True(db.Machines.Count >= 3);
        Assert.NotEmpty(db.Batches);
        Assert.NotEmpty(db.HelpRequests);
        Assert.NotEmpty(db.Audits);
        Assert.NotEmpty(db.RemoteRepairs);

        Assert.Contains(db.Cases, c => c.GetValueOrDefault("playbook_id")?.ToString() == "printers.spooler.restart.v1");
        Assert.Contains(db.Machines, m => m.GetValueOrDefault("machine_key")?.ToString() == "MK-IST-DESK-01");
    }

    [Fact]
    public async Task SupabaseClient_RemoteRepair_Dispatch_And_Update()
    {
        var db = new LocalDatabase(_tempFile);
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:5080/") };
        http.DefaultRequestHeaders.Add("x-local-mode", "1");
        var client = new SupabaseClient(http, db);

        // Dispatch
        var repairId = await client.DispatchRemoteRepairAsync("MK-TEST-01", "printers.spooler.restart.v1", "AdminTest", "test_param");
        Assert.NotNull(repairId);
        Assert.StartsWith("REP-", repairId);

        // Get Pending
        var pending = await client.GetPendingRemoteRepairsAsync("MK-TEST-01");
        Assert.Single(pending);
        Assert.Equal("printers.spooler.restart.v1", pending[0]["playbook_id"]?.ToString());

        // Update Result
        var ok = await client.UpdateRemoteRepairResultAsync(repairId, "completed", "Servis başarıyla yeniden başlatıldı");
        Assert.True(ok);

        // Verify no longer pending
        var pendingAfter = await client.GetPendingRemoteRepairsAsync("MK-TEST-01");
        Assert.Empty(pendingAfter);

        // Verify in history
        var all = await client.GetRemoteRepairsAsync("MK-TEST-01");
        Assert.Single(all);
        Assert.Equal("completed", all[0]["status"]?.ToString());
        Assert.Equal("Servis başarıyla yeniden başlatıldı", all[0]["output"]?.ToString());
    }
}
