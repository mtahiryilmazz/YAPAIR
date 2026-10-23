using System.Globalization;

namespace Yapair.Cli;

internal static class CliParser
{
    public static CliParseResult Parse(string[] args)
    {
        var options = new CliOptions();
        var command = CliCommand.Discover;
        var commandSeen = false;
        var help = false;
        var version = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument.Length == 0 || argument[0] != '-')
            {
                if (!commandSeen)
                {
                    commandSeen = true;
                    switch (argument.ToLowerInvariant())
                    {
                        case "discover":
                            command = CliCommand.Discover;
                            break;
                        case "verify":
                            command = CliCommand.Verify;
                            break;
                        case "playbooks":
                            command = CliCommand.Playbooks;
                            break;
                        case "schema":
                            command = CliCommand.Schema;
                            break;
                        case "version":
                            version = true;
                            break;
                        case "help":
                            help = true;
                            break;
                        default:
                            return Failure($"Bilinmeyen komut: {argument}");
                    }
                }
                else
                {
                    return Failure($"Beklenmeyen argüman: {argument}");
                }

                continue;
            }

            var (name, inlineValue) = SplitOption(argument);
            switch (name)
            {
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "--version":
                    version = true;
                    break;
                case "--pretty":
                    if (inlineValue is not null)
                    {
                        return Failure("--pretty değer almaz.");
                    }

                    options.Pretty = true;
                    break;
                case "--format":
                    if (!TryTakeValue(args, ref index, inlineValue, name, out var format, out var formatError))
                    {
                        return Failure(formatError!);
                    }

                    if (format is not ("json" or "summary" or "text"))
                    {
                        return Failure("--format değeri json veya summary olmalıdır.");
                    }

                    options.Format = format == "text" ? "summary" : format;
                    break;
                case "--output":
                    if (!TryTakeValue(args, ref index, inlineValue, name, out var output, out var outputError))
                    {
                        return Failure(outputError!);
                    }

                    options.OutputPath = output;
                    break;
                case "--timeout-ms":
                    if (!TryTakeInt(args, ref index, inlineValue, name, out var timeout, out var timeoutError))
                    {
                        return Failure(timeoutError!);
                    }

                    if (timeout is < 1_000 or > 120_000)
                    {
                        return Failure("--timeout-ms 1000 ile 120000 arasında olmalıdır.");
                    }

                    options.TimeoutMs = timeout;
                    break;
                case "--events":
                    if (!TryTakeInt(args, ref index, inlineValue, name, out var events, out var eventsError))
                    {
                        return Failure(eventsError!);
                    }

                    if (events is < 0 or > 500)
                    {
                        return Failure("--events 0 ile 500 arasında olmalıdır.");
                    }

                    options.Events = events;
                    break;
                case "--lookback-hours":
                    if (!TryTakeInt(args, ref index, inlineValue, name, out var lookback, out var lookbackError))
                    {
                        return Failure(lookbackError!);
                    }

                    if (lookback is < 1 or > 168)
                    {
                        return Failure("--lookback-hours 1 ile 168 arasında olmalıdır.");
                    }

                    options.LookbackHours = lookback;
                    break;
                case "--ipp":
                    if (!TryTakeValue(args, ref index, inlineValue, name, out var ipp, out var ippError))
                    {
                        return Failure(ippError!);
                    }

                    options.IppPrinter = ipp;
                    break;
                case "--ipp-path":
                    if (!TryTakeValue(args, ref index, inlineValue, name, out var ippPath, out var ippPathError))
                    {
                        return Failure(ippPathError!);
                    }

                    options.IppPath = ippPath.StartsWith('/') ? ippPath : $"/{ippPath}";
                    break;
                case "--ipp-insecure-tls":
                    if (inlineValue is not null)
                    {
                        return Failure("--ipp-insecure-tls değer almaz.");
                    }

                    options.IppAllowInsecureTls = true;
                    break;
                case "--show-network-addresses":
                    if (inlineValue is not null)
                    {
                        return Failure("--show-network-addresses değer almaz.");
                    }

                    options.ShowNetworkAddresses = true;
                    break;
                case "--include-document-names":
                    if (inlineValue is not null)
                    {
                        return Failure("--include-document-names değer almaz.");
                    }

                    options.IncludeDocumentNames = true;
                    break;
                case "--log":
                    if (!TryTakeValue(args, ref index, inlineValue, name, out var log, out var logError))
                    {
                        return Failure(logError!);
                    }

                    options.LogPath = log;
                    break;
                case "--verbose":
                    if (inlineValue is not null)
                    {
                        return Failure("--verbose değer almaz.");
                    }

                    options.Verbose = true;
                    break;
                case "--baseline":
                    if (!TryTakeValue(args, ref index, inlineValue, name, out var baseline, out var baselineError))
                    {
                        return Failure(baselineError!);
                    }

                    options.BaselinePath = baseline;
                    break;
                case "--printer":
                    if (!TryTakeValue(args, ref index, inlineValue, name, out var printer, out var printerError))
                    {
                        return Failure(printerError!);
                    }

                    options.PrinterName = printer;
                    break;
                case "--ask":
                    if (inlineValue is not null)
                    {
                        return Failure("--ask değer almaz.");
                    }

                    options.Ask = true;
                    break;
                case "--max-attempts":
                    if (!TryTakeInt(args, ref index, inlineValue, name, out var attempts, out var attemptsError))
                    {
                        return Failure(attemptsError!);
                    }

                    if (attempts is < 1 or > 5)
                    {
                        return Failure("--max-attempts 1 ile 5 arasında olmalıdır.");
                    }

                    options.MaxAttempts = attempts;
                    break;
                default:
                    return Failure($"Bilinmeyen seçenek: {name}");
            }
        }

        if (help)
        {
            return new CliParseResult(CliCommand.Help, options, null);
        }

        if (version)
        {
            return new CliParseResult(CliCommand.Version, options, null);
        }

        return new CliParseResult(command, options, null);
    }

    private static (string Name, string? Value) SplitOption(string argument)
    {
        var equals = argument.IndexOf('=', StringComparison.Ordinal);
        return equals < 0
            ? (argument, null)
            : (argument[..equals], argument[(equals + 1)..]);
    }

    private static bool TryTakeValue(
        string[] args,
        ref int index,
        string? inlineValue,
        string option,
        out string value,
        out string? error)
    {
        if (inlineValue is not null)
        {
            value = inlineValue;
            error = null;
            return true;
        }

        if (index + 1 >= args.Length || (args[index + 1].Length > 0 && args[index + 1][0] == '-'))
        {
            value = string.Empty;
            error = $"{option} için bir değer gerekli.";
            return false;
        }

        value = args[++index];
        error = null;
        return true;
    }

    private static bool TryTakeInt(
        string[] args,
        ref int index,
        string? inlineValue,
        string option,
        out int value,
        out string? error)
    {
        if (!TryTakeValue(args, ref index, inlineValue, option, out var text, out error))
        {
            value = 0;
            return false;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"{option} sayısal bir değer almalıdır.";
            return false;
        }

        return true;
    }

    private static CliParseResult Failure(string error) => new(CliCommand.Help, new CliOptions(), error);
}
