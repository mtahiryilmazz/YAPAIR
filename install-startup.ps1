$ErrorActionPreference = "Stop"
$repo = $PSScriptRoot

$exe = $null
$candidates = @(
    "$repo\publish\desktop\Yapair.Desktop.WinUI.exe",
    "$repo\src\Yapair.Desktop.WinUI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Yapair.Desktop.WinUI.exe",
    "$repo\src\Yapair.Desktop.WinUI\bin\Release\net10.0-windows10.0.19041.0\win-x64\Yapair.Desktop.WinUI.exe"
)
foreach ($p in $candidates) { if (Test-Path $p) { $exe = $p; break } }

if (-not $exe) { Write-Host "WinUI exe bulunamadi. Once build/publish et." -ForegroundColor Red; exit 1 }

$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup "YAPAIR Sorun Giderici.lnk"

$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = Split-Path $exe
$sc.Description = "YAPAIR - Sorun Giderici"
$sc.Save()
Write-Host "Startup kisayolu: $lnk" -ForegroundColor Green
Write-Host "  Hedef: $exe" -ForegroundColor DarkGray
