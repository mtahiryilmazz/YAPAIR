# Sürücü Yönetimi ve Paket Araçları

## 1. Temel ayrım

Windows sürücüsü ile uygulama paketi aynı şey değildir:

| Araç | Ne yapar | Sürücü için uygun mu? |
|---|---|---|
| `pnputil` | `.inf` sürücü paketini sürücü deposuna ekler/cihaza bağlar | **Evet, birincil yerel araç** |
| Windows Update / Microsoft Update Catalog | Resmî, imzalı sürücü paketleri | **Evet, birincil kaynak** |
| Üretici resmî kataloğu | Modele özel imzalı sürücü | **Evet, allowlist ile** |
| `winget` | Uygulama/paket kurulumu; bazı üretici uygulamaları | Sürücü için güvenilir değil; **uygulama** için opsiyonel |
| Chocolatey | Topluluk paket yöneticisi | Sürücü için önerilmez; tedarik zinciri riski |
| NuGet | .NET kütüphane paketleri | **Sürücü değil**; kullanılmaz |

Yani “winget/choco/nuget ile sürücü kuralım” doğru temel değildir. Sürücü; donanım kimliği → resmî kaynak → imza/hash doğrulama → `pnputil` zinciriyle kurulur.

## 2. Cihaz kimliğinden sürücüye akış

```text
1. PnP keşfi
   HardwareID / CompatibleID / VID&PID / ConfigManagerErrorCode
        │
2. Donanım eşleşmesi
   Model, üretici, mimari (x64/ARM64), Windows sürümü, sürücü sürümü
        │
3. Katalog sorgusu
   Windows Update / Microsoft Update Catalog / üretici resmî kaynağı
        │
4. Paket doğrulama
   Authenticode imza, yayıncı, hash, arşiv path traversal, paket kimliği
        │
5. Onay (R2)
   Kullanıcı etkiyi ve geri alma sınırını görür
        │
6. Kurulum
   pnputil /add-driver <inf> /install  (yönetici)
        │
7. Post-check
   ConfigManagerErrorCode=0, PnP durumu OK, cihaz işlevi
        │
8. Rollback
   Önceki sürücü paketi/bağlantısı kaydedilir; tam geri alma garanti değildir
```

## 3. Güvenlik kuralları

- LLM **asla** `inf_path`, dosya yolu veya URL veremez. Yalnızca katalog `driver_id` seçebilir.
- Kaynak allowlist’i: Microsoft / üreticinin resmî alan adı.
- İmza geçerli olsa bile model, mimari ve Windows sürümü eşleşmelidir.
- İmzasız, süresi geçmiş, yanlış yayıncı, yanlış mimari paket reddedilir.
- İndirme sırasında HTTPS ve yönlendirme hedefi kontrol edilir.
- Arşiv açma path traversal ve beklenmeyen dosya türü kontrolü içerir.
- Sürücü kurulumu **R2**; açık onay, audit, post-check ve rollback kaydı zorunlu.
- Kernel sürücüsü mavi ekran/kararsızlık riski taşır; üretimde kademeli dağıtım ve VM testi gerekir.
- “Sürücü güncelle” tek başına başarı değildir; cihazın gerçekten çalıştığı doğrulanır.

## 4. Katalog modeli

```json
{
  "driver_id": "kyocera-taskalfa-mz2501ci-x64-1.0.0",
  "vendor": "Kyocera",
  "model": "TASKalfa MZ2501ci",
  "hardware_ids": ["USBPRINT\\..."],
  "architecture": "x64",
  "windows_versions": ["10", "11"],
  "version": "1.0.0",
  "source_url": "https://...",
  "sha256": "...",
  "signature_publisher": "Kyocera Document Solutions Inc.",
  "inf_name": "oemXX.inf",
  "risk": "R2",
  "rollback": "Önceki sürücü paketi kaydedilir"
}
```

- `source_url` kullanıcıya gösterilir; LLM üretemez.
- `driver_id` katalogda sabittir; indirme URL’si uygulama içinde çözülür.
- Katalog merkezî Supabase’te tutulabilir; 100 PC aynı allowlist’i kullanır.

## 5. winget / choco ne zaman?

- **Uygulama kurulumu** için düşünülebilir: örn. üreticinin tarayıcı/yazıcı yazılımı.
- Yine de paket kimliği allowlist’te olmalı, kaynak ve hash doğrulanmalı, R2 onayı alınmalı.
- Sürücü kurulumunun yerine geçmez; `pnputil`/Windows Update birincil yoldur.
- Chocolatey topluluk paketleri kurumsal ortamda varsayılan kapalı olmalıdır.

## 6. Uygulama durumu

- Bu sürüm **salt okunur**; sürücü indirme/kurma yok.
- Planlanan playbook: `devices.driver.install.v1` (R2, onay, pre/post-check, rollback).
- Önce katalog + imza doğrulama + kuru çalıştırma (dry-run), sonra gerçek kurulum.
- Test: imzasız, yanlış mimari, yanlış model, path traversal, kesilmiş indirme, rollback.
