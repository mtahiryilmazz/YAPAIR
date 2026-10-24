# YAPAIR - Yol Haritasi

> Son guncelleme: 2026-10-24  
> Durum: Faz 1 tamam, Faz 2 basliyor

## Ozet

Windows icin yapay zeka destekli sorun giderme platformu.

- Kullanici: WinUI 3 chat + tray
- Yonetici: Web panel (http://localhost:5080/admin)
- API: ASP.NET Core + Supabase
- AI: Gemini Flash-Lite + DuckDuckGo (ucretsiz)
- Onarim: Spooler restart (R1) + onay dialog
- Yetki: R0-R3 modu

## Faz 1 - TAMAMLANDI (2026-10-24)

### Uygulamalar (9 proje)

| Proje | Tur |
|---|---|
| Yapair.Core | Kutuphane |
| Yapair.Storage | Kutuphane |
| Yapair.Discovery.Windows | Kutuphane |
| Yapair.Sync | Kutuphane |
| Yapair.Cli | CLI |
| Yapair.Api | Web API |
| Yapair.Desktop.WinUI | WinUI 3 |
| Yapair.Launcher | WPF |

### Ozellikler

- [x] Chat arayuzu (frameless, koyu, fluent)
- [x] Sistem tepsisi (TaskbarIcon)
- [x] Ayarlar dialog (yetki modu + servis kur/kaldir)
- [x] Yetki modu R0-R3
- [x] Audit log (yerel + Supabase)
- [x] Coz butonu + Duzelt + onay dialogu
- [x] Spooler restart executor (R1)
- [x] Yardim Iste butonu
- [x] Admin panelde yardim talepleri
- [x] Research cache + kota
- [x] Gemini + DuckDuckGo
- [x] Intents (ag/yazici/ses/bluetooth/aygit/update/guvenlik)
- [x] Publish script
- [x] Masaustu kisayollari
- [x] Windows Service (YapairApi)
- [x] Startup (WinUI otomatik baslar)

## Faz 2 - SIRADAKI

### 2.1 Publish (tamamlanacak)

- [ ] publish-all.ps1 calistir
- [ ] .gitignore: publish/ ekle
- [ ] install-service.ps1: publish/api kullan
- [ ] Masaustu kisayollari test

### 2.2 Yetki Modu - Sadece Ikon

Chat penceresinin sag ustunde kucuk renkli yuvarlak rozet:

| Mod | Renk |
|---|---|
| R0 Salt Okunur | Mavi #58a6ff |
| R1 Onayli | Amber #d29922 |
| R2 Otomatik | Yesil #3fb950 |
| R3 Uzman | Kirmizi #f85149 |

- Sadece ikon, tooltip: Yetki modu: R1 - Onayli
- Tiklayinca Ayarlar dialogu acilir
- Alt toolbar daki metin/aciklama kaldirilir

### 2.3 Audit + Makine Logu

Backend:
- [ ] authorization_audit: action, target_id, ip_address sutunlari
- [ ] GET /admin/audit?machine=&since=&action=
- [ ] Her Duzelt/Onayla icin audit satiri

Web panel:
- [ ] Audit sekmesi (filtre + tablo + CSV)
- [ ] Makine bazli log paneli (timeline)

### 2.4 Minimal Pencere

Win11 toast boyutuna yaklas:

- [ ] Pencere: 420 x 560
- [ ] Tray sag alt kosede acilir
- [ ] Windows bildirim alani ile cakismaz
- [ ] Sabitleme pini (TopMost) sag ustte
- [ ] Tasima (baslik surukle)
- [ ] Konum: %LOCALAPPDATA%/YAPAIR/window-pos.json
- [ ] Kucultulebilir (min 360x400, max 800x900)

### 2.5 Ek Executorlar

- [ ] audio.restart.v1 (R1)
- [ ] printers.queue.clear.v1 (R1)
- [ ] network.adapter.reset.v1 (R2)
- [ ] services.controlled_restart.v1 (R1)

## Faz 3 - UZUN VADELI

### 3.1 Native Admin (Yapair.Admin)

- WinUI 3 frameless, fluent, koyu
- Sol navigasyon: Vakalar / Batch / Makineler / Yardim / Audit / Arastirma
- Canli yardim talepleri + toast bildirim
- Uzak masaustu butonu (mstsc / RustDesk / AnyDesk)
- Makine detay (notes, owner, timeline)
- Toplu islemler (arsivle, sil, CSV)
- Masaustune kisayol

### 3.2 Bildirim Sistemi

- [ ] Microsoft.Windows.AppNotifications
- [ ] Yeni yardim talebi -> admin toast
- [ ] Onarim tamamlandi -> kullanici toast
- [ ] Onarim basarisiz -> IT toast

### 3.3 Uzak Baglanti

- [ ] mstsc /v:<ip> (Windows yerlesik RDP)
- [ ] rustdesk://<id>
- [ ] anydesk:<id>
- [ ] Makine detayinda Baglan butonu
- [ ] Baglanti logu audit

### 3.4 Kimlik + Coklu Kullanici

- [ ] Supabase Auth
- [ ] Roller: admin / teknisyen / salt-okunur
- [ ] R2/R3 onarim icin 2. onay
- [ ] API key rotasyonu

### 3.5 Dagitim

- [ ] MSI/EXE installer (WiX veya MSIX)
- [ ] Auto-update (Squirrel)
- [ ] GPO toplu kurulum
- [ ] Silent install (--silent)

### 3.6 Surucu Yonetimi

- [ ] devices.driver.install.v1 (pnputil)
- [ ] Windows Update Catalog entegrasyonu
- [ ] Imza + hash dogrulama
- [ ] Rollback

### 3.7 Ag Islemleri

- [ ] network.ip.change.v1 (R2)
- [ ] network.dns.change.v1 (R2)
- [ ] network.diagnose.v1
- [ ] APIPA/gateway/DNS derin tanilama

### 3.8 AI Genisletme

- [ ] Embedding cache (semantic search)
- [ ] RAG (gecmis vakalardan ogrenme)
- [ ] Coklu model fallback
- [ ] Fine-tune (Turkce)
- [ ] Prompt versioning

### 3.9 Raporlama

- [ ] Haftalik/aylik PDF
- [ ] Trend analizi
- [ ] MTTR metrikleri
- [ ] SLA takibi

### 3.10 Test + CI

- [ ] Unit test coverage >= 80%
- [ ] Integration test
- [ ] Playwright UI test
- [ ] GitHub Actions CI/CD
- [ ] Otomatik release notes

## Mimari

Kullanici PC        Sunucu              IT

WinUI chat   -->   Yapair.Api   -->   Admin Panel
                    :5080              (web/native)
                     |
                     v
                  PostgreSQL
                  (Supabase)
                  - cases
                  - machines
                  - help_requests
                  - authorization_audit

## Yetki Matrisi

| Mod | Rozet | Otomatik | Onay |
|---|---|---|---|
| R0 Salt Okunur | Mavi | R0 | - |
| R1 Onayli | Amber | R0 | R1+ |
| R2 Otomatik | Yesil | R0/R1 | R2+ |
| R3 Tam Erisim | Kirmizi | R0/R1/R2 | R3 |

## Guvenlik

- service_role anahtari sadece API de
- API key .env.local, Git e girmez
- RLS her tabloda aktif
- Audit her mod degisiminde + her onarimda
- R3 onay dialogu zorunlu

## Performans Hedefleri

| Metrik | Hedef |
|---|---|
| Uygulama acilis | < 2 sn |
| Tarama (discover) | < 30 sn |
| Chat (cache) | < 100 ms |
| Chat (AI) | < 8 sn |
| Onarim | < 15 sn |
| Admin panel | < 1 sn |

## Zaman Cizelgesi

| Tarih | Faz | Odak |
|---|---|---|
| 2026-10-24 | Faz 1 | TAMAM |
| 2026-10-25 | Faz 2.1-2.2 | Publish + rozet |
| 2026-10-26 | Faz 2.3 | Audit + makine log |
| 2026-10-27 | Faz 2.4 | Minimal pencere |
| 2026-10-28 | Faz 2.5 | Ek executor |
| 2026-10-29 | Faz 3.1 | Native admin |
| 2026-11-01 | Faz 3.2-3.3 | Toast + RDP |
| 2026-11-05 | Faz 3.4-3.5 | Auth + installer |
| 2026-11-10 | Faz 3.6-3.10 | Surucu/ag/AI/CI |

## Baglantili Belgeler

- README.md - kurulum
- ARCHITECTURE.md - mimari
- docs/verification-loop.md - onay dongusu
- docs/risk-policy.md - R0-R3
- supabase/migrations/ - sema

