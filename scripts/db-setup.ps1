[CmdletBinding()]
param(
    [string]$EnvFile = "supabase/.env.local",
    [switch]$Verify
)

$ErrorActionPreference = "Stop"

function Get-EnvValue {
    param([string]$Path, [string]$Key)
    if (-not (Test-Path -LiteralPath $Path)) { throw "Env dosyasi bulunamadi: $Path" }
    foreach ($line in Get-Content -LiteralPath $Path) {
        $t = $line.Trim()
        if ($t -eq "" -or $t.StartsWith("#")) { continue }
        $i = $t.IndexOf("=")
        if ($i -lt 1) { continue }
        $k = $t.Substring(0, $i).Trim()
        $v = $t.Substring($i + 1).Trim().Trim('"').Trim("'")
        if ($k -eq $Key) { return $v }
    }
    return $null
}

$dbUrl = Get-EnvValue -Path $EnvFile -Key "YAPAIR_SUPABASE_DB_URL"
if (-not $dbUrl) { throw "YAPAIR_SUPABASE_DB_URL tanimli degil ($EnvFile)" }

if ($dbUrl -notmatch "sslmode=") {
    $sep = if ($dbUrl.Contains("?")) { "&" } else { "?" }
    $dbUrl = "$dbUrl${sep}sslmode=require"
}
if ($dbUrl -match ":6543/") {
    Write-Warning "URL transaction pooler (6543). DDL icin 5432 onerilir."
}

$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql) {
    throw "psql bulunamadi. Kurulum: winget install PostgreSQL.PostgreSQL.16"
}

$migrationsDir = "supabase/migrations"
if (-not (Test-Path -LiteralPath $migrationsDir)) { throw "Migration klasoru yok: $migrationsDir" }
$files = Get-ChildItem -LiteralPath $migrationsDir -Filter "*.sql" | Sort-Object Name
if ($files.Count -eq 0) { throw "Uygulanacak migration yok" }

Write-Host "Uygulanacak migration: $($files.Count) dosya" -ForegroundColor Cyan
foreach ($f in $files) {
    Write-Host "-> $($f.Name)" -ForegroundColor Yellow
    & $psql.Source --dbname=$dbUrl --set=ON_ERROR_STOP=1 --single-transaction --file="$($f.FullName)"
    if ($LASTEXITCODE -ne 0) { throw "Migration basarisiz: $($f.Name)" }
}
Write-Host "OK: Migrationlar uygulandi" -ForegroundColor Green

if ($Verify) {
    $verifySql = @"
select table_schema, table_name
from information_schema.tables
where table_schema = 'public'
order by table_name;

select relname as tablo, relrowsecurity as rls_acik, relforcerowsecurity as rls_zorunlu
from pg_class
where relnamespace = 'public'::regnamespace and relkind = 'r'
order by relname;
"@
    $tmp = New-TemporaryFile
    try {
        Set-Content -LiteralPath $tmp.FullName -Value $verifySql -Encoding UTF8
        & $psql.Source --dbname=$dbUrl --set=ON_ERROR_STOP=1 --file="$($tmp.FullName)"
        if ($LASTEXITCODE -ne 0) { throw "Dogrulama sorgusu basarisiz" }
    } finally {
        Remove-Item -LiteralPath $tmp.FullName -ErrorAction SilentlyContinue
    }
}
