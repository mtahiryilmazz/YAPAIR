# Risk ve Onay Politikası

## 1. Risk sınıfları

| Sınıf | Tanım | Örnek | Varsayılan |
|---|---|---|---|
| **R0** | Salt okunur, sistem durumu değişmez | Servis/kuyruk/olay/disk/UAC anahtarı okuma | Otomatik |
| **R1** | Düşük etkili, geri alınabilir veya kesintisi açıklanabilir | Spooler, Audiosrv gibi hizmeti yeniden başlatma | Kullanıcı onayı; düşük riskli modda otomatik olabilir |
| **R2** | Etkili veya veri kaybı olası | Sürücü kurma, port değiştirme, seçili işleri silme, cihaz sıfırlama | Açık onay + pre/post-check |
| **R3** | Güvenliği azaltan veya yüksek etkili | UAC'yi kapatma, güvenlik duvarını kapatma, RPC sertleştirmesini kaldırma, grup ilkesi değiştirme, firmware | Otomatik değil; uzman + ayrı onay; kurumsal politikayla yasaklanabilir |

## 2. Onay matrisi

| İşlem | R0 | R1 | R2 | R3 |
|---|---|---|---|---|
| Tanılama | Otomatik | Otomatik | Otomatik | Otomatik |
| Kullanıcı onayı | Gerekmez | Gerekli | Gerekli | Gerekli + uzman |
| Kurumsal politika | — | Kapatılabilir | Kapatılabilir | Varsayılan kapalı |
| Geri alma | Değişiklik yok | Kaydedilir, tam garanti yok | Kaydedilir, tam garanti yok | Önceki değer dışa aktarılır |

## 3. Güvenlik azaltan değişiklikler

Aşağıdakiler **hiçbir zaman** “güvenli otomatik düzeltme” olarak sınıflandırılmaz:

- `EnableLUA = 0` (UAC kapatma)
- `ConsentPromptBehaviorAdmin = 0` (yönetici onayını kapatma)
- `PromptOnSecureDesktop = 0` (güvenli masaüstünü kapatma)
- Güvenlik duvarı profilini kapatma
- RPC/IPC sertleştirmesini kaldıran registry değişiklikleri
- Grup ilkesiyle güvenlik denetimini gevşetme
- Defender/antivirüs dışlama ekleme
- İmzasız sürücü yükleme

Bu durumlarda ajan yalnızca kanıt ve etki raporu üretir; değişiklik önerisi R3 olarak işaretlenir ve varsayılan olarak uygulanmaz.

## 4. Onay kaydı

Onay kaydı şunları içerir:

- `action_id`, `tool_id`, `tool_version`
- `risk_level`
- `approved_by` (yerel kullanıcı bağlamı)
- `approved_at`
- `scope` (hangi yazıcı/servis/aygıt)
- `effect_summary`
- `rollback_available`, `rollback_limit`
- Onayın hangi kanıta dayandığı

Onay, UI'dan gelen bir boolean alanı olarak kabul edilmez; servis tarafında oturum ve işlem bağlamıyla doğrulanır.

## 5. Güvenli duruş

- Timeout, servis hatası veya politika değişikliğinde işlem durur.
- Aynı araç art arda iki başarısız denemeden sonra devre kesiciye alınır.
- Kısmi işlem sonrası yeni eylemden önce sistem durumu yeniden okunur.
- Kullanıcı iptali, uygulama kapanması veya oturum sonunda güvenli duruş uygulanır.
- Başarısızlık “tekrar dene” ile gizlenmez; kullanıcıya ve denetime raporlanır.

## 6. Playbook kabulü

Bir playbook kataloğa girmeden önce:

1. Risk sınıfı ve onay gereksinimi belirlenmiş,
2. Ön koşullar ve kapsam yazılmış,
3. Post-check tanımlı,
4. Geri alma sınırı açık,
5. Pozitif/negatif/iptal/kısmi başarısızlık testleri geçmiş,
6. İmza ve kaynak doğrulaması (sürücü) yapılmış olmalıdır.
