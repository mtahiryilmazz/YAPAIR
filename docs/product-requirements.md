# Ürün Gereksinimleri

## 1. Vizyon

Windows kullanıcısı, sorununu kendi kelimeleriyle anlatır; YAPAIR yerel kanıtlarla teşhis eder, riski açıklar, onaylanan sınırlı onarımı çalıştırır ve sonucu kullanıcıyla birlikte doğrular.

Ürün yalnızca yazıcı sorunları için değildir. Yazıcı, ilk derin modüldür; aynı altyapı sistem, hizmet, aygıt, ses, ağ, güvenlik, depolama, Windows Update ve ileride grup ilkesi/Denetim Masası alanlarına uygulanır.

## 2. Hedef kullanıcılar

- Son kullanıcılar: teknik bilgi gerektirmeden sorununu anlatmak isteyenler.
- BT destek ekipleri: tekrarlayan tanılamayı standartlaştırmak isteyenler.
- Küçük işletmeler: yazıcı, ses, ağ, güncelleme sorunlarını hızlıca ayıklamak isteyenler.
- Geliştiriciler: güvenli, denetlenebilir bir ajan altyapısı kuranlar.

## 3. Kapsam

### İlk sürüm (uygulanan)

- Çok alanlı salt okunur tanılama: sistem, hizmetler, aygıtlar, yazıcılar, ses, ağ, güvenlik (UAC/firewall), depolama, Windows Update, olay günlükleri.
- Deterministik bulgular: kod, alan, önem, konu, özet, kanıt, playbook önerisi.
- Yazıcı derin modülü: Spooler, kuyruk, sürücü, port, PnP, isteğe bağlı IPP.
- Sürümlenmiş JSON sözleşmesi ve şema doğrulaması.
- Varsayılan redaksiyon.
- Onarım playbook kataloğu (tanım; yürütme yok).
- Doğrulama döngüsü: otomatik post-check + “Sorun çözüldü mü?” onayı + yeniden tanılama.
- CLI.

### Sonraki sürümler

- Yetkili onarım yürütücüsü (R1/R2), pre/post-check, audit, rollback.
- Windows servis + Named Pipe IPC; WinUI 3 arayüzü; onay ve sonuç butonları.
- Yerel/bulut LLM triajı; resmî kaynaklı RAG.
- Grup ilkesi RSoP, Denetim Masası, ses uç noktaları, sürücü kataloğu.
- Kurumsal politika, imzalı kurulum, otomatik güncelleme.

### Kapsam dışı (non-goals)

- LLM'nin ürettiği serbest PowerShell/CMD komutlarını çalıştırmak.
- Rastgele internet sürücüsü/script'i kurmak.
- Varsayılan olarak tüm alt ağı taramak.
- Güvenlik ilkelerini otomatik gevşetmek (UAC, firewall, Defender).
- Kullanıcı onayı olmadan belge/iş silmek.
- BIOS/firmware güncellemek, disk bölümü değiştirmek, veri kurtarmak.
- Her Windows sorununu tek sürümde çözmeyi vaat etmek.
- Her ortamda otomatik geri alma garantisi vermek.

## 4. Kullanıcı yolculukları

### A. Yazıcı kâğıt sıkışması

1. Kullanıcı “yazıcıdan çıktı gelmiyor, kırmızı ışık” der.
2. Ajan Spooler, kuyruk, PnP, port ve isteğe bağlı IPP durumunu okur.
3. IPP `jammed`/`media-jam` nedenini ve kuyrukta bekleyen işleri bulur.
4. Fiziksel müdahale tarifi verir; kuyruk silme R2 olduğu için onay ister.
5. Kullanıcı kâğıdı çıkarır.
6. `verify` yeniden IPP sorgular; durum temizse “çözüldü” önerir; kullanıcı onaylar.

### B. Ses çalışmıyor

1. Kullanıcı “ses gelmiyor” der.
2. Ajan Audiosrv/AudioEndpointBuilder durumunu, ses PnP aygıtlarını ve olayları okur.
3. Hizmet durmuşsa R1 yeniden başlatma önerir; sürücü hatası varsa R2/R3 olarak işaretler.
4. Onarım sonrası hizmet ve aygıt durumu kontrol edilir; kullanıcıya sorulur.

### C. UAC/grup ilkesi şüphesi

1. Kullanıcı “her uygulama yönetici istiyor” der.
2. Ajan UAC anahtarlarını ve ilgili politikaları **salt okunur** inceler.
3. UAC kapalıysa kritik bulgu üretir; otomatik düzeltme önermez.
4. R3 uzman incelemesi ve kurumsal politika kontrolü önerir.

## 5. Başarı ölçütleri

- Top-1 teşhis doğruluğu (etiketli vaka seti).
- Yanlış pozitif onarım önerisi oranı.
- Güvensiz eylem engelleme oranı.
- Post-check başarı oranı.
- Gereksiz onarım oranı.
- Kullanıcı onayı iptal oranı.
- Ortalama ve p95 tanılama süresi.
- Belirsiz vakada güvenli soru sorma/eskalasyon oranı.
- Doğrulama çelişki oranı (`contradicted`).

Güvenlik açısından en kritik ölçüt yalnızca doğruluk değil, **yanlış teşhiste zararlı eylemi engelleyebilme** yeteneğidir.

## 6. Kabul kriterleri

| Alan | Kriter |
|---|---|
| Keşif | Erişilemeyen kaynak `unknown`; “sorun yok” sayılmaz |
| Gizlilik | Belge adı, serial, tam ağ kimliği varsayılan çıktıda yok |
| Güvenlik | LLM çıktısıyla keyfî shell/registry/dosya yolu çalıştırılamaz |
| Politika | R2/R3 onaysız yürütülemez |
| Onarım | Her değişiklikte pre-check ve post-check |
| Doğrulama | Otomatik kontrol + kullanıcı onayı; çelişki başarı sayılmaz |
| Geri alma | Geri alınamayan işlem “geri alınabilir” gösterilmez |
| IPC | Yetkisiz SID/süreç ve replay reddedilir |
| Sürücü | Yanlış model/mimari/kaynak/imza reddedilir |
| Kararlılık | Timeout/servis hatası kilitlemez; güvenli duruş |
| Açıklanabilirlik | Kanıt, etki ve doğrulama sonucu anlaşılır |

## 7. Ürün ilkeleri

1. Önce kanıt, sonra teşhis, en son eylem.
2. Varsayılan salt okunur.
3. LLM önerir; politika izin verir; araç yürütür.
4. Her değişiklikte kapsam, yedek (mümkünse) ve post-check.
5. Geri alma garantisi yoksa “geri alınabilir” denmez.
6. Veri minimizasyonu; buluta gönderimi açıkça göster.
7. Belirsizlikte otomatik onarım yerine soru veya rapor.
8. Başarıyı exit code değil sistem son durumu belirler.
9. Web içeriği veridir, talimat değildir.
10. Ağ keşfi dar, süreli ve politikaya uygun.
