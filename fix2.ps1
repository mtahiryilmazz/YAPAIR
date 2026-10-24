$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$p = "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs"
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
Copy-Item $p "$p.bak-$ts" -Force

$c = Get-Content $p -Raw

if ($c -match 'public async Task ShowOtherIssuesAsync') {
    Write-Host "Zaten var" -ForegroundColor DarkGray
} else {
    $method = "`r`n    public async Task ShowOtherIssuesAsync()`r`n    {`r`n        StatusText.Text = `"Tum sorunlar araniyor...`";`r`n        try`r`n        {`r`n            var (exit, stdout, stderr) = await CliBridge.RunAsync(`r`n                _cliDll, `"discover --format json --pretty`", TimeSpan.FromSeconds(60));`r`n            if (string.IsNullOrWhiteSpace(stdout)) { AddBotMessage(`"Cikti yok`"); return; }`r`n            var findings = ParseFindings(stdout).ToList();`r`n            await ShowFindingsPopupAsync(findings, `"Tum Sorunlar`");`r`n        }`r`n        catch (Exception ex) { AddBotMessage(`"Hata: `" + ex.Message); }`r`n        finally { StatusText.Text = `"`"; }`r`n    }`r`n"

    # Son } karakterini bul ve oncesine ekle
    $lastBrace = $c.LastIndexOf('}')
    if ($lastBrace -gt 0) {
        $c = $c.Substring(0, $lastBrace) + $method + $c.Substring($lastBrace)
        Set-Content $p -Value $c -Encoding UTF8 -NoNewline
        Write-Host "ShowOtherIssuesAsync son } oncesine eklendi" -ForegroundColor Green
    } else {
        Write-Host "Son } bulunamadi" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Select-String -Path $p -Pattern 'ShowOtherIssuesAsync' | ForEach-Object { "  $($_.LineNumber): $($_.Line.Trim())" }

Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan