$ErrorActionPreference = "Stop"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Yonetici gerekli" -ForegroundColor Red; exit 1 }

if (-not (Get-Service -Name "YapairApi" -ErrorAction SilentlyContinue)) {
    Write-Host "YapairApi servisi zaten yok" -ForegroundColor Yellow; exit 0
}

Stop-Service -Name "YapairApi" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
sc.exe delete YapairApi | Out-Null
Write-Host "YapairApi servisi kaldirildi" -ForegroundColor Green
