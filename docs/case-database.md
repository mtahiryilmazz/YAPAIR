# Vaka Veritabanı ve Kümeleme (POC)

## 1. Amaç

100’e kadar PC’den gelen farklı cümleleri:

1. yerel olarak kaydetmek,
2. normalize edip semptom ailesine/koduna eşlemek,
3. benzer vakaları kümelemek,
4. destek ekibinin önceliklendirmesi ve program geliştirme için kullanmak.

Örnek: beş kullanıcı aynı yazıcı arızasını farklı kelimelerle anlatır:

| Kullanıcı ifadesi | Aile | Semptom kodu |
|---|---|---|
| “Yazıcı yazdırmıyor” | printers | `printer_output_failure` |
| “Yazıcı çevrimdışı” | printers | `printer_offline` |
| “Yazıcı görünmüyor” | printers | `printer_not_visible` |
| “Çıktı gelmiyor, spooler takıldı” | printers | `printer_queue` |
| “Yazıcı bağlı değil” | printers | `printer_offline` |

Sonuç: tek “Yazıcı sorunları” ailesi, altında üç-dört alt tür. Böylece hem “hepsi yazıcı” görünür hem de kök neden ayrımı korunur.

## 2. Yerel POC (uygulandı)

- SQLite: `%LOCALAPPDATA%\YAPAIR\cases.db`
- Tablo: `cases` (case_id, zaman, normalize metin, semptom kodu, aile, domain, sonuç, playbook, makine anahtarı/etiketi)
- Ham metin varsayılan olarak **saklanmaz**; `--store-raw` açık rıza.
- Makine kimliği:
  - `machine_label`: bilgisayar adı (destek için okunabilir etiket)
  - `machine_key`: SMBIOS UUID/BIOS serial’ın SHA-256 özeti
  - Ham HWID/serial **saklanmaz**.
- Bilinmeyen metinler token benzerliği (Jaccard) ile gruplanır.

### Komutlar

```powershell
# Tek vaka
yapair cases add --text "Yazıcı çevrimdışı" --db cases.db

# 10 PC’den gelen JSONL dosyası
yapair cases import --file data\sample-complaints.jsonl --db cases.db

# Aile + alt tür raporu
yapair cases cluster --db cases.db --format summary

# Sonuç etiketi (geliştirme/ölçüm için)
yapair cases outcome --case case-... --outcome resolved --db cases.db
```

### Örnek çıktı

```text
Yazıcı sorunları (printers): 5 vaka
  - printer_offline: 2
  - printer_output_failure: 1
  - printer_not_visible: 1
  - printer_queue: 1
```

## 3. Makine kimliği kararı

Kullanıcı bilgisayar adı veya HWID kullanılmasını istedi. Uygulanan model:

- **Etiket:** bilgisayar adı (`BT`, `MUHASEBE-01` gibi). Destek ekibi için görünür.
- **Anahtar:** `SHA-256("yapair.machine.v1|hwid:<SMBIOS UUID>")`.
- HWID yoksa BIOS serial, o da yoksa bilgisayar adı hash’e girer.
- Ham UUID/serial hiçbir tabloya yazılmaz.

Bu model makineyi ayırt etmeye yarar; ham donanım kimliğini merkeze taşımaz. Yine de etiket + hash **kişisel veri/pseudonymous** sayılmalıdır; KVKK aydınlatma ve saklama politikası gerekir. Bilgisayar adı değişirse etiket değişir; anahtar HWID varsa sabit kalır.

## 4. Merkezî veritabanı (planlanan)

100 PC için yerel SQLite merkez olmaz. Hedef mimari:

```text
Her PC: SQLite (cache + outbox)
        │ HTTPS, kuruluma özel token
        ▼
ASP.NET Core API (auth, rate limit, doğrulama)
        │
        ▼
Supabase PostgreSQL
        │
        ▼
Destek paneli: kümeler, eğilimler, sonuçlar
```

- Ajan çevrimdışı çalışır; vakaları `outbox` kuyruğuna alır.
- Bağlantı gelince idempotent `POST /cases` ile gönderir.
- Merkezde ham metin yok; `machine_key`, `symptom_code`, `family`, `outcome` gibi alanlar.
- `machine_label` yalnızca yetkili destek rolüne gösterilir.
- Sunucu tarafı kümeleme ve dashboard; ajan tarafı hızlı ön sınıflandırma.
- Saklama: varsayılan 90 gün; kurum politikasına göre değiştirilebilir.
- RLS (Row Level Security) ve kurulum başına token.

### Neden Supabase?

- Yönetilen PostgreSQL, hazır auth, RLS, yedekleme.
- 100 PC için yeterli; büyüme halinde aynı PostgreSQL şeması taşınabilir.
- API için yine ince bir ASP.NET Core katmanı önerilir; doğrudan istemciden tabloya yazmak yerine doğrulama/rate limit.

## 5. Geliştirme için kullanım

- Haftalık küme raporu: hangi aile/semptom artıyor?
- Sonuç etiketleri: `resolved`, `not_resolved`, `contradicted` oranları.
- Playbook önceliği: en sık ve en başarısız kümeler.
- Yeni anahtar kelimeler: bilinmeyen metinlerden normalize sözlüğüne ekleme.
- Ham metinle model eğitimi yok; yalnızca anonimleştirilmiş/izinli veri.

## 6. Sınırlamalar

- Bu sürüm yalnızca yerel POC; merkezî senkronizasyon yok.
- Kural tabanlı normalizasyon; eş anlamlılar sınırlı. İleride embedding/LLM destekli sınıflandırma.
- Jaccard yalnızca bilinmeyen metinlerde kaba bir gruplama.
- Makine kimliği hash’i kişisel veri olarak değerlendirilmelidir.
