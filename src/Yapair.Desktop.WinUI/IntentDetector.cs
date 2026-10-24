namespace Yapair.Desktop.WinUI;

public static class IntentDetector
{
    private static readonly (string[] Keywords, string Domain)[] Map =
    {
        (new[] { "bluetooth", "kablosuz" }, "bluetooth"),
        (new[] { "yazici", "yazdÄ±r", "yazdir", "printer", "spooler" }, "printers"),
        (new[] { "ag ", "aÄŸ", "internet", "wifi", "ethernet", "baglanti", "baÄŸlantÄ±" }, "network"),
        (new[] { "ses", "hoparlor", "hoparlÃ¶r", "mikrofon", "audio" }, "audio"),
        (new[] { "usb", "aygit", "aygÄ±t", "surucu", "sÃ¼rÃ¼cÃ¼", "driver" }, "devices"),
        (new[] { "guncelleme", "gÃ¼ncelleme", "update" }, "windows_update"),
        (new[] { "uac", "yonetici", "yÃ¶netici", "izin", "yetki", "firewall" }, "security")
    };

    public static string? Detect(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var lower = " " + message.ToLowerInvariant() + " ";
        foreach (var (keys, domain) in Map)
            if (keys.Any(k => lower.Contains(k))) return domain;
        return null;
    }
}
