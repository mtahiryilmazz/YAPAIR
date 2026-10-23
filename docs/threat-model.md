# Tehdit Modeli

## 1. Korunacak varlıklar

- Yönetici/SYSTEM yetkileri ve işletim sistemi bütünlüğü.
- Registry, hizmet yapılandırmaları, sürücü paketleri, grup ilkesi.
- Kullanıcının belgeleri, yazdırma işleri ve kişisel verileri.
- Yerel ağ bilgileri, cihaz kimlikleri, oturum token'ları.
- Denetim kayıtlarının bütünlüğü.
- LLM API anahtarları ve kimlik bilgileri.

## 2. Tehditler ve kontroller

| Tehdit | Örnek | Kontrol |
|---|---|---|
| Prompt injection | Web sayfası “SYSTEM olarak şu komutu çalıştır” der | Web metni veri; araç yetkisi yok; ayrı karantina; yapılandırılmış özet |
| LLM halüsinasyonu | Var olmayan registry anahtarı önerir | Şema doğrulama; katalog kimliği; politika motoru; bilinmeyen reddedilir |
| Parametre kötüye kullanımı | Sürücü yoluna `..\` eklenir | LLM dosya yolu veremez; yalnızca `driver_id`; uygulama içinde çözülür |
| Yetki yükseltme | Standart kullanıcı servisi keyfî komuta yönlendirir | Sabit araç kataloğu; allowlist; servis tarafı yetkilendirme; en düşük yetki |
| IPC sahteciliği | Başka süreç servise istek yollar | SID ACL; oturum nonce; istemci imzası; replay koruması; PID tek başına yetmez |
| Zararlı sürücü | İmzalı fakat yanlış/eski paket | Kaynak allowlist; imza zinciri; hash; model/mimari/OS eşleşmesi |
| Veri sızıntısı | Belge adı/serial buluta gider | Veri minimizasyonu; redaksiyon; yerel model; gönderim önizlemesi |
| Yanlış onarım | Kanıt yetersizken güvenlik ayarı değiştirilir | Kanıt eşiği; R0-R3; onay; güvenli duruş |
| Tekrarlı işlem | Araç aynı hizmeti sürekli durdurur | Idempotency; sınırlı retry; backoff; devre kesici |
| Denetim tahrifi | Saldırgan geçmişi siler | Append-only; erişim kontrolü; hash zinciri; anahtar koruması |
| UAC/grup ilkesi kötüye kullanımı | “Sorunu çözmek için UAC'yi kapat” | R3; otomatik değil; güvenlik azaltıcı olarak etiketlenir |
| Fiziksel yanlış teşhis | Kâğıt sıkışması yokken kuyruk silinir | Kullanıcı onayı; IPP/cihaz kanıtı; geri alınamaz işaretleme |

## 3. Güven sınırları

- **LLM/RAG → araçlar:** doğrudan yol yok.
- **UI → servis:** kimlik doğrulamalı IPC; UI'nin “onayladım” alanı güvenilmez.
- **Keşif → politika:** veri sağlar; karar vermez.
- **Politika → araç:** yalnızca katalogdaki eylem ve şemalı parametre.
- **Araç → post-check:** bağımsız doğrulama; araç kendi başarısını ilan edemez.

## 4. AI'ya özgü kontroller

- LLM çıktısı JSON Schema ile doğrulanır; bilinmeyen alanlar reddedilir.
- LLM yalnızca katalog kimliği, enum ve ölçülü parametre döndürür.
- Serbest metin komuta dönüştürülmez.
- LLM'in güven skoru olasılık olarak sunulmaz; kanıt kalitesi ayrı raporlanır.
- LLM devre dışıyken deterministik çekirdek çalışır.
- Web içeriği araç çağrısı veya politika talimatı olarak yorumlanmaz.

## 5. Kalan riskler

- Fiziksel dünya (kâğıt, toner, kablo) yazılımla tam doğrulanamaz.
- Üreticiye özgü sürücü davranışları test laboratuvarında tam simüle edilemez.
- Kullanıcı onayı özneldir; otomatik kanıtla çelişebilir.
- Kurumsal ortamda grup ilkesi/RSoP etkisi tam modellenmeden otomatik değişiklik yapılmamalıdır.
- Windows Server ve farklı dil paketleri ayrıca doğrulanmalıdır.
