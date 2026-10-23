namespace Yapair.Cli;

internal static class CliHelp
{
    public const string Text = """
        YAPAIR — Windows sistem tanılama ve doğrulama (salt okunur)

        Kullanım:
          yapair discover [seçenekler]     Tanılama raporu üretir (varsayılan komut).
          yapair verify --baseline <json>  Önceki tanılamayla karşılaştırıp sorunun çözülüp çözülmediğini doğrular.
          yapair playbooks                 Salt okunur onarım playbook kataloğunu listeler.
          yapair schema                    Sürümlenmiş JSON şemasını yazdırır.
          yapair version                   Sürüm bilgisini yazdırır.
          yapair help                      Bu yardımı gösterir.

        Seçenekler:
          --format json|summary            Çıktı biçimi (varsayılan: json).
          --output <dosya>                 Raporu dosyaya yazar; stdout'a yazmaz.
          --pretty                         JSON çıktısını girintili üretir.
          --timeout-ms <ms>                Kaynak başına zaman aşımı (1000-120000, varsayılan 15000).
          --events <adet>                  En fazla olay sayısı (0-500, varsayılan 25).
          --lookback-hours <saat>          Olay günlüğü geriye bakış süresi (1-168, varsayılan 24).
          --ipp <yazıcı adı>               Yalnızca seçilen yazıcı için IPP durum sorgusu yapar.
          --ipp-path <yol>                 IPP yolu (varsayılan /ipp/print).
          --ipp-insecure-tls               IPPS'e yönlenen yerel yazıcının kendinden imzalı sertifikasını kabul eder.
          --show-network-addresses         IPv4 adreslerini maskelemeden gösterir (yerel hata ayıklama).
          --include-document-names         Belge adlarını gizlemez (kişisel veri; yalnızca yerel hata ayıklama).
          --log <dosya>                    Yapılandırılmış JSONL işlem günlüğü yazar.
          --verbose                        İlerleme bilgisini stderr'e yazar.
          -h, --help                       Bu yardımı gösterir.
          --version                        Sürüm bilgisini yazdırır.

        Doğrulama seçenekleri:
          --baseline <json>                Karşılaştırılacak önceki tanılama raporu.
          --printer <ad>                   Doğrulanacak yazıcı (varsayılan: rapordaki varsayılan yazıcı).
          --ask                            "Sorun çözüldü mü?" onayını etkileşimli sorar.
          --max-attempts <1-5>             Sorun sürerse en fazla kaç kez yeniden tanılama yapılacağı.

        Çıkış kodları:
          0  Başarılı / sorun çözüldü
          2  Kısmi, bilinmeyen veya doğrulanamayan sonuç
          3  Tanılama başarısız / sorun çözülmedi
          4  Şema veya iç hata
          64 Kullanım hatası

        Gizlilik:
          Belge adları varsayılan olarak <redacted-document> ile değiştirilir.
          IPv4 adresleri /24 maskelenir; MAC, e-posta ve kullanıcı profil adları temizlenir.
          Bilgisayar adı, kullanıcı adı, alan adı, belge içeriği ve seri numarası toplanmaz.

        Örnekler:
          yapair discover --pretty --output rapor.json
          yapair discover --ipp "Kyocera TASKalfa MZ2501ci KX" --format summary
          yapair discover --events 50 --lookback-hours 48 --timeout-ms 8000
          yapair discover --ipp "Kyocera TASKalfa MZ2501ci KX" --ipp-insecure-tls --output once.json
          yapair verify --baseline once.json --ask
        """;
}
