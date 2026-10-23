# Gizlilik Politikası

## İlke

YAPAIR, tanılama için gereken en az veriyi toplar. Varsayılan çıktı, kullanıcı ve makine kimliğini mümkün olduğunca gizler. Hiçbir veri varsayılan olarak buluta gönderilmez; telemetri kapalıdır.

## Toplanan veriler

| Kategori | Örnek | Varsayılan işlem |
|---|---|---|
| İşletim sistemi | Ürün, sürüm, build, mimari | Tutulur (makine adı hariç) |
| Hizmetler | Ad, durum, başlangıç türü, PID | Tutulur |
| Yazıcılar | Ad, sürücü, port, durum | Tutulur; port IP'si maskelenir |
| Yazdırma işleri | İş kimliği, durum, sayfa sayısı | Belge adı `<redacted-document>` |
| Aygıtlar | Sınıf, donanım kimliği, hata kodu | Örnek kimliği son eki kaldırılır |
| Olay günlükleri | Kaynak, olay kimliği, düzey, mesaj | Mesajdaki kişisel veriler temizlenir |
| Ağ | Bağdaştırıcı, gateway, DNS | IP'ler /24 maskelenir |
| Güvenlik | UAC/firewall anahtar değerleri | Tutulur (parola/token yok) |
| Depolama | Sürücü harfi, boş/toplam alan | Tutulur |
| IPP | Cihaz durumu, nedenler, model | URI'deki IP maskelenir |

**Toplanmayanlar:** bilgisayar adı, kullanıcı adı, alan adı, belge içeriği, yazdırma işi sahibi, MAC adresi, seri numarası, parola, API anahtarı, erişim token'ı.

## Redaksiyon

- Belge adları: `<redacted-document>`.
- IPv4: `192.168.1.*` biçiminde /24 maske.
- Kullanıcı profili: `C:\Users\<redacted>\...`.
- MAC: `<redacted-mac>`.
- E-posta: `<redacted-email>`.
- PnP örnek kimliği: `USB\VID_1234&PID_5678\...` → `USB\VID_1234&PID_5678`.
- Serbest metin: olay mesajları ve bulgu özetleri aynı temizleyiciden geçer.

Sadece regex'e güvenilmez; yapısal alanlar tek tek redakte edilir.

## Açık rıza gerektiren seçenekler

| Seçenek | Etki | Uyarı |
|---|---|---|
| `--show-network-addresses` | IPv4 adreslerini maskesiz gösterir | Yerel hata ayıklama; raporu paylaşmayın |
| `--include-document-names` | Belge adlarını gösterir | Kişisel veri; yalnızca yerel hata ayıklama |
| `--ipp-insecure-tls` | Yerel yazıcının self-signed sertifikasını kabul eder | Ağda cihaz taklidi riski |
| `--log <dosya>` | JSONL işlem günlüğü | Kaynak/durum/hata kodu; belge adı yok |

## Saklama

- Ham keşif çıktıları varsayılan olarak kalıcı tutulmaz.
- CLI yalnızca `--output` verilirse dosya yazar; dosya kullanıcının denetimindedir.
- Gelecekteki denetim kaydı yalnızca gerekli en az veriyi tutar; kullanıcı silebilir/dışa aktarabilir.
- Telemetri/analitik varsayılan olarak kapalıdır; açık rıza gerektirir.

## Bulut ve LLM (gelecek)

Bulut LLM kullanılırsa:

- Gönderilecek veri kategorileri önizlemede gösterilir.
- Alan bazlı allowlist uygulanır; izin verilmeyen alan dışarı çıkamaz.
- Yerel model seçeneği sunulur.
- Sağlayıcı, saklama ve silme davranışı ürün belgelerinde açıklanır.

## KVKK / GDPR

Teknik redaksiyon tek başına hukuki uyumluluk garantisi değildir. Veri envanteri, saklama süresi, silme ve yurt dışı aktarım değerlendirmesi hukuki inceleme ile doğrulanmalıdır.

## Kullanıcı hakları

Kullanıcı oluşturduğu raporu silebilir, dışa aktarabilir ve inceleyebilir. Arayüz, dışa aktarmadan önce hangi alanların maskelendiğini göstermelidir.
