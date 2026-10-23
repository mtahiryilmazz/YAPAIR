# Doğrulama ve Sonuç Döngüsü

Bu belge, kullanıcının “işlem sonunda sorunun çözülüp çözülmediği sorulsun; onay butonu olsun; çözülmediyse yeniden sorgulansın; ajan kendi testini yapıp onaylasın” gereksinimini teknik akışa dönüştürür.

## 1. İlke

Bir onarımın başarılı olduğu:

- komutun `exit code = 0` dönmesiyle,
- ajanın “yaptım” demesiyle,
- yalnızca kullanıcının “çözüldü” demesiyle

tek başına belirlenmez. Başarı; **otomatik post-check + kullanıcı onayı** birlikte değerlendirilerek belirlenir. Çelişki varsa sonuç doğrulanmış başarı değildir.

## 2. Akış

```mermaid
sequenceDiagram
    actor U as Kullanıcı
    participant A as Ajan
    participant P as Politika
    participant T as Araç
    participant V as Doğrulama

    U->>A: Sorunu anlatır
    A->>A: Kanıt toplar, bulgu üretir
    A->>U: Teşhis + öneri + risk
    U->>P: Onay / iptal
    P->>T: Onaylanan R1/R2 aracı
    T->>V: Pre-check + yürütme
    V->>V: Post-check (servis, kuyruk, IPP, olay)
    V->>U: "Sorun çözüldü mü?" [Evet] [Hayır] [Bilmiyorum]
    alt Evet + otomatik temiz
        V-->>U: Doğrulanmış başarı
    else Evet + otomatik sorun sürüyor
        V-->>U: Çelişki; başarı sayılmaz
        V->>A: Yeniden tanılama
    else Hayır
        V->>A: Vaka yeniden açılır, yeni kanıt
    else Bilmiyorum
        V-->>U: Doğrulanamadı; ek kanıt/test önerisi
    end
```

## 3. Arayüz onay kartı

Onarım sonrası modal kart:

```text
┌──────────────────────────────────────────────────────────┐
│  Sorun çözüldü mü?                                       │
│                                                          │
│  Yazdırma Biriktiricisi yeniden başlatıldı.              │
│  Otomatik kontrol: Spooler çalışıyor, kuyruk okunabiliyor│
│  ancak fiziksel çıktı yazılımla doğrulanamıyor.          │
│                                                          │
│  [ Evet, çözüldü ]  [ Hayır, devam ediyor ]  [ Emin değilim ] │
└──────────────────────────────────────────────────────────┘
```

Kurallar:

- Butonlar klavye ile erişilebilir olmalı; odak “Evet”te değil, tarafsız bir konumda başlamalı.
- Otomatik kapanma olmamalı; kullanıcı seçim yapmalı.
- “Evet” seçilse bile otomatik kontrol sorun gösteriyorsa sonuç **çelişkili** olarak gösterilir.
- Seçim, zaman damgası ve otomatik kanıt özetiyle birlikte denetim kaydına yazılır.
- “Hayır” seçilirse “Farklı bir neden olabilir; yeniden tanılama yapılsın mı?” ikinci onayı gösterilir.
- Yeniden tanılama sayısı sınırlıdır (varsayılan 2, en fazla 5). Sınıra ulaşınca güvenli duruş ve uzman eskalasyonu.

## 4. Sonuç türleri

| Sonuç | Anlam | Kullanıcıya gösterim |
|---|---|---|
| `resolved` | Otomatik kontrol temiz; kullanıcı da onayladı | “Sorun çözüldü” |
| `not_resolved` | Sinyal sürüyor veya kullanıcı “hayır” dedi | “Sorun sürüyor; farklı neden olabilir” |
| `unknown` | Kanıt yok / kullanıcı emin değil | “Doğrulanamadı” |
| `contradicted` | Kullanıcı “çözüldü” dedi, otomatik kontrol sorun görüyor | “Doğrulanamadı; otomatik kontrol sorun görüyor” |

## 5. Otomatik testler

Onarım türüne göre post-check:

- **Hizmet:** durum `Running`, bağımlılıklar sağlıklı, ilgili işlev tekrar denenir.
- **Spooler:** servis `Running`, kuyruk okunabilir, yeni test işi kuyruğa girebiliyor.
- **Yazıcı:** `Win32_Printer` hata durumu temiz, IPP `printer-state` normal, `printer-state-reasons` yok.
- **Aygıt/sürücü:** `ConfigManagerErrorCode = 0`, PnP durumu `OK`, cihaz işlevi çalışıyor.
- **Ağ:** bağdaştırıcı `Up`, gateway/DNS tanımlı; istenirse kontrollü bağlantı testi.
- **Depolama:** boş alan eşiği, disk durumu.
- **Windows Update:** bekleyen reboot bayrağı, hizmet durumu.

Fiziksel sonuç (kâğıt çıktısı, toner, jam) yazılımla kanıtlanamıyorsa otomatik test “bağlantı/cihaz durumu doğrulandı, fiziksel çıktı teyit edilemedi” der ve kullanıcı onayına başvurur.

## 6. Yeniden tanılama

“Hayır” veya çelişki durumunda:

1. Vaka `Reopened` durumuna geçer.
2. Önceki bulgular “çözülmedi” olarak işaretlenir; yeni kanıt toplanır.
3. Farklı alanlar taranır (ör. yazıcı sorunu sanılırken ağ/port/sürücü/güvenlik politikası).
4. Yeni bulgular öncekiyle karşılaştırılır; tekrar eden playbook önerilmez.
5. En fazla `MaxAttempts` tur; sonrasında “uzman incelemesi gerekli” raporu.

Yeniden tanılama, aynı aracı körlemesine tekrar çalıştırmak değildir. Her turda yeni kanıt ve farklı hipotez gerekir.

## 7. CLI karşılığı

```powershell
# 1) Başlangıç raporu
yapair discover --pretty --output once.json

# 2) Sorun giderildikten sonra doğrula; butonlar yerine konsol onayı
yapair verify --baseline once.json --ask

# 3) JSON sonuç
yapair verify --baseline once.json --format json --output sonuc.json
```

- `--ask`: “Sorun çözüldü mü?” sorusunu sorar (`e/h/b`).
- `--max-attempts`: sorun sürerse kaç kez yeniden tanılama yapılacağı.
- Çıkış kodları: `0` çözüldü, `2` bilinmiyor, `3` çözülmedi/çelişkili, `4` hata.

JSON sözleşmesi `VerificationResult` v1.0'dır: `outcome`, `user_confirmation`, `baseline_signals`, `persisting_signals`, `new_signals`, `unverifiable_signals`, `details`, `attempts`.

## 8. Test senaryoları

| Senaryo | Beklenen |
|---|---|
| Baseline sorunlu, güncel temiz, kullanıcı evet | `resolved` |
| Baseline sorunlu, güncel sorunlu, kullanıcı evet | `contradicted` |
| Baseline sorunlu, güncel temiz, kullanıcı hayır | `not_resolved` + yeniden tanılama |
| Baseline sorunlu, güncel kaynak okunamıyor | `unknown` (asla resolved değil) |
| Baseline temiz | `unknown` (“doğrulanacak sorun yok”) |
| Yeniden tanılama sınırı | güvenli duruş, eskalasyon |

## 9. Arayüz sözleşmesi

Çekirdek, arayüzden bağımsızdır:

```csharp
public interface IVerificationPrompt
{
    Task<UserConfirmation> AskOutcomeAsync(VerificationResult result, CancellationToken ct);
    Task<bool> AskReDiagnoseAsync(VerificationResult result, CancellationToken ct);
}
```

WinUI 3 uygulaması bu arayüzü butonlarla uygular; CLI `ConsoleVerificationPrompt` ile uygular. Testler `FakePrompt` ile uygular. Böylece doğrulama mantığı UI değişse de aynı kalır.

## 10. Sınırlamalar

- Onarım yürütücüsü henüz yok; doğrulama şu an tanılama farkını ölçer.
- Fiziksel dünya (kâğıt, toner, kablo) yazılımla tam doğrulanamaz.
- IPP yalnızca seçili yazıcıda ve erişilebilir olduğunda kanıt üretir.
- Kullanıcı onayı özneldir; otomatik kanıtla çeliştiğinde üstün sayılmaz.
