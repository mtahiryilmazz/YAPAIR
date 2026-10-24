$ErrorActionPreference = "Stop"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Bu script YONETICI olarak calistirilmalidir!" -ForegroundColor Red; exit 1 }

$repo = "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"
$dll = "$repo\src\Yapair.Api\bin\Debug\net10.0\Yapair.Api.dll"
$contentRoot = "$repo\src\Yapair.Api"
if (-not (Test-Path $dll)) { Write-Host "Once build: dotnet build src\Yapair.Api\Yapair.Api.csproj" -ForegroundColor Red; exit 1 }

$dotnetExe = (Get-Command dotnet).Source
$binPath = "`"$dotnetExe`" exec `"$dll`" --contentRoot `"$contentRoot`" --urls http://localhost:5080"

if (Get-Service -Name "YapairApi" -ErrorAction SilentlyContinue) {
    Write-Host "Mevcut servis kaldiriliyor..." -ForegroundColor Yellow
    Stop-Service -Name "YapairApi" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete YapairApi | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Servis olusturuluyor: YapairApi" -ForegroundColor Green
# sc.exe ciktisini yakala, boylece hata olursa goruruz
$result = sc.exe create YapairApi binPath= $binPath start= auto DisplayName= "YAPAIR API" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "sc.exe create basarisiz oldu:" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    Write-Host ""
    Write-Host "Ipucu: binPath sunun gibi gorunuyor:" -ForegroundColor Yellow
    Write-Host $binPath -ForegroundColor Yellow
    exit 1
}

sc.exe description YapairApi "YAPAIR Windows sorun giderici - yerel API + admin panel" | Out-Null
sc.exe failure YapairApi reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

Write-Host "Servis baslatiliyor..." -ForegroundColor Green
Start-Service -Name "YapairApi"
Start-Sleep -Seconds 3

$status = Get-Service -Name "YapairApi"
Write-Host "Servis durumu: $($status.Status)" -ForegroundColor Green
Write-Host ""
try {
    $r = Invoke-RestMethod "http://localhost:5080/health" -TimeoutSec 5
    Write-Host "Health: $($r.status) - $($r.service) v$($r.version)" -ForegroundColor Green
} catch {
    Write-Host "Health check basarisiz: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Event Log: Get-EventLog -LogName Application -Source YapairApi -Newest 5" -ForegroundColor Yellow
}
