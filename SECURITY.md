# Güvenlik Politikası

## Desteklenen sürüm

Bu depo erken geliştirme aşamasındadır. Güvenlik düzeltmeleri yalnızca varsayılan dalın güncel sürümüne uygulanır. Yayın sürümleri ve desteklenen Windows sürümleri sürüm notlarında belirtilir.

## Güvenlik açığı bildirimi

Lütfen güvenlik açıklarını herkese açık bir issue olarak değil, depo sahibine özel bir kanaldan bildirin. Bildirimde:

- Etkilenen sürüm ve Windows sürümü,
- Yeniden üretim adımları,
- Beklenen ve gerçek davranış,
- Olası etki (yetki yükseltme, veri sızıntısı, onaysız değişiklik),
- Varsa önerilen düzeltme

yer almalıdır. Hassas veri içeren ekran görüntüleri veya gerçek kullanıcı belgeleri gönderilmemelidir.

## Bu sürümün güvenlik duruşu

**Sprint 1/2 salt okunurdur.** Uygulama:

- Hizmet durdurmaz/başlatmaz,
- Registry yazmaz,
- Sürücü kurmaz,
- Yazdırma işi silmez,
- Ağ taraması yapmaz,
- Onarım playbook'u çalıştırmaz.

Tanılama sırasında yalnızca okuma API'leri, WMI sorguları, Event Log okuma, registry okuma, `NetworkInterface` sorguları ve isteğe bağlı IPP HTTP/HTTPS isteği kullanılır. IPP isteği yalnızca kullanıcı `--ipp <yazıcı>` dediğinde yapılır. `--ipp-insecure-tls` yalnızca kullanıcı açıkça verdiğinde sertifika doğrulamasını kapatır; bu, güvenilmeyen bir ağda cihaz taklidine yol açabilir.

## Güven modeli

- **Arayüz:** standart kullanıcı; sistem değişikliği yapmaz.
- **Tanılama:** düşük yetki, salt okunur.
- **Politika motoru:** risk ve onay kararını verir; LLM'den bağımsızdır.
- **Araç yürütücüsü (gelecek):** yalnızca katalogdaki sabit araçları, şemalı parametrelerle çalıştırır. Genel amaçlı shell, serbest PowerShell/CMD, keyfî dosya yolu veya URL yoktur.
- **LLM/RAG:** sistem yetkisi yoktur; araç çağıramaz. Çıktısı yalnızca aday teşhis/playbook önerisidir.
- **IPC (gelecek):** Named Pipe + SID ACL + oturum nonce'u; istemci süreç kimliği tek başına kimlik doğrulama sayılmaz. Risk ve onay servis tarafında doğrulanır.

## Risk ve onay

| Seviye | Örnek | Onay |
|---|---|---|
| R0 | Salt okunur inceleme | Gerekmez |
| R1 | Hizmeti yeniden başlatma | Kullanıcı onayı; kesinti açıklanır |
| R2 | Sürücü kurma, port değiştirme, iş silme | Açık onay + pre/post-check |
| R3 | UAC/güvenlik duvarı/grup ilkesi gevşetme, firmware | Otomatik değil; uzman + ayrı onay |

Güvenliği azaltan bir değişiklik “güvenli düzeltme” olarak sınıflandırılamaz. Kurumsal politika R2/R3 işlemleri tamamen yasaklayabilir.

## Doğrulama ve başarı

- Komutun `exit code = 0` dönmesi başarı değildir.
- Başarı; tanımlı post-check'lerin geçmesi ve kullanıcı sonucunun doğrulanmasıdır.
- Otomatik kontrol ile kullanıcı onayı çelişirse sonuç `contradicted` olur; başarı bildirilmez.
- Kanıt yoksa sonuç `unknown` olur; “sorun yok” varsayılmaz.
- Fiziksel çıktı (kâğıt, toner, sıkışma) yazılımla kanıtlanamıyorsa bu açıkça belirtilir.

## Günlükler ve denetim

- Denetim kayıtları append-only olacak şekilde tasarlanır (uygulama sırasında).
- Hash zinciri tahrifi fark etmeye yardımcı olur; tek başına kayıtların gerçekliğini garanti etmez.
- İmza anahtarı yazılabilir veri klasöründe düz metin tutulmaz.
- CLI `--log` yalnızca kaynak, durum, süre ve hata kodu yazar; yazıcı/belge yolu veya istisna metni yazmaz.

## Dış içerik ve LLM

- Web içeriği veri kabul edilir; talimat veya yetki kaynağı değildir.
- LLM çıktısı şema doğrulamasından geçmeden kullanılmaz.
- LLM; `inf_path`, registry yolu, dosya yolu, URL veya serbest komut üretemez.
- LLM devre dışıyken çekirdek teşhis ve politika çalışmaya devam eder.

## Sorumlu açıklama

Bu proje hiçbir Windows sürümünde “tüm sorunları otomatik çözer” iddiasında değildir. Yazılım, sistem değiştiren işlemler için açık onay, geri alma sınırı ve güvenli duruş olmadan üretim ortamında kullanılmamalıdır.
