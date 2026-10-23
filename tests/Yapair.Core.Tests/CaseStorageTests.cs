using Yapair.Storage;

namespace Yapair.Core.Tests;

public sealed class CaseStorageTests
{
    [Theory]
    [InlineData("Yazıcı yazdırmıyor, çıktı gelmiyor.", "printer_output_failure", "printers")]
    [InlineData("Yazıcı çevrimdışı görünüyor.", "printer_offline", "printers")]
    [InlineData("Yazıcı aygıtlar listesinde görünmüyor.", "printer_not_visible", "printers")]
    [InlineData("Ses gelmiyor, hoparlör çalışmıyor.", "audio_no_sound", "audio")]
    [InlineData("Wi-Fi bağlanmıyor, internet yok.", "network_connectivity", "network")]
    public void NormalizerMapsTurkishVariantsToStableCodes(string text, string expectedCode, string expectedFamily)
    {
        var normalized = SymptomNormalizer.Normalize(text);

        Assert.Equal(expectedCode, normalized.Code);
        Assert.Equal(expectedFamily, normalized.IssueFamily);
        Assert.NotEmpty(normalized.Tokens);
    }

    [Fact]
    public void FivePrinterComplaintsGroupIntoOneFamilyWithSubclusters()
    {
        var texts = new[]
        {
            "Yazıcı yazdırmıyor, iş kuyrukta bekliyor.",
            "Yazıcı çevrimdışı görünüyor.",
            "Yazıcı aygıtlar listesinde görünmüyor.",
            "Çıktı gelmiyor, spooler takıldı.",
            "Yazıcı bağlı değil, kırmızı ışık yanıyor.",
            "Ses gelmiyor.",
        };

        var cases = texts.Select((text, index) => ToCase(text, index)).ToArray();
        var report = CaseClusterer.Cluster(cases);

        var printers = Assert.Single(report.Clusters, cluster => cluster.Family == "printers");
        Assert.Equal(5, printers.Count);
        Assert.Contains(printers.Subclusters, subcluster => subcluster.Code == "printer_output_failure");
        Assert.Contains(printers.Subclusters, subcluster => subcluster.Code == "printer_offline");
        Assert.Contains(printers.Subclusters, subcluster => subcluster.Code == "printer_not_visible");
        Assert.Contains(report.Clusters, cluster => cluster.Family == "audio");
    }

    [Fact]
    public void UnknownComplaintsAreGroupedByTokenSimilarity()
    {
        var cases = new[]
        {
            ToCase("Bilgisayar garip davranıyor.", 1),
            ToCase("Bilgisayar tuhaf davranıyor.", 2),
            ToCase("Tamamen alakasız bir konu.", 3),
        };

        var report = CaseClusterer.Cluster(cases, threshold: 0.3);

        var unknown = report.Clusters.Where(cluster => cluster.Family == "unknown").ToArray();
        Assert.Equal(2, unknown.Length);
        Assert.Contains(unknown, cluster => cluster.Count == 2);
    }

    [Fact]
    public void SqliteStorePersistsCasesAndOutcome()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"yapair-cases-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "cases.db");

        try
        {
            var store = new CaseStore(databasePath);
            store.Initialize();

            var machine = Yapair.Core.Contracts.MachineIdentity.Create("BT-TEST", "HWID-123");
            var record = store.AddCase("Yazıcı çevrimdışı", storeRaw: false, machine: machine);
            Assert.Equal("printer_offline", record.SymptomCode);
            Assert.Null(record.RawText);
            Assert.Equal(machine.Key, record.MachineKey);
            Assert.Equal("BT-TEST", record.MachineLabel);
            Assert.Equal(1, store.Count());

            var listed = Assert.Single(store.GetCases());
            Assert.Equal(record.CaseId, listed.CaseId);
            Assert.Equal(machine.Key, listed.MachineKey);
            Assert.Equal("BT-TEST", listed.MachineLabel);

            Assert.True(store.SetOutcome(record.CaseId, "not_resolved", "not_resolved"));
            var updated = Assert.Single(store.GetAllCases());
            Assert.Equal("not_resolved", updated.Outcome);
            Assert.Equal("not_resolved", updated.UserConfirmation);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void RawTextIsStoredOnlyWhenRequested()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"yapair-raw-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "cases.db");

        try
        {
            var store = new CaseStore(databasePath);
            store.Initialize();

            var withoutRaw = store.AddCase("Yazıcı görünmüyor", storeRaw: false);
            var withRaw = store.AddCase("Ses gelmiyor", storeRaw: true);

            Assert.Null(withoutRaw.RawText);
            Assert.Equal("Ses gelmiyor", withRaw.RawText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static CaseRecord ToCase(string text, int index)
    {
        var normalized = SymptomNormalizer.Normalize(text);
        return new CaseRecord
        {
            CaseId = $"case-{index:D3}",
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(index),
            RawText = text,
            NormalizedText = normalized.NormalizedText,
            SymptomCode = normalized.Code,
            SymptomLabel = normalized.Label,
            IssueFamily = normalized.IssueFamily,
            Domain = normalized.Domain,
        };
    }
}
