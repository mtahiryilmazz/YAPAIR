namespace Yapair.Cli;

internal static class CliHelp
{
    public const string Text = """
        YAPAIR — Windows sistem tanılama ve doğrulama (salt okunur)

        Kullanım:
          yapair discover [seçenekler]     Tanılama raporu üretir (varsayılan komut).
          yapair verify --baseline <json>  Önceki tanılamayla karşılaştırıp sorunun çözülüp çözülmediğini doğrular.
          yapair cases add --text "..."    Kullanıcı arızasını yerel vakaya dönüştürür.
          yapair cases import --file ...   JSONL dosyasındaki vakaları içe aktarır.
          yapair cases cluster             Benzer arızaları aile/alt tür olarak gruplar.
          yapair cases list                Son vakaları listeler.
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

        Vaka veritabanı seçenekleri:
          --db <dosya>                     SQLite veritabanı (varsayılan: %LOCALAPPDATA%\YAPAIR\cases.db).
          --text <metin>                   Eklenecek kullanıcı arıza metni.
          --file <jsonl>                   İçe aktarılacak JSONL dosyası (her satırda {"text":"..."}).
          --store-raw                      Ham kullanıcı metnini de sakla (varsayılan: yalnızca normalize metin).
          --limit <1-1000>                 Listelenecek vaka sayısı.
          --threshold <0.1-0.9>            Bilinmeyen metinler için benzerlik eşiği.
          --case <case-id>                 Sonuç güncellenecek vaka.
          --outcome <resolved|not_resolved|unknown|contradicted>

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
          yapair cases import --file data\sample-complaints.jsonl
          yapair cases cluster
        """;

    public const string CasesText = """
        YAPAIR Vaka Veritabanı (yerel SQLite)

        Kullanım:
          yapair cases add --text "yazıcı çevrimdışı" [--store-raw] [--db dosya]
          yapair cases import --file vakalar.jsonl [--store-raw] [--db dosya]
          yapair cases list [--limit 50] [--format summary]
          yapair cases cluster [--threshold 0.35] [--format summary]
          yapair cases outcome --case <case-id> --outcome resolved

        Notlar:
          - Ham metin varsayılan olarak saklanmaz; --store-raw açık rıza gerektirir.
          - Normalizasyon, farklı cümleleri aile + semptom koduna eşler.
          - Bilinmeyen metinler token benzerliğiyle gruplanır.
          - Merkezî senkronizasyon henüz yoktur; bu sürüm yerel POC'dur.
        """;
}
