$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$p = "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs"
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
Copy-Item $p "$p.bak-$ts" -Force

$c = Get-Content $p -Raw

# Sadece gercek metot tanimini ara
if ($c -match 'private\s+async\s+Task\s+ShowFindingsPopupAsync') {
    Write-Host "ShowFindingsPopupAsync tanimi zaten var" -ForegroundColor DarkGray
} else {
    $method = @'

    private async Task ShowFindingsPopupAsync(List<FindingItem> findings, string title)
    {
        if (findings.Count == 0) { AddBotMessage("Bulgu yok."); return; }

        var panel = new StackPanel { Spacing = 6 };
        foreach (var f in findings)
        {
            var row = new Grid { Padding = new Thickness(2, 4, 2, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Background = HexBrush(f.RiskBg),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            badge.Child = new TextBlock
            {
                Text = f.RiskLabel,
                Foreground = HexBrush(f.RiskFg),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };
            Grid.SetColumn(badge, 0);
            row.Children.Add(badge);

            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = f.Summary,
                Foreground = HexBrush("#c9d1d9"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                MaxWidth = 360
            });
            info.Children.Add(new TextBlock
            {
                Text = f.PlaybookText,
                Foreground = HexBrush("#484f58"),
                FontSize = 10
            });
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            var fixBtn = new Button
            {
                Content = "Duzelt",
                Padding = new Thickness(10, 4, 10, 4),
                Background = HexBrush("#238636"),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var capturedFinding = f;
            fixBtn.Click += async (s, ev) => await OnFixAsync(capturedFinding);
            Grid.SetColumn(fixBtn, 2);
            row.Children.Add(fixBtn);

            panel.Children.Add(row);
        }

        var dialog = new ContentDialog
        {
            Title = $"{title} ({findings.Count})",
            Content = new ScrollViewer { Content = panel, MaxHeight = 460 },
            CloseButtonText = "Kapat",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
'@

    $lastBrace = $c.LastIndexOf('}')
    if ($lastBrace -gt 0) {
        $c = $c.Substring(0, $lastBrace) + $method + $c.Substring($lastBrace)
        Set-Content $p -Value $c -Encoding UTF8 -NoNewline
        Write-Host "ShowFindingsPopupAsync tanimi eklendi" -ForegroundColor Green
    } else {
        Write-Host "Son } bulunamadi" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Kontrol:"
Select-String -Path $p -Pattern 'private async Task ShowFindingsPopupAsync|private async Task OnFixAsync' | ForEach-Object { "  $($_.LineNumber): $($_.Line.Trim())" }

Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan