using Yapair.Core.Contracts;
using Yapair.Core.Privacy;
using Yapair.TestSupport;

namespace Yapair.Core.Tests;

public sealed class RedactionTests
{
    [Fact]
    public void DefaultRedactionRemovesDocumentNamesAddressesAndIdentifiers()
    {
        var snapshot = SnapshotFixture.CreateHealthy() with
        {
            PrintJobs =
            [
                new PrintJobInfo
                {
                    PrinterName = "Test Printer",
                    JobId = 7,
                    Document = "Fatura_Ekim.pdf",
                    Status = "Error",
                    SubmittedAt = SnapshotFixture.FixedTime,
                },
            ],
            EventLogs = new EventLogSection
            {
                LookbackHours = 24,
                Records =
                [
                    new EventRecordInfo
                    {
                        LogName = "System",
                        EventId = 372,
                        LevelCode = 2,
                        Level = "Error",
                        Provider = "Test",
                        TimeCreated = SnapshotFixture.FixedTime,
                        Message = @"C:\Users\ayse\Desktop\Fatura_Ekim.pdf yazdırılamadı. IP 192.168.10.20, MAC 00:1E:0B:8A:9C:11, e-posta ayse@example.com",
                    },
                ],
            },
            PnpDevices =
            [
                new PnpDeviceInfo
                {
                    FriendlyName = "Test Device",
                    Class = "USB",
                    DeviceId = @"USB\VID_1234&PID_5678\5&1A2B3C4D&0&2",
                    HardwareIds = [@"USB\VID_1234&PID_5678"],
                    ConfigManagerErrorCode = 28,
                    ErrorMeaning = "failed_install",
                },
            ],
            Findings =
            [
                new Finding
                {
                    Source = SourceNames.Printers,
                    Code = "printer_offline",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Error,
                    Subject = "Test Printer",
                    Summary = "Sunucu 192.168.10.20 yanıt vermiyor; belge C:\\Users\\ayse\\Fatura.pdf.",
                    Evidence = ["host=192.168.10.20"],
                },
            ],
        };

        var redacted = new SnapshotRedactor(new RedactionOptions()).Redact(snapshot);

        Assert.Equal("<redacted-document>", redacted.PrintJobs[0].Document);
        Assert.Equal("192.168.10.*", redacted.Ports[0].Host);
        Assert.DoesNotContain("Fatura_Ekim", redacted.EventLogs.Records[0].Message);
        Assert.DoesNotContain("192.168.10.20", redacted.EventLogs.Records[0].Message);
        Assert.DoesNotContain("00:1E:0B", redacted.EventLogs.Records[0].Message);
        Assert.DoesNotContain("ayse@example.com", redacted.EventLogs.Records[0].Message);
        Assert.Equal(@"USB\VID_1234&PID_5678", redacted.PnpDevices[0].DeviceId);
        Assert.DoesNotContain("192.168.10.20", redacted.Findings[0].Summary);
        Assert.DoesNotContain("C:\\Users\\ayse", redacted.Findings[0].Summary);
        Assert.Equal("redacted", redacted.Privacy.DocumentNames);
        Assert.Equal("masked", redacted.Privacy.NetworkAddresses);
        Assert.True(redacted.Privacy.RedactedFields > 0);
    }

    [Fact]
    public void ExplicitOptInKeepsDocumentNamesAndAddresses()
    {
        var snapshot = SnapshotFixture.CreateHealthy() with
        {
            PrintJobs =
            [
                new PrintJobInfo
                {
                    PrinterName = "Test Printer",
                    JobId = 7,
                    Document = "Fatura_Ekim.pdf",
                    SubmittedAt = SnapshotFixture.FixedTime,
                },
            ],
        };

        var redacted = new SnapshotRedactor(new RedactionOptions
        {
            IncludeDocumentNames = true,
            ShowNetworkAddresses = true,
        }).Redact(snapshot);

        Assert.Equal("Fatura_Ekim.pdf", redacted.PrintJobs[0].Document);
        Assert.Equal("192.168.10.20", redacted.Ports[0].Host);
        Assert.Equal("included", redacted.Privacy.DocumentNames);
        Assert.Equal("included", redacted.Privacy.NetworkAddresses);
    }

    [Theory]
    [InlineData("Windows 10.0.19045.6937", "Windows 10.0.19045.6937")]
    [InlineData("Sunucu 192.168.1.25 yanıt vermiyor", "Sunucu 192.168.1.* yanıt vermiyor")]
    [InlineData("Sürüm 1.2.3.4", "Sürüm 1.2.3.*")]
    public void Ipv4MaskingIsAppliedOnlyToValidAddresses(string input, string expected)
    {
        var result = SensitiveDataMasker.RedactText(input, maskNetworkAddresses: true);

        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void DeviceInstanceSuffixIsRemoved()
    {
        Assert.Equal(
            @"USB\VID_1234&PID_5678",
            SensitiveDataMasker.RedactDeviceInstanceId(@"USB\VID_1234&PID_5678\5&1A2B3C4D&0&2"));
        Assert.Equal(@"USBPRINT\CANON", SensitiveDataMasker.RedactDeviceInstanceId(@"USBPRINT\CANON"));
    }
}
