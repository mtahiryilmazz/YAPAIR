using System.Text;
using System.Text.Json;
using Yapair.Core.Contracts;
using Yapair.Core.Playbooks;
using Yapair.Core.Serialization;
using Yapair.Core.Verification;
using Yapair.Discovery.Windows;

namespace Yapair.Cli;

public static class CliApplication
{
    public const string Version = "0.1.0";

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        TextReader? standardInput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var parsed = CliParser.Parse(args);
        if (parsed.Error is not null)
        {
            stderr.WriteLine($"Hata: {parsed.Error}");
            stderr.WriteLine();
            stderr.WriteLine(CliHelp.Text);
            return 64;
        }

        switch (parsed.Command)
        {
            case CliCommand.Help:
                stdout.WriteLine(CliHelp.Text);
                return 0;
            case CliCommand.Version:
                stdout.WriteLine($"yapair {Version}");
                return 0;
            case CliCommand.Schema:
                stdout.WriteLine(SnapshotSchema.SchemaText);
                return 0;
            case CliCommand.Discover:
                return await RunDiscoverAsync(parsed.Options, stdout, stderr, cancellationToken).ConfigureAwait(false);
            case CliCommand.Playbooks:
                return RunPlaybooks(parsed.Options, stdout);
            case CliCommand.Verify:
                return await RunVerifyAsync(
                    parsed.Options,
                    stdout,
                    stderr,
                    standardInput ?? Console.In,
                    cancellationToken).ConfigureAwait(false);
            default:
                return 64;
        }
    }

    private static async Task<int> RunDiscoverAsync(
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (options.IncludeDocumentNames)
        {
            stderr.WriteLine("Uyarı: --include-document-names etkin. Yazdırma işi belge adları rapora dahil edilecek.");
        }

        if (options.ShowNetworkAddresses)
        {
            stderr.WriteLine("Uyarı: --show-network-addresses etkin. IPv4 adresleri maskelenmeyecek.");
        }

        if (options.IppAllowInsecureTls)
        {
            stderr.WriteLine("Uyarı: --ipp-insecure-tls etkin. Yerel yazıcının TLS sertifikası doğrulanmayacak.");
        }

        using var log = DiscoveryLog.TryCreate(options.LogPath, stderr);
        log?.Write("info", "discovery_started");

        if (options.Verbose)
        {
            stderr.WriteLine("YAPAIR salt okunur tanılama başlatıldı...");
        }

        try
        {
            var discovery = new WindowsDiscovery();
            var snapshot = await discovery.CollectAsync(
                new DiscoveryOptions
                {
                    PerSourceTimeout = TimeSpan.FromMilliseconds(options.TimeoutMs),
                    IppTimeout = TimeSpan.FromMilliseconds(Math.Min(options.TimeoutMs, 5_000)),
                    EventLookbackHours = options.LookbackHours,
                    MaxEvents = options.Events,
                    IppPrinterName = options.IppPrinter,
                    IppPath = options.IppPath,
                    IppAllowInsecureTls = options.IppAllowInsecureTls,
                    IncludeDocumentNames = options.IncludeDocumentNames,
                    ShowNetworkAddresses = options.ShowNetworkAddresses,
                },
                cancellationToken).ConfigureAwait(false);

            var validation = SnapshotSchema.Validate(snapshot);
            if (!validation.IsValid)
            {
                stderr.WriteLine($"Hata: rapor şema doğrulamasından geçemedi: {validation.DescribeErrors()}");
                log?.Write("error", "schema_validation_failed");
                return 4;
            }

            foreach (var source in snapshot.Sources)
            {
                log?.Write(
                    source.Status is SourceStatus.Failed ? "error" : "info",
                    "source_collected",
                    source.Source,
                    source.Status,
                    source.DurationMs,
                    source.ItemCount,
                    source.ErrorCode);

                if (options.Verbose)
                {
                    stderr.WriteLine($"  {source.Source,-18} {source.Status,-8} {source.DurationMs,6} ms");
                }
            }

            var outputText = options.Format == "json"
                ? SnapshotJson.Serialize(snapshot, options.Pretty)
                : CliSummary.Build(snapshot);

            if (!outputText.EndsWith('\n'))
            {
                outputText += Environment.NewLine;
            }

            if (options.OutputPath is not null)
            {
                await WriteAtomicAsync(options.OutputPath, outputText, cancellationToken).ConfigureAwait(false);
                stderr.WriteLine($"Rapor yazıldı: {Path.GetFullPath(options.OutputPath)}");
            }
            else
            {
                await stdout.WriteAsync(outputText).ConfigureAwait(false);
            }

            log?.Write("info", "discovery_completed");
            if (options.Verbose)
            {
                stderr.WriteLine($"Tanılama tamamlandı: {snapshot.Status}.");
            }

            return snapshot.Status switch
            {
                SnapshotStatus.Success => 0,
                SnapshotStatus.Partial => 2,
                _ => 3,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            log?.Write("warn", "discovery_cancelled");
            stderr.WriteLine("Tanılama iptal edildi.");
            return 130;
        }
        catch (InvalidOperationException ex)
        {
            log?.Write("error", "internal_error");
            stderr.WriteLine($"İç hata: {ex.Message}");
            return 4;
        }
        catch (Exception)
        {
            log?.Write("error", "unexpected_error");
            stderr.WriteLine("Beklenmeyen bir iç hata oluştu; rapor üretilemedi.");
            return 4;
        }
    }

    private static int RunPlaybooks(CliOptions options, TextWriter stdout)
    {
        if (options.Format == "json")
        {
            stdout.WriteLine(JsonSerializer.Serialize(
                PlaybookCatalog.All,
                options.Pretty ? SnapshotJson.IndentedOptions : SnapshotJson.Options));
            return 0;
        }

        var text = new StringBuilder();
        text.AppendLine("YAPAIR Onarım Playbook Kataloğu (salt okunur)");
        text.AppendLine("============================================");
        text.AppendLine($"{PlaybookCatalog.All.Count} playbook tanımlı. Bu sürüm hiçbir playbook'u çalıştırmaz.");
        text.AppendLine();

        foreach (var playbook in PlaybookCatalog.All)
        {
            var approval = playbook.RequiresApproval ? "onay gerekli" : "onay gerekmez";
            text.AppendLine($"{playbook.Id}  [{playbook.Risk}, {approval}]");
            text.AppendLine($"  {playbook.Title}");
            text.AppendLine($"  {playbook.Summary}");
            text.AppendLine($"  Adımlar: {string.Join(" -> ", playbook.Steps)}");
            text.AppendLine($"  Ön koşullar: {string.Join("; ", playbook.Preconditions)}");
            text.AppendLine($"  Post-check: {string.Join("; ", playbook.PostChecks)}");
            text.AppendLine($"  Geri alma: {playbook.Rollback}");
            text.AppendLine();
        }

        stdout.Write(text.ToString());
        return 0;
    }

    private static async Task<int> RunVerifyAsync(
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        TextReader standardInput,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.BaselinePath))
        {
            stderr.WriteLine("Hata: verify için --baseline <json> zorunludur.");
            return 64;
        }

        SystemSnapshot baseline;
        try
        {
            var json = await File.ReadAllTextAsync(options.BaselinePath, cancellationToken).ConfigureAwait(false);
            baseline = SnapshotJson.Deserialize(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            stderr.WriteLine("Hata: baseline raporu okunamadı veya geçerli bir YAPAIR raporu değil.");
            return 4;
        }

        var validation = SnapshotSchema.Validate(baseline);
        if (!validation.IsValid)
        {
            stderr.WriteLine($"Hata: baseline raporu şema doğrulamasından geçemedi: {validation.DescribeErrors()}");
            return 4;
        }

        // A plain verify covers the whole system. Only select a printer when the
        // user asked for one or the baseline contains an IPP device check.
        var printerName = options.PrinterName ?? baseline.IppChecks.FirstOrDefault()?.PrinterName;

        var baselineIpp = baseline.IppChecks.FirstOrDefault(check =>
            printerName is null || string.Equals(check.PrinterName, printerName, StringComparison.OrdinalIgnoreCase));
        var ippTarget = options.IppPrinter ?? baselineIpp?.PrinterName;

        if (options.Verbose)
        {
            stderr.WriteLine($"Doğrulama başlatıldı. Kapsam: {printerName ?? "tüm sistem"}");
        }

        var discovery = new WindowsDiscovery();
        async Task<SystemSnapshot> Recollect(CancellationToken token) => await discovery.CollectAsync(
            new DiscoveryOptions
            {
                PerSourceTimeout = TimeSpan.FromMilliseconds(options.TimeoutMs),
                IppTimeout = TimeSpan.FromMilliseconds(Math.Min(options.TimeoutMs, 5_000)),
                EventLookbackHours = options.LookbackHours,
                MaxEvents = options.Events,
                IppPrinterName = ippTarget,
                IppPath = options.IppPath,
                IppAllowInsecureTls = options.IppAllowInsecureTls,
                IncludeDocumentNames = options.IncludeDocumentNames,
                ShowNetworkAddresses = options.ShowNetworkAddresses,
            },
            token).ConfigureAwait(false);

        IVerificationPrompt prompt = options.Ask
            ? new ConsoleVerificationPrompt(standardInput, stderr)
            : new NoopVerificationPrompt();

        try
        {
            var result = await new VerificationLoop().RunAsync(
                baseline,
                Recollect,
                prompt,
                new VerificationOptions
                {
                    PrinterName = printerName,
                    MaxAttempts = options.MaxAttempts,
                },
                cancellationToken).ConfigureAwait(false);

            var verificationValidation = VerificationSchema.Validate(result);
            if (!verificationValidation.IsValid)
            {
                stderr.WriteLine($"Hata: doğrulama sonucu şema doğrulamasından geçemedi: {verificationValidation.DescribeErrors()}");
                return 4;
            }

            var outputText = options.Format == "json"
                ? VerificationJson.Serialize(result, options.Pretty)
                : CliSummary.BuildVerification(result);

            if (!outputText.EndsWith('\n'))
            {
                outputText += Environment.NewLine;
            }

            if (options.OutputPath is not null)
            {
                await WriteAtomicAsync(options.OutputPath, outputText, cancellationToken).ConfigureAwait(false);
                stderr.WriteLine($"Doğrulama raporu yazıldı: {Path.GetFullPath(options.OutputPath)}");
            }
            else
            {
                await stdout.WriteAsync(outputText).ConfigureAwait(false);
            }

            return result.Outcome switch
            {
                VerificationOutcome.Resolved => 0,
                VerificationOutcome.Unknown => 2,
                _ => 3,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stderr.WriteLine("Doğrulama iptal edildi.");
            return 130;
        }
        catch (Exception)
        {
            stderr.WriteLine("Beklenmeyen bir iç hata oluştu; doğrulama tamamlanamadı.");
            return 4;
        }
    }

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".yapair-{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
