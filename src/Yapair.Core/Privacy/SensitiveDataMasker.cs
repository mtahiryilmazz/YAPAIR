using System.Text.RegularExpressions;

namespace Yapair.Core.Privacy;

/// <summary>
/// Redacts identifiers that are not needed for local diagnosis. Regexes are only
/// the last line of defence for free text; structured fields are redacted
/// explicitly by <see cref="SnapshotRedactor"/>.
/// </summary>
public static partial class SensitiveDataMasker
{
    public static string RedactDeviceInstanceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return deviceId;
        }

        var parts = deviceId.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2 ? deviceId : string.Join('\\', parts.Take(2));
    }

    public static string? MaskIpv4(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsIpv4(value))
        {
            return value;
        }

        var parts = value.Split('.');
        return $"{parts[0]}.{parts[1]}.{parts[2]}.*";
    }

    public static TextRedactionResult RedactText(string? value, bool maskNetworkAddresses, bool redactDocumentNames = true)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new TextRedactionResult(value ?? string.Empty, 0, 0, 0);
        }

        var documentNames = 0;
        var networkAddresses = 0;
        var identifiers = 0;
        var text = value;

        text = UserProfilePath().Replace(text, match =>
        {
            identifiers++;
            return $"{match.Groups["prefix"].Value}<redacted>{match.Groups["suffix"].Value}";
        });

        if (redactDocumentNames)
        {
            text = DocumentName().Replace(text, _ =>
            {
                documentNames++;
                return "<redacted-document>";
            });
        }

        if (maskNetworkAddresses)
        {
            text = Ipv4().Replace(text, match =>
            {
                if (!IsIpv4(match.Value))
                {
                    return match.Value;
                }

                networkAddresses++;
                return MaskIpv4(match.Value)!;
            });
        }

        text = MacAddress().Replace(text, _ =>
        {
            identifiers++;
            return "<redacted-mac>";
        });

        text = EmailAddress().Replace(text, _ =>
        {
            identifiers++;
            return "<redacted-email>";
        });

        return new TextRedactionResult(text, documentNames, networkAddresses, identifiers);
    }

    public static bool IsIpv4(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var octet)
                || octet is < 0 or > 255)
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"(?<prefix>(?:[A-Za-z]:\\Users\\))(?<name>[^\\\s""']+)(?<suffix>\\?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserProfilePath();

    [GeneratedRegex(@"(?<!\w)(?<name>[^\s""'()\[\]\\/]{1,120}\.(?:pdf|docx?|xlsx?|pptx?|txt|rtf|odt|ods|odp|xps|oxps|jpe?g|tiff?|png|bmp|gif|csv|eml|msg|zip|7z))(?=$|[\s""'()\[\],;:.!?])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentName();

    [GeneratedRegex(@"(?<![\d.])(?<ip>(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})(?![\d.])", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4();

    [GeneratedRegex(@"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant)]
    private static partial Regex MacAddress();

    [GeneratedRegex(@"(?<![\w.+-])[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}(?![\w.-])", RegexOptions.CultureInvariant)]
    private static partial Regex EmailAddress();
}

public sealed record TextRedactionResult(string Text, int DocumentNames, int NetworkAddresses, int Identifiers)
{
    public int Total => DocumentNames + NetworkAddresses + Identifiers;
}
