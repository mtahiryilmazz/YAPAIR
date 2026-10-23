# Playbook Yazım Kılavuzu

## 1. Playbook nedir?

Playbook; tek bir sorun sınıfı için ön koşulları, sabit araç adımlarını, post-check'leri ve geri alma sınırını tanımlayan sürümlenmiş bir sözleşmedir. Serbest metin komut dosyası değildir.

## 2. Zorunlu alanlar

```csharp
new RepairPlaybook
{
    Id = "domain.action.v1",
    Title = "Kısa kullanıcı başlığı",
    Domain = HealthDomain.Printers,
    Risk = PlaybookRisk.R1,
    RequiresApproval = true,
    Summary = "Ne yapar, etkisi ne, neyi yapmaz.",
    Steps = ["tool.id", "tool.id:parameter"],
    Preconditions = ["Kanıt", "Kullanıcı onayı"],
    PostChecks = ["Gözlenebilir sonuç"],
    Rollback = "Geri alma sınırı veya 'Değişiklik yok.'",
}
```

## 3. Kurallar

1. **Tek sorumluluk:** Bir playbook tek bir kök nedeni hedefler.
2. **Sabit araç:** Adımlar katalogdaki araç kimlikleridir; serbest shell yoktur.
3. **Şemalı parametre:** Dosya yolu, registry yolu, URL veya serbest metin parametresi olamaz. Yalnızca enum, katalog kimliği ve doğrulanmış cihaz kimliği.
4. **Ön koşul:** Playbook, kanıt ve sistem durumu doğrulanmadan çalışmaz.
5. **Post-check:** En az bir gözlenebilir sonuç; “komut çalıştı” yeterli değildir.
6. **Geri alma:** Tam geri alma yoksa açıkça yazılır; arayüzde “geri alınabilir” gösterilmez.
7. **Risk:** R0-R3 sınıfı ve onay gereksinimi zorunludur. UAC/firewall/grup ilkesi gevşetmesi R3'tür.
8. **Idempotency:** Aynı playbook iki kez çalıştırıldığında güvenli olmalı veya ikinci çalışma reddedilmeli.
9. **Sürüm:** `v1`, `v2`; eski sürümler denetim kaydında korunur.
10. **Test:** Pozitif, negatif, iptal, kısmi başarısızlık ve timeout senaryoları.

## 4. Örnek: Spooler yeniden başlatma

```text
id: printers.spooler.restart.v1
risk: R1
approval: required
preconditions:
  - Spooler hizmeti mevcut
  - Aktif işlerin kesileceği kullanıcıya açıklandı
steps:
  - services.inspect:Spooler
  - services.restart:Spooler
  - printers.inspect_queue
post_checks:
  - Spooler durumu Running
  - Kuyruk yeniden okunabiliyor
  - Yazıcı durumu hata bildirmiyor
rollback:
  - Önceki başlangıç türü kaydedildi
  - Yeniden başlatmanın yan etkileri tamamen geri alınamaz
```

## 5. Yasak örnekler

```text
# YANLIŞ
steps: ["powershell -Command ..."]
parameters: { "inf_path": "C:\\..." }
parameters: { "registry_path": "HKLM\\..." }
```

LLM'den gelen serbest komut, dosya yolu veya URL asla playbook adımına dönüşmez.

## 6. İnceleme kontrol listesi

- [ ] Risk ve onay doğru mu?
- [ ] Kanıt eşiği var mı?
- [ ] Kapsam (hangi yazıcı/servis/aygıt) net mi?
- [ ] Post-check gerçekten sorunu ölçüyor mu?
- [ ] Geri alma sınırı dürüst mü?
- [ ] Hata/timeout/iptal davranışı tanımlı mı?
- [ ] Denetim alanları ve kullanıcı açıklaması var mı?
- [ ] Testler geçti mi?
