# Supabase Kurulumu

## 1. Proje oluşturma

1. [supabase.com/dashboard](https://supabase.com/dashboard) → **New project**.
2. Organization: kişisel hesabın.
3. Name: `yapair` (veya `yapair-prod`).
4. Database Password: güçlü bir parola üret ve **password manager’a kaydet**.
5. Region: Türkiye’ye en yakın bölge — önerim **Frankfurt (eu-central-1)**.
6. Plan: Free ile başla; 100 PC için yeterli.
7. **Create new project** → 1-2 dakika bekle.

GitHub ile giriş yapmış olman proje oluşturmayı değiştirmez; GitHub sadece oturum açma yöntemidir.

## 2. Bilgileri al

Supabase panelinde:

- **Project Settings → API**
  - Project URL
  - `anon` public key
- **Project Settings → Database**
  - Connection string → URI (pooler)
  - Database password (kaydettiğin parola)

Bu bilgileri bana **sohbetten gönderme**. Yerelde `supabase/.env.local` dosyasına koy:

```text
YAPAIR_SUPABASE_URL=https://<project-ref>.supabase.co
YAPAIR_SUPABASE_ANON_KEY=<anon-key>
YAPAIR_SUPABASE_DB_URL=postgresql://postgres.<ref>:<password>@<host>:6543/postgres
YAPAIR_SUPABASE_SERVICE_ROLE_KEY=<service-role-key>   # yalnızca sunucu tarafı
```

> `service_role` anahtarı asla masaüstü istemcisine konmaz. Sync API sunucusu kullanır.

## 3. Şemayı uygula

En kolay yol:

1. Supabase panelinde **SQL Editor**.
2. [`migrations/20261023130000_init.sql`](migrations/20261023130000_init.sql) içeriğini yapıştır.
3. **Run**.

Alternatif: Supabase CLI.

```powershell
supabase login
supabase link --project-ref <project-ref>
supabase db push
```

## 4. RLS ve erişim

- `machines` ve `cases` tablolarında RLS açıktır.
- `anon`/`authenticated` doğrudan erişemez.
- Sync API, `service_role` ile sunucu tarafında yazar.
- Destek paneli için ileride ayrı `yapair_support` rolü ve SELECT policy eklenir.

## 5. Sıradaki adım

Proje oluştuktan sonra bana şunları söyle:

- Project URL (gizli değil)
- Region
- Project ref

Parola/anahtar gönderme. Ben yerel `outbox` + ASP.NET Core sync API iskeletini hazırlarım. Tarayıcına erişip tıklama yapamam; hesap açma ve proje oluşturma adımını senin yapman gerekiyor.
