$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$p = "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs"
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
Copy-Item $p "$p.bak-$ts" -Force
$c = Get-Content $p -Raw

# 1) Constructor'daki ModeCombo referansini kaldir
$c = $c -replace '\s*ModeCombo\.SelectedIndex = \(int\)_mode;', ''

# 2) UpdateModeBadge metodunu bosalt (XAML'de badge yok)
$oldMethod = '(?s)private void UpdateModeBadge\(\)\s*\{.*?\n    \}'
$newMethod = @'
private void UpdateModeBadge()
    {
        // Yetki modu artik Ayarlar dialog'unda gosteriliyor - ana pencerede badge yok
    }
'@
$c = $c -replace $oldMethod, $newMethod

Set-Content $p -Value $c -Encoding UTF8 -NoNewline

Write-Host "ModeCombo/ModeBadge referanslari temizlendi" -ForegroundColor Green
Write-Host ""
Write-Host "Kontrol:"
Select-String -Path $p -Pattern 'ModeCombo|ModeBadge' | ForEach-Object { "  $($_.LineNumber): $($_.Line.Trim())" }

if (-not (Select-String -Path $p -Pattern 'ModeCombo|ModeBadge')) {
    Write-Host "  Temiz!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan