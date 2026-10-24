$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$ts = Get-Date -Format "yyyyMMdd-HHmmss"

# ============ 1) XAML: Settings butonu + toolbar sadeleştir ============
Write-Host "MainWindow.xaml guncelleniyor..." -ForegroundColor Cyan
$xamlPath = "src\Yapair.Desktop.WinUI\MainWindow.xaml"
Copy-Item $xamlPath "$xamlPath.bak-$ts" -Force
$xaml = Get-Content $xamlPath -Raw

# Title bar'a settings butonu ekle (CloseBtn'den once)
if ($xaml -notmatch 'SettingsBtn_Click') {
    $settingsBtn = @'
<Button Click="SettingsBtn_Click" Background="Transparent" BorderThickness="0"
                Width="46" Height="40" Padding="0" ToolTipService.ToolTip="Ayarlar">
          <FontIcon Glyph="&#xE713;" FontSize="14" Foreground="#c9d1d9"/>
        </Button>
        <Button Click="HideBtn_Click" Background="Transparent" BorderThickness="0"
'@
    $xaml = $xaml.Replace('<Button Click="HideBtn_Click" Background="Transparent" BorderThickness="0"', $settingsBtn)
    Write-Host "  + Settings butonu eklendi" -ForegroundColor Green
}

# Alt toolbar'dan yetki modu + badge kaldir
if ($xaml -match 'ModeCombo') {
    $oldToolbar = '(?s)<Border x:Name="ModeBadge".*?</ComboBox>\s*'
    $xaml = $xaml -replace $oldToolbar, ''
    Write-Host "  - ModeBadge + ModeCombo kaldirildi (Ayarlar'a tasindi)" -ForegroundColor Green
}

Set-Content $xamlPath -Value $xaml -Encoding UTF8 -NoNewline

# ============ 2) CS: Settings dialog + servis kur/kaldir ============
Write-Host "MainWindow.xaml.cs guncelleniyor..." -ForegroundColor Cyan
$csPath = "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs"
Copy-Item $csPath "$csPath.bak-$ts" -Force
$cs = Get-Content $csPath -Raw

# Eski ModeCombo handler'i kaldir (XAML'de artik yok)
$cs = $cs -replace '(?s)\s+private async void ModeCombo_SelectionChanged\(object sender, SelectionChangedEventArgs e\)\s*\{.*?\n    \}', ''

# SettingsBtn_Click + servis metotlarini ekle (CloseBtn_Click'ten once)
$settingsMethods = @'
private async void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var serviceRunning = IsServiceInstalled();
        var serviceStatus = serviceRunning ? GetServiceStatus() : "Kurulu degil";
        var modeLabel = _mode switch
        {
            AuthorizationMode.ReadOnly => "R0 - Salt Okunur",
            AuthorizationMode.Approved => "R1 - Onayli",
            AuthorizationMode.AutoLowRisk => "R2 - Otomatik",
            AuthorizationMode.FullAccess => "R3 - Uzman",
            _ => "R0"
        };

        var panel = new StackPanel { Spacing = 14, MinWidth = 440 };

        // -- Yetki Modu --
        panel.Children.Add(new TextBlock { Text = "Yetki Modu", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, Foreground = HexBrush("#58a6ff") });
        var modeCombo = new ComboBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        modeCombo.Items.Add(new ComboBoxItem { Content = "R0 - Salt Okunur", Tag = "0" });
        modeCombo.Items.Add(new ComboBoxItem { Content = "R1 - Onayli", Tag = "1" });
        modeCombo.Items.Add(new ComboBoxItem { Content = "R2 - Otomatik", Tag = "2" });
        modeCombo.Items.Add(new ComboBoxItem { Content = "R3 - Uzman", Tag = "3" });
        modeCombo.SelectedIndex = (int)_mode;
        panel.Children.Add(modeCombo);

        // -- Servis --
        panel.Children.Add(new TextBlock { Text = "Arka Plan Servisi (YapairApi)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, Foreground = HexBrush("#58a6ff"), Margin = new Thickness(0, 8, 0, 0) });
        var statusBlock = new TextBlock
        {
            Text = $"Durum: {serviceStatus}",
            Foreground = serviceRunning ? HexBrush("#3fb950") : HexBrush("#8b949e"),
            FontSize = 12
        };
        panel.Children.Add(statusBlock);

        var serviceButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var installBtn = new Button
        {
            Content = "Servisi Kur",
            Background = HexBrush("#238636"),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6, 14, 6),
            IsEnabled = !serviceRunning
        };
        var removeBtn = new Button
        {
            Content = "Servisi Kaldir",
            Background = HexBrush("#da3633"),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6, 14, 6),
            IsEnabled = serviceRunning
        };
        serviceButtons.Children.Add(installBtn);
        serviceButtons.Children.Add(removeBtn);
        panel.Children.Add(serviceButtons);

        var infoText = new TextBlock
        {
            Text = "Servis kurmak yonetici yetkisi gerektirir. Kurulumdan sonra API otomatik olarak Windows baslangicinda calisir.",
            Foreground = HexBrush("#8b949e"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        panel.Children.Add(infoText);

        // -- Dialog --
        var dialog = new ContentDialog
        {
            Title = "Ayarlar",
            Content = new ScrollViewer { Content = panel, MaxHeight = 500 },
            PrimaryButtonText = "Kaydet",
            CloseButtonText = "Iptal",
            XamlRoot = Content.XamlRoot
        };

        // -- Servis butonlari --
        installBtn.Click += async (s, ev) =>
        {
            installBtn.IsEnabled = false;
            statusBlock.Text = "Servis kuruluyor... (yonetici onayi gerekebilir)";
            var ok = await RunElevatedScriptAsync("install-service.ps1");
            serviceRunning = IsServiceInstalled();
            serviceStatus = serviceRunning ? GetServiceStatus() : "Kurulum basarisiz";
            statusBlock.Text = "Durum: " + serviceStatus;
            statusBlock.Foreground = serviceRunning ? HexBrush("#3fb950") : HexBrush("#f85149");
            installBtn.IsEnabled = !serviceRunning;
            removeBtn.IsEnabled = serviceRunning;
        };

        removeBtn.Click += async (s, ev) =>
        {
            removeBtn.IsEnabled = false;
            statusBlock.Text = "Servis kaldiriliyor...";
            var ok = await RunElevatedScriptAsync("uninstall-service.ps1");
            serviceRunning = IsServiceInstalled();
            serviceStatus = serviceRunning ? GetServiceStatus() : "Kaldirildi";
            statusBlock.Text = "Durum: " + serviceStatus;
            statusBlock.Foreground = serviceRunning ? HexBrush("#3fb950") : HexBrush("#8b949e");
            installBtn.IsEnabled = !serviceRunning;
            removeBtn.IsEnabled = serviceRunning;
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (modeCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var tag))
            {
                var newMode = (AuthorizationMode)tag;
                if (newMode == AuthorizationMode.FullAccess && _mode != AuthorizationMode.FullAccess)
                {
                    var confirm = new ContentDialog
                    {
                        Title = "Tam Erisim (R3)",
                        Content = new TextBlock { Text = "R3 guvenlik azaltan degisikliklere izin verir. Yetkili uzman misiniz?", TextWrapping = TextWrapping.Wrap },
                        PrimaryButtonText = "Evet", CloseButtonText = "Vazgec",
                        XamlRoot = Content.XamlRoot
                    };
                    if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
                }
                var oldMode = _mode;
                _mode = newMode;
                SaveMode();
                UpdateModeBadge();
                await AuditLogger.LogModeChangeAsync(_repoRoot, oldMode.ToString(), _mode.ToString());
            }
        }
    }

    private static bool IsServiceInstalled()
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController("YapairApi");
            _ = sc.Status; // throws if not found
            return true;
        }
        catch { return false; }
    }

    private static string GetServiceStatus()
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController("YapairApi");
            return sc.Status.ToString();
        }
        catch { return "Bilinmiyor"; }
    }

    private async Task<bool> RunElevatedScriptAsync(string scriptName)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"Start-Process powershell -Verb RunAs -ArgumentList '-ExecutionPolicy Bypass -NoExit -File `\"{Path.Combine(_repoRoot, scriptName)}`\"'\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            var p = System.Diagnostics.Process.Start(psi);
            if (p != null) await Task.Delay(1500);
            return true;
        }
        catch (Exception ex)
        {
            var err = new ContentDialog
            {
                Title = "Yonetici onayi gerekli",
                Content = new TextBlock { Text = "Servis kurmak icin yonetici yetkisi gerekiyor.\n\nHata: " + ex.Message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Tamam",
                XamlRoot = Content.XamlRoot
            };
            await err.ShowAsync();
            return false;
        }
    }

    private void CloseBtn_Click
'@

if ($cs -notmatch 'SettingsBtn_Click') {
    $cs = $cs.Replace('private void CloseBtn_Click', $settingsMethods)
    Write-Host "  + SettingsBtn_Click + servis metotlari" -ForegroundColor Green
} else {
    Write-Host "  Zaten var" -ForegroundColor DarkGray
}

Set-Content $csPath -Value $cs -Encoding UTF8 -NoNewline

# ============ 3) Dogrulama ============
Write-Host ""
Write-Host "Dogrulama:" -ForegroundColor Cyan
$xaml2 = Get-Content $xamlPath -Raw
$cs2 = Get-Content $csPath -Raw

$checks = @{
    "XAML SettingsBtn"          = $xaml2 -match 'SettingsBtn_Click'
    "XAML ModeCombo kaldirildi" = ($xaml2 -notmatch 'ModeCombo')
    "CS SettingsBtn_Click"      = $cs2 -match 'private async void SettingsBtn_Click'
    "CS IsServiceInstalled"     = $cs2 -match 'IsServiceInstalled'
    "CS RunElevatedScript"      = $cs2 -match 'RunElevatedScriptAsync'
    "CS servis kur/kaldir"      = ($cs2 -match 'install-service\.ps1' -and $cs2 -match 'uninstall-service\.ps1')
    "CS ModeCombo handler yok"  = ($cs2 -notmatch 'ModeCombo_SelectionChanged')
}
$allOk = $true
foreach ($k in $checks.Keys) {
    $ok = $checks[$k]
    if (-not $ok) { $allOk = $false }
    Write-Host ("  {0,-30} {1}" -f $k, $(if ($ok) {"OK"} else {"EKSIK"})) -ForegroundColor $(if ($ok) {"Green"} else {"Red"})
}

if (-not $allOk) { Write-Host "Eksikler var!" -ForegroundColor Red; exit 1 }

# ============ 4) Build ============
Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan