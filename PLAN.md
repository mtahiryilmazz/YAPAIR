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

## Faz 2 - TAMAMLANDI (2026-10-26)

### 2.1 Publish
- [x] publish-all.ps1 calistir
- [x] .gitignore: publish/ ekle
- [x] install-service.ps1: publish/api kullan
- [x] Masaustu kisayollari test

### 2.2 Yetki Modu - Sadece Ikon
- [x] Chat penceresinin sag ustunde kucuk renkli yuvarlak rozet (R0-R3)
- [x] Sadece ikon, tooltip: Yetki modu: R1 - Onayli
- [x] Tiklayinca Ayarlar dialogu acilir
- [x] Alt toolbar daki metin/aciklama sadeletirildi

### 2.3 Audit + Makine Logu
Backend:
- [x] authorization_audit: action, target_id, ip_address sutunlari
- [x] GET /admin/audit?machine=&since=&action=
- [x] POST /admin/authorization/audit
- [x] Her Duzelt/Onayla ve mod degisimi icin audit satiri

Web panel:
- [x] Audit sekmesi (filtre + tablo + CSV disaktarma)

### 2.4 Minimal Pencere
- [x] Pencere: 420 x 560 varsayilan boyut
- [x] Sag alt kosede acilir
- [x] Sabitleme pini (TopMost toggle) sag ustte
- [x] Konum hafizasi: %LOCALAPPDATA%/YAPAIR/window-pos.json
- [x] Boyut sinirlari (min 360x400, max 800x900)

### 2.5 Ek Executorlar
- [x] audio.restart.v1 (R1)
- [x] printers.queue.clear.v1 (R1)
- [x] network.adapter.reset.v1 (R2)
- [x] services.controlled_restart.v1 (R1)

## Faz 3 - UZUN VADELI

### 3.1 Native Admin (Yapair.Admin)

- [x] WinUI 3 frameless, fluent, koyu (#0d1117)
- [x] Sol navigasyon: Genel Bakış / Yardım / Vakalar / Batch / Makineler / Audit / Araştırma
- [x] Canli yardim talepleri + in-app bildirim InfoBar
- [x] Uzak masaustu butonu (mstsc / RustDesk / AnyDesk)
- [x] Makine detay ve düzenleme (notes, owner, label)
- [x] Toplu islemler (arsivle, unarchive, sil, CSV)
- [x] Masaustune kisayol (install-admin-shortcut.ps1)

### 3.2 Bildirim Sistemi

- [x] AdminNotificationService periyodik talep takibi (10s)
- [x] Yeni yardim talebi -> admin anlık bildirim barı + "Talepleri Aç" yönlendirmesi
- [x] Yardım talebi durum güncelleme (in_progress, resolved, closed) + admin notu

### 3.3 Uzak Baglanti

- [x] mstsc /v:<ip> (Windows yerlesik RDP)
- [x] rustdesk://<id>
- [x] anydesk:<id>
- [x] Makine listesinde Uzak Bağlan dropdown butonu
- [x] Baglanti logu audit (remote_connect_rdp/rustdesk/anydesk)

### 3.4 Kimlik + Coklu Kullanici

- [x] Supabase Auth & RBAC (UserRole: Admin, Technician, ReadOnly)
- [x] Roller: admin / teknisyen / salt-okunur yetki matrisi denetimi
- [x] R2/R3 onarim icin 2. onay (Dual Authorization, ApprovalManager, PIN dogrulama)
- [x] API key rotasyonu (ApiKeyManager, SHA-256 hash, rotate, revoke, %LOCALAPPDATA%/YAPAIR/api-keys.json)

### 3.5 Uzaktan Mudahale ve Admin Entegrasyonu
- [x] Entegre EmbeddedApiServer ve baslik cubugu Sunucu Yonetim Menusu (Baslat/Durdur/Yeniden Baslat/Seed)
- [x] Web arayuzu butonu (http://localhost:5080/admin/index.html tek tikla acilis)
- [x] Cihaza uzaktan playbook gonderme (POST /admin/remote-repair/dispatch)
- [x] Desktop ajani periyodik uzaktan komut dinleme ve yerel executor calistirma (GET /api/v1/remote-repair/pending, POST /api/v1/remote-repair/result)
- [x] Frameless pencere baslik cubugu yerel caption buton cakismasinin (cift buton / ust uste binme) giderilmesi
- [x] Desktop yetki modu seciminin gorunur ComboBox yapilmasi (Varsayilan R0 - Salt Okunur)
- [x] Sistem tepsisi (Tray) kapata tiklandiginda uygulamanin sonlanmama hatasinin (IsExplicitExit + ForceClose + Environment.Exit) cozumu
- [x] Vaka ve makine listelerinde Tumunu Sec, toplu silme, guvenli XamlRoot ile detay modal ve yazdirma
- [x] Canli sandbox entegrasyon testi (test-sandbox-api.ps1) ve 69/69 gecen birim testler

### 3.6 Dagitim
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

