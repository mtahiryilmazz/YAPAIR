$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$p = "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs"
$c = Get-Content $p -Raw

# Eski OtherBtn_Click metodunu sil
$c = $c -replace '(?s)\s+private async void OtherBtn_Click\(object sender, RoutedEventArgs e\)\s*\{.*?\n\s{4}\}', ''

# Yerine public ShowOtherIssuesAsync + OtherBtn_Click ekle (ShowFindingsPopupAsync oncesine)
$newMethods = @'

    private async void OtherBtn_Click(object sender, RoutedEventArgs e) => await ShowOtherIssuesAsync();

    public async Task ShowOtherIssuesAsync()
    {
        StatusText.Text = "Tum sorunlar araniyor...";
        try
        {
            var (exit, stdout, stderr) = await CliBridge.RunAsync(
                _cliDll, "discover --format json --pretty", TimeSpan.FromSeconds(60));
            if (string.IsNullOrWhiteSpace(stdout)) { AddBotMessage("Cikti yok"); return; }
            var findings = ParseFindings(stdout).ToList();
            await ShowFindingsPopupAsync(findings, "Tum Sorunlar");
        }
        catch (Exception ex) { AddBotMessage("Hata: " + ex.Message); }
        finally { StatusText.Text = ""; }
    }
'@

if ($c -notmatch 'public async Task ShowOtherIssuesAsync') {
    $c = $c -replace '(\s+private async Task ShowFindingsPopupAsync)', ($newMethods + '$1')
    Set-Content $p -Value $c -Encoding UTF8 -NoNewline
    Write-Host "ShowOtherIssuesAsync eklendi" -ForegroundColor Green
} else {
    Write-Host "Zaten var" -ForegroundColor DarkGray
}

# Kontrol
Select-String -Path $p -Pattern 'ShowOtherIssuesAsync|OtherBtn_Click' | ForEach-Object { "  $($_.LineNumber): $($_.Line.Trim())" }

# Build
Write-Host ""
Write-Host "Build ediliyor..." -ForegroundColor Yellow
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD BASARISIZ" -ForegroundColor Red
    exit 1
}
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan