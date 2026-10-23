namespace Yapair.Core.Playbooks;

public enum PlaybookRisk
{
    R0,
    R1,
    R2,
    R3,
}

/// <summary>
/// Declarative repair contract. A playbook is executable only through the
/// privileged broker; in this sprint the catalogue is read-only and no executor
/// exists. Every playbook must state preconditions, post-checks, and rollback
/// limits before it is allowed into the catalogue.
/// </summary>
public sealed record RepairPlaybook
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public Contracts.HealthDomain Domain { get; init; }

    public PlaybookRisk Risk { get; init; }

    public bool RequiresApproval { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Steps { get; init; } = [];

    public IReadOnlyList<string> Preconditions { get; init; } = [];

    public IReadOnlyList<string> PostChecks { get; init; } = [];

    public string? Rollback { get; init; }
}

public static class PlaybookCatalog
{
    public static IReadOnlyList<RepairPlaybook> All { get; } = Create();

    public static RepairPlaybook? Find(string id) =>
        All.FirstOrDefault(playbook => string.Equals(playbook.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<RepairPlaybook> Create() =>
    [
        new RepairPlaybook
        {
            Id = "printers.spooler.restart.v1",
            Title = "Print Spooler hizmetini kontrollü yeniden başlat",
            Domain = Contracts.HealthDomain.Printers,
            Risk = PlaybookRisk.R1,
            RequiresApproval = true,
            Summary = "Spooler'ı durdurup yeniden başlatır; kuyruk dosyalarını silmez. Aktif yazdırma işlerinin kesilebileceği kullanıcıya açıklanır.",
            Steps = ["services.inspect:Spooler", "services.restart:Spooler", "printers.inspect_queue"],
            Preconditions = ["Spooler hizmeti mevcut", "Aktif işlerin kesileceği onaylandı"],
            PostChecks = ["Spooler durumu Running", "Yazıcı kuyruğu yeniden okunur"],
            Rollback = "Hizmet yeniden başlatma yan etkileri tamamen geri alınamaz; önceki başlangıç türü kaydedilir.",
        },
        new RepairPlaybook
        {
            Id = "printers.queue.inspect.v1",
            Title = "Yazdırma kuyruğunu incele ve raporla",
            Domain = Contracts.HealthDomain.Printers,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Kuyruktaki işleri belge adlarını redakte ederek okur. Hiçbir işi silmez.",
            Steps = ["printers.inspect_queue"],
            Preconditions = ["Yazıcı kimliği doğrulanmış"],
            PostChecks = ["Kuyruk sayısı ve durumları tekrar okunur"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "printers.device.review.v1",
            Title = "Yazıcı cihaz durumunu incele",
            Domain = Contracts.HealthDomain.Printers,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Win32_Printer, PnP ve isteğe bağlı IPP kanıtlarını birleştirir; fiziksel müdahale gerekiyorsa tarif eder.",
            Steps = ["printers.inspect_state", "devices.inspect", "ipp.inspect"],
            Preconditions = ["Yazıcı seçilmiş"],
            PostChecks = ["Durum ve veri kaynağı raporlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "services.controlled_restart.v1",
            Title = "Hizmeti kontrollü yeniden başlat",
            Domain = Contracts.HealthDomain.Services,
            Risk = PlaybookRisk.R1,
            RequiresApproval = true,
            Summary = "Yalnızca katalogda tanımlı, durdurulması güvenli hizmetler için kullanılır; bağımlılıklar önce kontrol edilir.",
            Steps = ["services.inspect", "services.restart"],
            Preconditions = ["Hizmet kimliği allowlist içinde", "Bağımlı hizmetler doğrulandı", "Kullanıcı onayı"],
            PostChecks = ["Hizmet durumu Running", "İlgili işlev tekrar denenir"],
            Rollback = "Önceki durum ve başlangıç türü kaydedilir; yeniden başlatma yan etkileri geri alınamayabilir.",
        },
        new RepairPlaybook
        {
            Id = "services.crash.review.v1",
            Title = "Hizmet çökme olaylarını incele",
            Domain = Contracts.HealthDomain.Services,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "7031/7034 olaylarını ve ilgili hizmetleri eşleştirir; otomatik yeniden başlatma yapmaz.",
            Steps = ["event_logs.inspect:System", "services.inspect"],
            Preconditions = ["Olay günlüğü erişilebilir"],
            PostChecks = ["Eşleşen hizmet durumu raporlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "devices.driver.review.v1",
            Title = "Aygıt sürücüsü ve yapılandırma hatasını incele",
            Domain = Contracts.HealthDomain.Devices,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "PnP hata kodunu, donanım kimliğini ve sürücü durumunu raporlar. Sürücü indirmez veya kurmaz.",
            Steps = ["devices.inspect", "drivers.inspect"],
            Preconditions = ["Aygıt kimliği doğrulanmış"],
            PostChecks = ["Hata kodu ve PnP durumu tekrar okunur"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "devices.display_driver.review.v1",
            Title = "Görüntü sürücüsü çökmesini incele",
            Domain = Contracts.HealthDomain.Devices,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "4101 olayını ve görüntü aygıtı sürücüsünü raporlar; sürücü değiştirmez.",
            Steps = ["event_logs.inspect:System", "devices.inspect"],
            Preconditions = ["Olay günlüğü erişilebilir"],
            PostChecks = ["İlgili aygıt durumu raporlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "security.uac.review.v1",
            Title = "UAC yapılandırmasını gözden geçir",
            Domain = Contracts.HealthDomain.Security,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "UAC ve güvenli masaüstü ayarlarını raporlar. Güvenlik ayarını otomatik değiştirmez; değişiklik R3 onayı gerektirir.",
            Steps = ["security.inspect_uac"],
            Preconditions = ["Politika anahtarları okunabilir"],
            PostChecks = ["Değerler ve etki açıklanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "security.firewall.review.v1",
            Title = "Güvenlik duvarı profil durumunu incele",
            Domain = Contracts.HealthDomain.Security,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Profil bazında güvenlik duvarı durumunu raporlar; kapatmaz veya kural değiştirmez.",
            Steps = ["security.inspect_firewall"],
            Preconditions = ["Politika anahtarları okunabilir"],
            PostChecks = ["Profil durumları raporlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "network.adapter.review.v1",
            Title = "Ağ bağdaştırıcısı durumunu incele",
            Domain = Contracts.HealthDomain.Network,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Bağdaştırıcı, bağlantı ve sürücü durumunu raporlar; ağ ayarlarını değiştirmez.",
            Steps = ["network.inspect_adapters"],
            Preconditions = ["Yerel ağ API'si erişilebilir"],
            PostChecks = ["Bağdaştırıcı durumu tekrar okunur"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "network.dns.review.v1",
            Title = "DNS yapılandırmasını incele",
            Domain = Contracts.HealthDomain.Network,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Tanımlı DNS sunucularını raporlar; DNS değiştirmez ve dış sorgu yapmaz.",
            Steps = ["network.inspect_dns"],
            Preconditions = ["Yerel ağ API'si erişilebilir"],
            PostChecks = ["DNS listesi raporlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "storage.free_space.review.v1",
            Title = "Boş disk alanını incele",
            Domain = Contracts.HealthDomain.Storage,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Birim doluluk oranını raporlar; dosya silmez veya taşımaz.",
            Steps = ["storage.inspect_volumes"],
            Preconditions = ["Birim bilgisi okunabilir"],
            PostChecks = ["Boş alan yeniden ölçülür"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "storage.disk_health.review.v1",
            Title = "Disk sağlık durumunu incele",
            Domain = Contracts.HealthDomain.Storage,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Disk durumunu raporlar; yüzey taraması veya onarım başlatmaz.",
            Steps = ["storage.inspect_disks"],
            Preconditions = ["Disk bilgisi okunabilir"],
            PostChecks = ["Disk durumu raporlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "windows_update.pending_reboot.review.v1",
            Title = "Bekleyen yeniden başlatmayı incele",
            Domain = Contracts.HealthDomain.WindowsUpdate,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "CBS, Windows Update ve bekleyen dosya işlemlerini raporlar; yeniden başlatmayı zorlamaz.",
            Steps = ["windows_update.inspect_pending_reboot"],
            Preconditions = ["Registry anahtarları okunabilir"],
            PostChecks = ["Bekleyen işlem bayrakları raporlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "system.memory.review.v1",
            Title = "Bellek kullanımını incele",
            Domain = Contracts.HealthDomain.System,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "Boş fiziksel bellek oranını raporlar; işlem sonlandırmaz.",
            Steps = ["system.inspect_memory"],
            Preconditions = ["WMI erişilebilir"],
            PostChecks = ["Bellek ölçümü tekrarlanır"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "system.crash.review.v1",
            Title = "Sistem çökme kaydını incele",
            Domain = Contracts.HealthDomain.System,
            Risk = PlaybookRisk.R0,
            RequiresApproval = false,
            Summary = "BugCheck/1001 olaylarını raporlar; dump analizi veya yeniden başlatma yapmaz.",
            Steps = ["event_logs.inspect:System"],
            Preconditions = ["Olay günlüğü erişilebilir"],
            PostChecks = ["İlgili olaylar listelenir"],
            Rollback = "Değişiklik yok.",
        },
        new RepairPlaybook
        {
            Id = "network.ip.change.v1",
            Title = "Ağ bağdaştırıcısı IP yapılandırmasını değiştir",
            Domain = Contracts.HealthDomain.Network,
            Risk = PlaybookRisk.R2,
            RequiresApproval = true,
            Summary = "Seçili bağdaştırıcının IPv4 adresini, alt ağ maskesini, gateway'ini ve DNS'ini değiştirir. Bağlantı kesintisi olabilir; eski yapılandırma önce kaydedilir.",
            Steps = ["network.inspect_config", "network.apply_ip_config", "network.verify_config"],
            Preconditions =
            [
                "Yönetici yetkisi",
                "Eski IP/gateway/DNS kaydedildi",
                "Hedef IP, alt ağ ve gateway doğrulandı",
                "Kurumsal ağ politikasıyla çelişmiyor",
                "Kullanıcı bağlantı kesintisini onayladı",
            ],
            PostChecks =
            [
                "NetworkInterface yapılandırması hedef IP ile eşleşiyor",
                "Varsayılan gateway ve DNS tanımlı",
                "Yerel ağ bağlantısı doğrulandı",
            ],
            Rollback = "Önceki IPv4 yapılandırması geri yüklenir; uzak oturum kopmuşsa yerel müdahale gerekebilir.",
        },
        new RepairPlaybook
        {
            Id = "apps.launch.allowlisted.v1",
            Title = "İzinli uygulamayı aç",
            Domain = Contracts.HealthDomain.System,
            Risk = PlaybookRisk.R1,
            RequiresApproval = true,
            Summary = "Allowlist'teki veya kullanıcının açıkça seçtiği uygulamayı argümansız başlatır. Keyfî dosya yolu veya shell komutu kabul etmez.",
            Steps = ["apps.resolve_allowlisted", "apps.launch"],
            Preconditions =
            [
                "Uygulama allowlist içinde veya kullanıcı tarafından seçilmiş",
                "Yol imzalı/beklenen yayıncıya ait",
                "Kullanıcı onayı",
            ],
            PostChecks = ["Süreç başladı", "Uygulama pencere/yanıt durumu kontrol edildi"],
            Rollback = "Uygulama kullanıcı tarafından kapatılır; zorla sonlandırma ayrı onay gerektirir.",
        },
        new RepairPlaybook
        {
            Id = "devices.driver.install.v1",
            Title = "Katalogdan imzalı sürücü kur",
            Domain = Contracts.HealthDomain.Devices,
            Risk = PlaybookRisk.R2,
            RequiresApproval = true,
            Summary = "PnP donanım kimliğini resmî katalogdaki driver_id ile eşleştirir; imza, yayıncı, hash, model, mimari ve Windows sürümünü doğruladıktan sonra pnputil ile kurar.",
            Steps =
            [
                "devices.inspect_hardware_ids",
                "drivers.resolve_catalog",
                "drivers.verify_signature",
                "drivers.install_pnputil",
                "devices.verify_pnp_state",
            ],
            Preconditions =
            [
                "Yönetici yetkisi",
                "Donanım kimliği yerel keşiften geldi",
                "Sürücü resmî allowlist kaynağında",
                "Authenticode imzası ve yayıncı doğrulandı",
                "Model/mimari/Windows sürümü eşleşiyor",
                "Kullanıcı onayı",
            ],
            PostChecks =
            [
                "ConfigManagerErrorCode = 0",
                "PnP durumu OK",
                "Cihaz işlevi/çıktısı doğrulandı",
            ],
            Rollback = "Önceki sürücü paketi/bağlantısı kaydedilir; kernel sürücüsünde tam geri alma garanti değildir.",
        },
    ];
}
