# ADR 0001 — Ürün kapsamının genel Windows sorunlarına genişletilmesi

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-23
- **Karar veren:** Ürün sahibi

## Bağlam

İlk mimari (v2), yazıcı/spooler alanını MVP olarak tanımlıyordu. Kullanıcı, ürünün yalnızca yazıcı sorunları için düşünülmemesi gerektiğini; UAC, grup ilkesi, Denetim Masası, aygıtlar, donanım, ses sürücüleri ve Windows'ta karşılaşılabilecek diğer tüm sorunların kapsamda olduğunu belirtti.

## Karar

YAPAIR, **genel Windows tanılama ve onarım platformu** olarak konumlandırılır. Mimari alan modüllerine ayrılır:

- sistem, hizmetler, aygıtlar, yazıcılar, ses, ağ, güvenlik (UAC/firewall), depolama, Windows Update, olay günlükleri
- yol haritası: grup ilkesi/RSoP, Denetim Masası, sürücü kataloğu, performans

Yazıcı modülü ilk **derin dikey dilim** olarak korunur; ürün sınırı değildir.

## Sonuçlar

- `Finding` ve `HealthDomain` sözleşmeleri eklendi; bulgular alan bağımsız hale geldi.
- `SystemSnapshot` v1, `findings` ve `domains_checked` alanlarıyla genişletildi.
- Salt okunur modüller: hizmetler, güvenlik, ağ, depolama, sistem, Windows Update; PnP tüm hata bildiren aygıtları kapsayacak şekilde genişletildi.
- Doğrulama döngüsü artık bulgu tabanlı; yalnızca yazıcıya özgü değil.
- Onarım yürütücüsü hâlâ yok; güvenlik modeli (R0-R3, onay, post-check, LLM'siz çekirdek) korunur.
- Grup ilkesi ve Denetim Masası için ayrı modüller ve risk matrisleri gerekir; bir alandaki yetki diğerine taşınmaz.

## Alternatifler

- **Yalnızca yazıcı MVP'sinde kalmak:** Daha hızlı teslim, ancak kullanıcı gereksinimini karşılamaz.
- **Tüm alanları tek sürümde onarımla birlikte sunmak:** Güvenlik ve test yükü nedeniyle reddedildi.
- **Genel kapsam + salt okunur keşifle başlamak:** Seçilen yol; her alan kendi kanıt ve risk sözleşmesiyle büyür.
