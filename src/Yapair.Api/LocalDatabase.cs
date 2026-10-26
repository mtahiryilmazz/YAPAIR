using System.Text.Json;

namespace Yapair.Api;

public sealed class LocalDatabase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly object _lock = new();

    public List<Dictionary<string, object?>> Cases { get; set; } = new();
    public List<Dictionary<string, object?>> Machines { get; set; } = new();
    public List<Dictionary<string, object?>> Batches { get; set; } = new();
    public List<Dictionary<string, object?>> HelpRequests { get; set; } = new();
    public List<Dictionary<string, object?>> Audits { get; set; } = new();

    public List<Dictionary<string, object?>> RemoteRepairs { get; set; } = new();

    public LocalDatabase(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "local-db.json");

        Load();
        if (Cases.Count == 0 && Machines.Count == 0)
        {
            EnsureSeedData(false);
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var txt = JsonSerializer.Serialize(this, JsonOpts);
                File.WriteAllText(_filePath, txt);
            }
            catch { }
        }
    }

    public void EnsureSeedData(bool force = false)
    {
        lock (_lock)
        {
            if (!force && (Cases.Count > 0 || Machines.Count > 0)) return;

            var now = DateTimeOffset.UtcNow;

            Machines = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["machine_key"] = "MK-IST-DESK-01",
                    ["machine_label"] = "Muhasebe-PC01",
                    ["first_seen"] = now.AddDays(-14).ToString("o"),
                    ["last_seen"] = now.AddMinutes(-3).ToString("o"),
                    ["notes"] = "Finans departmanı ana fatura bilgisayarı",
                    ["owner"] = "Ahmet Yılmaz"
                },
                new()
                {
                    ["machine_key"] = "MK-IST-NOTE-02",
                    ["machine_label"] = "Saha-Laptop02",
                    ["first_seen"] = now.AddDays(-7).ToString("o"),
                    ["last_seen"] = now.AddMinutes(-12).ToString("o"),
                    ["notes"] = "Saha satış personeli laptopu",
                    ["owner"] = "Elif Demir"
                },
                new()
                {
                    ["machine_key"] = "MK-ANK-SRV-01",
                    ["machine_label"] = "Yazici-Host-01",
                    ["first_seen"] = now.AddDays(-30).ToString("o"),
                    ["last_seen"] = now.AddMinutes(-1).ToString("o"),
                    ["notes"] = "Ortak ofis yazıcı ve tarayıcı sunucusu",
                    ["owner"] = "Bilgi İşlem"
                }
            };

            Cases = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["case_id"] = "CASE-2026-001",
                    ["machine_key"] = "MK-IST-DESK-01",
                    ["machine_label"] = "Muhasebe-PC01",
                    ["normalized_text"] = "yazıcı kuyruğunda belgeler takıldı yazdıramıyorum",
                    ["raw_text"] = "Yazıcı kuyruğunda belgeler takıldı, hiçbir şey çıkmıyor acil!",
                    ["symptom_code"] = "PRINTER_SPOOLER_STUCK",
                    ["symptom_label"] = "Yazıcı Biriktiricisi / Kuyruk Hatası",
                    ["issue_family"] = "printers",
                    ["domain"] = "hardware",
                    ["outcome"] = "resolved",
                    ["user_confirmation"] = "confirmed",
                    ["playbook_id"] = "printers.spooler.restart.v1",
                    ["received_at"] = now.AddHours(-2).ToString("o"),
                    ["archived_at"] = null
                },
                new()
                {
                    ["case_id"] = "CASE-2026-002",
                    ["machine_key"] = "MK-IST-NOTE-02",
                    ["machine_label"] = "Saha-Laptop02",
                    ["normalized_text"] = "wifi bağlı fakat internet yok dns hatası veriyor",
                    ["raw_text"] = "Wi-Fi bağlı görünüyor ama web siteleri açılmıyor, DNS hatası",
                    ["symptom_code"] = "NETWORK_DNS_FAILURE",
                    ["symptom_label"] = "Ağ DNS ve Bağlantı Sorunu",
                    ["issue_family"] = "network",
                    ["domain"] = "connectivity",
                    ["outcome"] = "resolved",
                    ["user_confirmation"] = "confirmed",
                    ["playbook_id"] = "network.dns.flush.v1",
                    ["received_at"] = now.AddHours(-5).ToString("o"),
                    ["archived_at"] = null
                },
                new()
                {
                    ["case_id"] = "CASE-2026-003",
                    ["machine_key"] = "MK-ANK-SRV-01",
                    ["machine_label"] = "Yazici-Host-01",
                    ["normalized_text"] = "ses kartı cızırtı yapıyor ve kulaklıktan ses gelmiyor",
                    ["raw_text"] = "Toplantı sırasında ses tamamen gitti, hoparlörden cızırtı geliyor",
                    ["symptom_code"] = "AUDIO_SERVICE_STOPPED",
                    ["symptom_label"] = "Windows Ses Hizmeti Durdu",
                    ["issue_family"] = "audio",
                    ["domain"] = "system",
                    ["outcome"] = "resolved",
                    ["user_confirmation"] = "confirmed",
                    ["playbook_id"] = "audio.restart.v1",
                    ["received_at"] = now.AddDays(-1).ToString("o"),
                    ["archived_at"] = null
                },
                new()
                {
                    ["case_id"] = "CASE-2026-004",
                    ["machine_key"] = "MK-IST-DESK-01",
                    ["machine_label"] = "Muhasebe-PC01",
                    ["normalized_text"] = "c sürücüsü tamamen doldu geçici dosyalar yer kaplıyor",
                    ["raw_text"] = "C sürücüsünde 0 bayt kaldı, programlar açılırken çöküyor",
                    ["symptom_code"] = "DISK_SPACE_LOW",
                    ["symptom_label"] = "Düşük Disk Alanı / Temp Dolu",
                    ["issue_family"] = "storage",
                    ["domain"] = "filesystem",
                    ["outcome"] = "resolved",
                    ["user_confirmation"] = "confirmed",
                    ["playbook_id"] = "storage.temp.cleanup.v1",
                    ["received_at"] = now.AddDays(-2).ToString("o"),
                    ["archived_at"] = null
                },
                new()
                {
                    ["case_id"] = "CASE-2026-005",
                    ["machine_key"] = "MK-IST-NOTE-02",
                    ["machine_label"] = "Saha-Laptop02",
                    ["normalized_text"] = "bluetooth fare ve klavye eşleşmiyor bağlanamadı",
                    ["raw_text"] = "Bluetooth açılmıyor, simgesi kayboldu",
                    ["symptom_code"] = "BLUETOOTH_SERVICE_HANG",
                    ["symptom_label"] = "Bluetooth Hizmet Kilitlenmesi",
                    ["issue_family"] = "bluetooth",
                    ["domain"] = "peripheral",
                    ["outcome"] = "pending",
                    ["user_confirmation"] = "unconfirmed",
                    ["playbook_id"] = "bluetooth.service.restart.v1",
                    ["received_at"] = now.AddDays(-3).ToString("o"),
                    ["archived_at"] = null
                }
            };

            Batches = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["batch_id"] = "BATCH-SEED-01",
                    ["case_count"] = 5,
                    ["api_version"] = "v1",
                    ["received_at"] = now.AddHours(-1).ToString("o")
                }
            };

            HelpRequests = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["id"] = 101,
                    ["machine_key"] = "MK-IST-DESK-01",
                    ["machine_label"] = "Muhasebe-PC01",
                    ["case_id"] = "CASE-2026-001",
                    ["problem_text"] = "Fatura yazıcısı kuyrukta bekletiyor, lütfen uzaktan yazıcı servisini yeniden başlatın.",
                    ["status"] = "pending",
                    ["created_at"] = now.AddHours(-2).ToString("o"),
                    ["admin_note"] = "Uzaktan müdahale sırasına alındı",
                    ["assigned_to"] = "Teknisyen Ali"
                }
            };

            Audits = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["id"] = 1,
                    ["machine_key"] = "MK-IST-DESK-01",
                    ["mode_before"] = "R0",
                    ["mode_after"] = "R1",
                    ["action"] = "elevate_mode",
                    ["actor"] = "Admin",
                    ["target_id"] = "MK-IST-DESK-01",
                    ["note"] = "Yazıcı onarımı için R1 moduna yükseltildi",
                    ["created_at"] = now.AddHours(-2).ToString("o")
                }
            };

            RemoteRepairs = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["id"] = "REP-001",
                    ["machine_key"] = "MK-IST-DESK-01",
                    ["playbook_id"] = "printers.spooler.restart.v1",
                    ["parameters"] = "",
                    ["status"] = "pending",
                    ["initiated_by"] = "Admin",
                    ["dispatched_at"] = now.AddMinutes(-5).ToString("o"),
                    ["completed_at"] = null,
                    ["output"] = null
                }
            };

            Save();
        }
    }

    private void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var txt = File.ReadAllText(_filePath);
                    var doc = JsonSerializer.Deserialize<LocalDatabase>(txt);
                    if (doc != null)
                    {
                        Cases = doc.Cases ?? new();
                        Machines = doc.Machines ?? new();
                        Batches = doc.Batches ?? new();
                        HelpRequests = doc.HelpRequests ?? new();
                        Audits = doc.Audits ?? new();
                        RemoteRepairs = doc.RemoteRepairs ?? new();
                    }
                }
            }
            catch { }
        }
    }
}
