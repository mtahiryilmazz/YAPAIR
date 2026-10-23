# Test Stratejisi

## 1. Katmanlar

1. **Birim testleri:** bulgu kuralları, risk sınıflandırma, redaksiyon, şema, doğrulama karşılaştırması.
2. **Sözleşme testleri:** JSON şeması, snake_case alanlar, enum değerleri, `snapshot_id` deseni.
3. **Fixture testleri:** gerçek cihazlardan kişisel veriler temizlenerek alınmış Windows/Event Log/IPP örnekleri.
4. **Entegrasyon testleri:** gerçek WMI, ServiceController, Event Log, PnP, `NetworkInterface`.
5. **Güvenlik testleri:** path traversal, sahte IPC istemcisi, replay, bozuk JSON, prompt injection, imzasız sürücü, onaysız R2/R3.
6. **Regresyon:** çözülmüş vaka seti her sürümde çalıştırılır.
7. **Kullanılabilirlik:** Türkçe doğal dil varyasyonları ve teknik olmayan kullanıcılarla görev testleri.

## 2. Mevcut otomatik testler

| Proje | Kapsam |
|---|---|
| `Yapair.Core.Tests` | Şema, redaksiyon, bulgu doğrulama, durum toplama, kullanıcı onayı çelişkisi, yeniden tanılama |
| `Yapair.Discovery.Windows.Tests` | Gerçek salt okunur keşif, Spooler invariant, IPP istek/yanıt ayrıştırma, hata yolları |
| `Yapair.Cli.Tests` | Yardım, şema, playbook kataloğu, JSON çıktı + şema, atomik dosya, hatalı baseline |

Çalıştırma:

```powershell
dotnet test Yapair.slnx
```

## 3. Entegrasyon testi ilkeleri

- Testler yalnızca okuma yapar; hiçbir onarım aracı çağırmaz.
- Spooler durumu test öncesi/sonrası karşılaştırılır.
- Gerçek kullanıcı belgeleri kullanılmaz; sentetik belge adları kullanılır.
- Testler izole VM veya ayrılmış laboratuvarda çalıştırılmalıdır.
- Canlı yazıcı IPP testi ağ erişimine bağlıdır; CI'da zorunlu değildir.
- Başarısız test gizlenmez; mock veri gerçek sistem çıktısı gibi sunulmaz.

## 4. Doğrulama testleri

| Senaryo | Beklenen |
|---|---|
| Baseline sorunlu, güncel temiz | `resolved` |
| Baseline sorunlu, güncel sorunlu | `not_resolved` |
| Güncel kaynak okunamıyor | `unknown` |
| Kullanıcı “evet”, otomatik sorunlu | `contradicted` |
| Kullanıcı “hayır”, otomatik temiz | `not_resolved` |
| Yeniden tanılama | yeni kanıt; max attempts |
| Kapsam dışı yazıcı bulgusu | seçili kapsamı etkilemez |

## 5. Güvenlik testleri (yol haritası)

- LLM çıktısında serbest komut/registry yolu/dosya yolu reddi.
- Prompt injection içeren web metni ile araç çağrısı denemesi.
- IPC'ye yetkisiz SID ve replay isteği.
- R2/R3 işlemi onaysız çalıştırma denemesi.
- Bozuk/eksik JSON ve şema dışı alanlar.
- İmzasız/yanlış mimari sürücü paketi.
- Timeout ve servis çökmesi sırasında güvenli duruş.

## 6. Kabul kapıları

Bir sürüm, aşağıdakilerin tümü sağlanmadan yayınlanmaz:

- CLI desteklenen Windows'ta çalışıyor.
- Salt okunur keşif gerçek API'lerden veri topluyor.
- Kaynak erişilemediğinde `unknown`/`failed` doğru gösteriliyor.
- JSON şeması ve redaksiyon testleri geçiyor.
- Onarım yürütücüsü varsa her araç için pre/post-check ve onay testi var.
- Çalıştırılan testler ve kalan eksikler raporlanıyor.

## 7. Laboratuvar

- Hyper-V checkpoint'leri.
- `snmpsim` ve IPP sanal yazıcı.
- Sentetik olay senaryoları.
- Bozuk Spooler/kuyruk enjektörü (yalnızca izole VM).
- Farklı Windows 10/11 sürümleri ve dil paketleri.

Sanallaştırma, uygulama düzeyindeki geri alma ve veri koruma gereksinimlerinin yerine geçmez.
