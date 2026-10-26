namespace Yapair.Desktop.WinUI;

public static class IntentDetector
{
    private static readonly (string[] Keywords, string Domain)[] Map =
    {
        (new[] { "bluetooth", "kablosuz", "bt" }, "bluetooth"),
        (new[] { "yazici", "yazıcı", "yazdir", "yazdır", "printer", "spooler", "kuyruk" }, "printers"),
        (new[] { "ag ", "ağ", "internet", "wifi", "wi-fi", "ethernet", "baglanti", "bağlantı", "ip", "dns" }, "network"),
        (new[] { "ses", "hoparlor", "hoparlör", "mikrofon", "audio", "sound", "kulaklik", "kulaklık" }, "audio"),
        (new[] { "usb", "aygit", "aygıt", "surucu", "sürücü", "driver", "pnp", "cihaz" }, "devices"),
        (new[] { "guncelleme", "güncelleme", "update", "reboot", "yeniden baslat" }, "windows_update"),
        (new[] { "uac", "yonetici", "yönetici", "izin", "yetki", "firewall", "guvenlik", "güvenlik" }, "security")
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
