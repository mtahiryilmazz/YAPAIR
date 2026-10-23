using System.Globalization;
using System.Text;
using Yapair.Core.Contracts;
using Yapair.Core.Verification;

namespace Yapair.Cli;

internal static class CliSummary
{
    public static string Build(SystemSnapshot snapshot)
    {
        var text = new StringBuilder();
        text.AppendLine("YAPAIR Tanılama Raporu");
        text.AppendLine("======================");
        text.AppendLine($"Rapor kimliği : {snapshot.SnapshotId}");
        text.AppendLine($"Oluşturma     : {snapshot.CollectedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)}");
        text.AppendLine($"Toplama durumu: {StatusText(snapshot.Status)}");
        text.AppendLine();

        text.AppendLine("İşletim sistemi");
        text.AppendLine($"  Ürün      : {ValueOrUnknown(snapshot.Os.ProductName)}");
        text.AppendLine($"  Sürüm     : {ValueOrUnknown(snapshot.Os.DisplayVersion)} ({ValueOrUnknown(snapshot.Os.Build)})");
        text.AppendLine($"  Mimari    : {ValueOrUnknown(snapshot.Os.Architecture)}");
        text.AppendLine($"  Yönetici  : {YesNo(snapshot.Os.IsElevated)} | Etki alanı: {YesNo(snapshot.Os.IsDomainJoined)}");
        text.AppendLine();

        AppendFindings(snapshot, text);

        text.AppendLine();
        text.AppendLine("Print Spooler");
        if (snapshot.Spooler is null)
        {
            text.AppendLine("  Durum okunamadı.");
        }
        else
        {
            text.AppendLine($"  {snapshot.Spooler.DisplayName}: {snapshot.Spooler.Status} / {snapshot.Spooler.StartType}"
                + (snapshot.Spooler.ProcessId is { } pid ? $" (PID {pid})" : string.Empty));
        }

        text.AppendLine();
        text.AppendLine($"Yazıcılar ({snapshot.Printers.Count})");
        if (snapshot.Printers.Count == 0)
        {
            text.AppendLine("  Kayıtlı yazıcı bulunamadı veya yazıcı kaynağı okunamadı.");
        }
        else
        {
            foreach (var printer in snapshot.Printers)
            {
                var details = new List<string>
                {
                    printer.DriverName,
                    printer.PortName,
                    printer.Status,
                };

                if (printer.ErrorState is not "no_error" and not "unknown")
                {
                    details.Add($"hata: {printer.ErrorState}");
                }

                if (printer.WorkOffline)
                {
                    details.Add("çevrimdışı");
                }

                if (printer.IsDefault)
                {
                    details.Add("varsayılan");
                }

                if (printer.QueueObserved)
                {
                    details.Add($"kuyruk: {printer.QueuedJobCount}");
                }

                text.AppendLine($"  - {printer.Name} | {string.Join(" | ", details)}");
            }
        }

        var queueObserved = snapshot.Printers.Any(static printer => printer.QueueObserved);
        text.AppendLine(queueObserved
            ? $"Kuyruk: {snapshot.PrintJobs.Count} iş"
            : "Kuyruk: gözlenemedi (yazdırma işleri kaynağı okunamadı)");

        text.AppendLine($"Bağlantı noktaları: {snapshot.Ports.Count} | Sürücüler: {snapshot.Drivers.Count} | PnP aygıtları: {snapshot.PnpDevices.Count}");
        text.AppendLine($"Olay kayıtları: {snapshot.EventLogs.Records.Count} (son {snapshot.EventLogs.LookbackHours} saat)");
        text.AppendLine();

        if (snapshot.IppChecks.Count > 0)
        {
            text.AppendLine("IPP durumu");
            foreach (var check in snapshot.IppChecks)
            {
                text.AppendLine($"  {check.PrinterName}: {check.Status} | durum: {check.PrinterStateText ?? "bilinmiyor"}"
                    + (check.PrinterStateReasons.Count > 0 ? $" ({string.Join(", ", check.PrinterStateReasons)})" : string.Empty));
                if (check.MakeAndModel is { Length: > 0 })
                {
                    text.AppendLine($"    Model: {check.MakeAndModel}");
                }

                if (check.Message is { Length: > 0 })
                {
                    text.AppendLine($"    Not: {check.Message}");
                }
            }

            text.AppendLine();
        }

        text.AppendLine("Kaynak durumu");
        foreach (var source in snapshot.Sources)
        {
            var detail = $"  {source.Source,-18} {source.Status,-8} {source.ItemCount,3} öğe {source.DurationMs,6} ms";
            if (source.ErrorCode is { Length: > 0 })
            {
                detail += $" | {source.ErrorCode}";
            }

            text.AppendLine(detail);
        }

        text.AppendLine();
        text.AppendLine("Gizlilik");
        text.AppendLine($"  Belge adları: {snapshot.Privacy.DocumentNames} | IPv4: {snapshot.Privacy.NetworkAddresses}"
            + $" | Kullanıcı: {snapshot.Privacy.UserIdentifiers} | Temizlenen alan: {snapshot.Privacy.RedactedFields}");
        foreach (var note in snapshot.Privacy.Notes)
        {
            text.AppendLine($"  - {note}");
        }

        if (snapshot.Limitations.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Sınırlamalar");
            foreach (var limitation in snapshot.Limitations)
            {
                text.AppendLine($"  - {limitation}");
            }
        }

        return text.ToString();
    }

    private static void AppendFindings(SystemSnapshot snapshot, StringBuilder text)
    {
        text.AppendLine($"Bulgular ({snapshot.Findings.Count})");
        if (snapshot.Findings.Count == 0)
        {
            text.AppendLine("  Toplanan kanıtlara göre sorun sinyali bulunmadı.");
        }
        else
        {
            foreach (var finding in snapshot.Findings.Take(30))
            {
                text.AppendLine($"  [{SeverityText(finding.Severity)}] {DomainText(finding.Domain)} / {finding.Subject}");
                text.AppendLine($"    {finding.Summary}");
                if (finding.PlaybookId is { Length: > 0 })
                {
                    text.AppendLine($"    Önerilen playbook: {finding.PlaybookId} (onay olmadan çalıştırılmaz)");
                }
            }

            if (snapshot.Findings.Count > 30)
            {
                text.AppendLine($"  ... {snapshot.Findings.Count - 30} bulgu daha var; tam liste JSON raporunda.");
            }
        }

        if (snapshot.DomainsChecked.Count > 0)
        {
            text.AppendLine("  Taranan alanlar: " + string.Join(", ", snapshot.DomainsChecked.Select(DomainText)));
        }
    }

    private static string SeverityText(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Critical => "KRİTİK",
        FindingSeverity.Error => "HATA",
        FindingSeverity.Warning => "UYARI",
        _ => "BİLGİ",
    };

    private static string DomainText(HealthDomain domain) => domain switch
    {
        HealthDomain.System => "Sistem",
        HealthDomain.Services => "Hizmetler",
        HealthDomain.Devices => "Aygıtlar",
        HealthDomain.Printers => "Yazıcılar",
        HealthDomain.Audio => "Ses",
        HealthDomain.Network => "Ağ",
        HealthDomain.Security => "Güvenlik",
        HealthDomain.Storage => "Depolama",
        HealthDomain.WindowsUpdate => "Windows Update",
        _ => domain.ToString(),
    };

    public static string BuildVerification(VerificationResult result)
    {
        var text = new StringBuilder();
        text.AppendLine("YAPAIR Doğrulama Raporu");
        text.AppendLine("=======================");
        text.AppendLine($"Doğrulama kimliği : {result.VerificationId}");
        text.AppendLine($"Zaman             : {result.VerifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)}");
        text.AppendLine($"Kapsam            : {result.PrinterName ?? "tüm sistem"}");
        text.AppendLine($"Sonuç            : {OutcomeText(result.Outcome)}");
        text.AppendLine($"Kullanıcı onayı  : {UserConfirmationText(result.UserConfirmation)}");
        text.AppendLine($"Deneme           : {result.Attempts}");
        text.AppendLine($"Özet             : {VerificationSummaryText(result)}");
        text.AppendLine();

        WriteSignals("Temel problem sinyalleri", result.BaselineSignals);
        WriteSignals("Süren sinyaller", result.PersistingSignals);
        WriteSignals("Yeni sinyaller", result.NewSignals);
        WriteSignals("Çözülen sinyaller", result.ResolvedSignals);
        WriteSignals("Doğrulanamayan sinyaller", result.UnverifiableSignals);

        if (result.Outcome is VerificationOutcome.NotResolved or VerificationOutcome.Contradicted)
        {
            text.AppendLine("Sorun sürüyor veya çelişkili bir sonuç var. Neden farklı olabilir?");
            text.AppendLine("  - Sorun farklı bir katmanda olabilir (sürücü, servis, ağ, güvenlik politikası, depolama).");
            text.AppendLine("  - Fiziksel müdahale veya yeniden başlatma henüz tamamlanmamış olabilir.");
            text.AppendLine("  - Yeniden tanılama için: yapair discover --pretty --output yeni.json");
        }

        if (result.Details.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Ayrıntılar");
            foreach (var detail in result.Details)
            {
                text.AppendLine($"  - {detail}");
            }
        }

        return text.ToString();

        void WriteSignals(string title, IReadOnlyList<ProblemSignal> signals)
        {
            if (signals.Count == 0)
            {
                return;
            }

            text.AppendLine($"{title} ({signals.Count})");
            foreach (var signal in signals)
            {
                text.AppendLine($"  - {signal.Source}/{signal.Scope}: {signal.Code} ({signal.Detail})");
            }

            text.AppendLine();
        }
    }

    private static string VerificationSummaryText(VerificationResult result) => result.Outcome switch
    {
        VerificationOutcome.Resolved => "Temel sorun sinyalleri güncel raporda görünmüyor.",
        VerificationOutcome.NotResolved =>
            $"{result.PersistingSignals.Count} temel sinyal sürüyor, {result.NewSignals.Count} yeni sinyal gözlendi."
            + " Kök neden farklı olabilir; yeniden tanılama önerilir.",
        VerificationOutcome.Contradicted =>
            "Kullanıcı sorunun çözüldüğünü bildirdi ancak otomatik kontroller hâlâ sorun sinyali görüyor."
            + " Sonuç doğrulanmış başarı olarak işaretlenmedi.",
        _ => "Güncel raporda doğrulama için yeterli kanıt yok; sonuç bilinmiyor.",
    };

    public static string OutcomeText(VerificationOutcome outcome) => outcome switch
    {
        VerificationOutcome.Resolved => "çözüldü",
        VerificationOutcome.NotResolved => "çözülmedi",
        VerificationOutcome.Contradicted => "çelişkili (kullanıcı çözüldü dedi, otomatik kontrol sürüyor)",
        _ => "bilinmiyor / doğrulanamadı",
    };

    public static string UserConfirmationText(UserConfirmation confirmation) => confirmation switch
    {
        UserConfirmation.Resolved => "evet, çözüldü",
        UserConfirmation.NotResolved => "hayır, devam ediyor",
        UserConfirmation.Unsure => "emin değil",
        _ => "sorulmadı",
    };

    private static string StatusText(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Success => "başarılı",
        SnapshotStatus.Partial => "kısmi",
        _ => "başarısız",
    };

    private static string ValueOrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "bilinmiyor" : value;

    private static string YesNo(bool value) => value ? "evet" : "hayır";
}
