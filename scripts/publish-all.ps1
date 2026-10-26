$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$repo\Yapair.slnx")) {
    $repo = $PSScriptRoot
}

Write-Host "=== YAPAIR Tum Projeleri Yayinlama (Publish) ===" -ForegroundColor Cyan
Write-Host "Kok Dizin: $repo" -ForegroundColor DarkGray

$cliOut = Join-Path $repo "publish\cli"
$apiOut = Join-Path $repo "publish\api"
$desktopOut = Join-Path $repo "publish\desktop"

Write-Host ""
Write-Host "1/3 Yapair.Cli yayinlaniyor..." -ForegroundColor Yellow
dotnet publish "$repo\src\Yapair.Cli\Yapair.Cli.csproj" -c Release -o $cliOut --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "CLI publish basarisiz!" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "2/3 Yapair.Api yayinlaniyor..." -ForegroundColor Yellow
dotnet publish "$repo\src\Yapair.Api\Yapair.Api.csproj" -c Release -o $apiOut --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "API publish basarisiz!" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "3/4 Yapair.Desktop.WinUI yayinlaniyor..." -ForegroundColor Yellow
dotnet publish "$repo\src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -c Release -p:Platform=x64 -o $desktopOut --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "Desktop.WinUI publish basarisiz!" -ForegroundColor Red; exit 1 }

$adminOut = Join-Path $repo "publish\admin"
Write-Host ""
Write-Host "4/4 Yapair.Admin yayinlaniyor..." -ForegroundColor Yellow
dotnet publish "$repo\src\Yapair.Admin\Yapair.Admin.csproj" -c Release -p:Platform=x64 -o $adminOut --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "Admin publish basarisiz!" -ForegroundColor Red; exit 1 }

# wwwroot kopyalama (Admin Web Arayüzü)
$wwwrootSrc = Join-Path $repo "src\Yapair.Api\wwwroot"
if (Test-Path $wwwrootSrc) {
    Copy-Item -Path $wwwrootSrc -Destination "$adminOut\wwwroot" -Recurse -Force
    Write-Host "wwwroot web arayuzu publish/admin/wwwroot klasorune kopyalandi." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Yayinlama Basariyla Tamamlandi ===" -ForegroundColor Green
Write-Host "  CLI:     $cliOut" -ForegroundColor DarkGray
Write-Host "  API:     $apiOut" -ForegroundColor DarkGray
Write-Host "  Desktop: $desktopOut" -ForegroundColor DarkGray
Write-Host "  Admin:   $adminOut" -ForegroundColor DarkGray
