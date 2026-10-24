$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$ts = Get-Date -Format "yyyyMMdd-HHmmss"
Copy-Item "src\Yapair.Desktop.WinUI\MainWindow.xaml" "src\Yapair.Desktop.WinUI\MainWindow.xaml.bak-$ts" -Force
Copy-Item "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs" "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs.bak-$ts" -Force
Write-Host "Yedek: .bak-$ts" -ForegroundColor DarkGray

# ---------- MainWindow.xaml ----------
@'
<Window
    x:Class="Yapair.Desktop.WinUI.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="YAPAIR">
  <Grid Background="#0d1117">
    <Grid.RowDefinitions>
      <RowDefinition Height="40"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <Grid x:Name="TitleBarGrid" Grid.Row="0" Background="#161b22">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <StackPanel Grid.Column="0" Orientation="Horizontal" Margin="12,0,0,0"
                  VerticalAlignment="Center" Spacing="8">
        <Border Width="20" Height="20" CornerRadius="4" Background="#1f6feb">
          <TextBlock Text="Y" Foreground="White" FontWeight="Bold"
                     HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="12"/>
        </Border>
        <TextBlock Text="YAPAIR" Foreground="#c9d1d9" FontWeight="SemiBold"
                   VerticalAlignment="Center" FontSize="13"/>
      </StackPanel>
      <Border Grid.Column="1" Background="Transparent"/>
      <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Top">
        <Button Click="HideBtn_Click" Background="Transparent" BorderThickness="0"
                Width="46" Height="40" Padding="0" ToolTipService.ToolTip="Tray'e gizle">
          <FontIcon Glyph="&#xE921;" FontSize="10" Foreground="#c9d1d9"/>
        </Button>
        <Button Click="CloseBtn_Click" Background="Transparent" BorderThickness="0"
                Width="46" Height="40" Padding="0" ToolTipService.ToolTip="Kapat">
          <FontIcon Glyph="&#xE8BB;" FontSize="10" Foreground="#c9d1d9"/>
        </Button>
      </StackPanel>
    </Grid>

    <ListView x:Name="MessagesList" Grid.Row="1" SelectionMode="None"
              Background="Transparent" BorderThickness="0" Padding="12">
      <ListView.ItemTemplate>
        <DataTemplate>
          <Grid Padding="0,4">
            <StackPanel>
              <TextBlock Text="{Binding Author}" FontSize="11" Foreground="{Binding AuthorColor}"/>
              <Border Background="{Binding BubbleBg}" CornerRadius="8" Padding="12,8"
                      MaxWidth="460" HorizontalAlignment="Left">
                <TextBlock Text="{Binding Text}" Foreground="#c9d1d9" FontSize="13"
                           TextWrapping="Wrap" IsTextSelectionEnabled="True"/>
              </Border>
            </StackPanel>
          </Grid>
        </DataTemplate>
      </ListView.ItemTemplate>
    </ListView>

    <Border Grid.Row="2" Background="#161b22" BorderBrush="#30363d"
            BorderThickness="0,1,0,0" Padding="12,8">
      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>
          <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBox x:Name="InputBox" Grid.Column="0" PlaceholderText="Sorununuzu yazin..."
                 KeyDown="InputBox_KeyDown"
                 Background="#0d1117" Foreground="#c9d1d9" BorderBrush="#30363d"
                 BorderThickness="1" Padding="10,8"/>
        <Button Grid.Column="1" Click="SendBtn_Click"
                Background="#238636" Foreground="White" BorderThickness="0"
                Padding="18,8" Margin="8,0,0,0">
          <TextBlock Text="Gonder" FontWeight="SemiBold" FontSize="13"/>
        </Button>
      </Grid>
    </Border>

    <Border Grid.Row="3" Background="#0d1117" BorderBrush="#30363d"
            BorderThickness="0,1,0,0" Padding="12,6">
      <StackPanel Orientation="Horizontal" Spacing="8">
        <Border x:Name="ModeBadge" Background="#58a6ff" CornerRadius="10"
                Padding="6,3" VerticalAlignment="Center">
          <TextBlock x:Name="ModeBadgeText" Text="R0" Foreground="White"
                     FontSize="10" FontWeight="Bold"/>
        </Border>
        <ComboBox x:Name="ModeCombo" Width="150" VerticalAlignment="Center"
                  SelectionChanged="ModeCombo_SelectionChanged">
          <ComboBoxItem Content="R0 - Salt Okunur" Tag="0"/>
          <ComboBoxItem Content="R1 - Onayli" Tag="1"/>
          <ComboBoxItem Content="R2 - Otomatik" Tag="2"/>
          <ComboBoxItem Content="R3 - Uzman" Tag="3"/>
        </ComboBox>
        <Button Click="CozBtn_Click"
                Background="#238636" Foreground="White" BorderThickness="0"
                Padding="10,5">
          <TextBlock Text="Coz" FontSize="11" FontWeight="SemiBold"/>
        </Button>
        <Button Click="OtherBtn_Click"
                Background="#21262d" Foreground="#c9d1d9" BorderBrush="#30363d"
                BorderThickness="1" Padding="10,5">
          <TextBlock Text="Diger Sorunlar" FontSize="11"/>
        </Button>
        <Button Click="ClearBtn_Click"
                Background="#21262d" Foreground="#c9d1d9" BorderBrush="#30363d"
                BorderThickness="1" Padding="10,5">
          <TextBlock Text="Temizle" FontSize="11"/>
        </Button>
        <TextBlock x:Name="StatusText" Foreground="#484f58" FontSize="10"
                   VerticalAlignment="Center" Margin="8,0,0,0"/>
      </StackPanel>
    </Border>
  </Grid>
</Window>
'@ | Set-Content "src\Yapair.Desktop.WinUI\MainWindow.xaml" -Encoding UTF8
Write-Host "MainWindow.xaml yazildi" -ForegroundColor Green

# ---------- MainWindow.xaml.cs ----------
@'
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.System;
using Yapair.Core.Playbooks;

namespace Yapair.Desktop.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly string _repoRoot;
    private readonly string _cliDll;
    private readonly List<IRepairExecutor> _executors;
    private readonly List<FindingItem> _lastFindings = new();
    private AuthorizationMode _mode = AuthorizationMode.ReadOnly;
    private string? _modeFile;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        _repoRoot = CliBridge.FindRepoRoot();
        _cliDll = CliBridge.FindCliDll(_repoRoot);
        _executors = new List<IRepairExecutor> { new SpoolerRestartExecutor() };

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarGrid);

        try
        {
            var aw = GetAppWindow();
            aw.Resize(new Windows.Graphics.SizeInt32(520, 720));
            if (aw.Presenter is OverlappedPresenter p)
            {
                p.IsResizable = true;
                p.IsMaximizable = false;
                p.IsMinimizable = true;
                p.SetBorderAndTitleBar(false, false);
            }
            aw.Closing += (s, e) => { e.Cancel = true; HideWindow(); };
        }
        catch { }

        _modeFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "authorization-mode.txt");
        LoadMode();
        ModeCombo.SelectedIndex = (int)_mode;
        UpdateModeBadge();

        MessagesList.ItemsSource = Messages;
        AddBotMessage("Merhaba! Ben YAPAIR. Sorununuzu yazin, sisteminizi tarayip cozum onerileri sunayim. Ornegin: 'bluetooth calismiyor', 'yazici cevrimdisi'.");
    }

    private AppWindow GetAppWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var id = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(id);
    }

    public bool IsWindowVisible => GetAppWindow().IsVisible;
    public void ShowWindow() { GetAppWindow().Show(); Activate(); }
    public void HideWindow() => GetAppWindow().Hide();

    private void HideBtn_Click(object sender, RoutedEventArgs e) => HideWindow();
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => HideWindow();

    private void LoadMode()
    {
        try
        {
            if (_modeFile != null && File.Exists(_modeFile))
            {
                var t = File.ReadAllText(_modeFile).Trim();
                if (int.TryParse(t, out var v) && v >= 0 && v <= 3) _mode = (AuthorizationMode)v;
            }
        }
        catch { }
    }

    private void SaveMode()
    {
        try
        {
            if (_modeFile != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_modeFile)!);
                File.WriteAllText(_modeFile, ((int)_mode).ToString());
            }
        }
        catch { }
    }

    private async void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeCombo.SelectedItem is not ComboBoxItem item) return;
        if (!int.TryParse(item.Tag?.ToString(), out var tag)) return;
        var newMode = (AuthorizationMode)tag;

        if (newMode == AuthorizationMode.FullAccess && _mode != AuthorizationMode.FullAccess)
        {
            var d = new ContentDialog
            {
                Title = "Tam Erisim (R3)",
                Content = new TextBlock
                {
                    Text = "R3 guvenlik azaltan degisikliklere izin verir.\nYetkili uzman misiniz?",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "Evet",
                CloseButtonText = "Vazgec",
                XamlRoot = Content.XamlRoot
            };
            if (await d.ShowAsync() != ContentDialogResult.Primary)
            {
                ModeCombo.SelectedIndex = (int)_mode;
                return;
            }
        }

        var oldMode = _mode;
        _mode = newMode;
        SaveMode();
        UpdateModeBadge();
        await AuditLogger.LogModeChangeAsync(_repoRoot, oldMode.ToString(), _mode.ToString());
    }

    private void UpdateModeBadge()
    {
        var (label, color) = _mode switch
        {
            AuthorizationMode.ReadOnly => ("R0", "#58a6ff"),
            AuthorizationMode.Approved => ("R1", "#d29922"),
            AuthorizationMode.AutoLowRisk => ("R2", "#3fb950"),
            AuthorizationMode.FullAccess => ("R3", "#f85149"),
            _ => ("R0", "#8b949e")
        };
        ModeBadgeText.Text = label;
        ModeBadge.Background = HexBrush(color);
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { SendBtn_Click(sender, e); e.Handled = true; }
    }

    private async void SendBtn_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        InputBox.Text = "";

        AddUserMessage(text);
        var domain = IntentDetector.Detect(text);
        StatusText.Text = domain is null ? "Taraniyor..." : $"Odak: {domain}";

        _lastFindings.Clear();

        try
        {
            var args = domain is null
                ? "discover --format json --pretty"
                : $"discover --format json --pretty --focus {domain}";
            var (exit, stdout, stderr) = await CliBridge.RunAsync(_cliDll, args, TimeSpan.FromSeconds(60));

            var findings = string.IsNullOrWhiteSpace(stdout)
                ? new List<FindingItem>()
                : ParseFindings(stdout).ToList();
            _lastFindings.AddRange(findings);

            var researchAnswer = await ResearchAsync(text);
            AddBotMessage(BuildBotResponse(findings, researchAnswer));
        }
        catch (Exception ex) { AddBotMessage("Hata: " + ex.Message); }
        finally { StatusText.Text = ""; }
    }

    private string BuildBotResponse(List<FindingItem> findings, string? research)
    {
        var sb = new System.Text.StringBuilder();
        if (findings.Count == 0) sb.AppendLine("Yerel taramada bu sorunla ilgili bulgu bulamadim.");
        else
        {
            sb.AppendLine($"Yerel taramada {findings.Count} ilgili bulgu:");
            foreach (var f in findings.Take(5)) sb.AppendLine($"- [{f.RiskLabel}] {f.Summary}");
            if (findings.Count > 5) sb.AppendLine($"... ve {findings.Count - 5} bulgu daha. 'Coz' butonuyla tumunu gorup onarabilirsiniz.");
            else sb.AppendLine("Onarim icin 'Coz' butonuna tiklayin.");
        }
        if (!string.IsNullOrWhiteSpace(research))
        {
            sb.AppendLine();
            sb.AppendLine("Web + AI arastirmasi:");
            sb.AppendLine(research.Length > 800 ? research[..800] + "..." : research);
        }
        return sb.ToString();
    }

    private async Task<string?> ResearchAsync(string query)
    {
        if (ResearchCache.TryGetCached(query, out var cached)) return cached;
        if (!await ResearchCache.WaitIfNeededAsync())
            return "(Gemini kota siniri asildi. Biraz bekleyin.)";

        try
        {
            var env = LoadEnv();
            if (!env.TryGetValue("YAPAIR_API_KEY", out var apiKey)) return null;
            var baseUrl = env.TryGetValue("YAPAIR_API_BASE_URL", out var u) && !string.IsNullOrEmpty(u)
                ? u : "http://localhost:5080";

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            var payload = JsonSerializer.Serialize(new { query });
            using var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            content.Headers.Add("X-Yapair-Api-Key", apiKey);
            using var resp = await http.PostAsync(baseUrl.TrimEnd('/') + "/admin/research", content);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var answer = doc.RootElement.TryGetProperty("answer", out var a) ? a.GetString() : null;
            if (!string.IsNullOrEmpty(answer)) ResearchCache.Save(query, answer);
            return answer;
        }
        catch { return null; }
    }

    private Dictionary<string, string> LoadEnv()
    {
        var path = Path.Combine(_repoRoot, "supabase", ".env.local");
        var dict = new Dictionary<string, string>();
        if (!File.Exists(path)) return dict;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var i = line.IndexOf('=');
            if (i < 1) continue;
            dict[line[..i].Trim()] = line[(i + 1)..].Trim().Trim('"').Trim('\'');
        }
        return dict;
    }

    private void AddUserMessage(string text) => Messages.Add(new ChatMessage
    { Author = "Siz", Text = text, IsUser = true });

    private void AddBotMessage(string text) => Messages.Add(new ChatMessage
    { Author = "YAPAIR", Text = text, IsUser = false });

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        Messages.Clear();
        _lastFindings.Clear();
        AddBotMessage("Sohbet temizlendi.");
    }

    private async void CozBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFindings.Count == 0)
        {
            AddBotMessage("Once bir sorun yazip tarama yapin, sonra 'Coz' butonuna tiklayin.");
            return;
        }
        await ShowFindingsPopupAsync(_lastFindings.ToList(), "Son Tarama Sonuclari");
    }

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

    private async Task OnFixAsync(FindingItem f)
    {
        var playbookId = f.PlaybookText.Replace("Playbook: ", "").Trim();
        var executor = _executors.FirstOrDefault(x => x.PlaybookId == playbookId);

        if (executor is null)
        {
            var d = new ContentDialog
            {
                Title = "Yurutucu yok",
                Content = new TextBlock
                {
                    Text = $"'{playbookId}' icin henuz otomatik yurutucu eklenmedi.",
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "Tamam",
                XamlRoot = Content.XamlRoot
            };
            await d.ShowAsync();
            return;
        }

        var requiredMode = executor.Risk switch
        {
            RiskLevel.R0 => AuthorizationMode.ReadOnly,
            RiskLevel.R1 => AuthorizationMode.Approved,
            RiskLevel.R2 => AuthorizationMode.AutoLowRisk,
            RiskLevel.R3 => AuthorizationMode.FullAccess,
            _ => AuthorizationMode.FullAccess
        };

        if (_mode < requiredMode)
        {
            var warn = new ContentDialog
            {
                Title = "Yetki yetersiz",
                Content = new TextBlock
                {
                    Text = $"Bu islem {executor.Risk} seviyesinde.\nMevcut mod: {_mode}.\nLutfen yetki modunu yukseltin.",
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "Tamam",
                XamlRoot = Content.XamlRoot
            };
            await warn.ShowAsync();
            return;
        }

        var confirm = new ContentDialog
        {
            Title = $"Onarim Onayi - {executor.Risk}",
            Content = new TextBlock
            {
                Text = $"Playbook: {executor.PlaybookId}\nRisk: {executor.Risk}\nYetki modu: {_mode}\n\nUygulansin mi?",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "Onayla",
            CloseButtonText = "Vazgec",
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        StatusText.Text = "Onarim uygulaniyor...";
        try
        {
            var result = await executor.ExecuteAsync();
            AddBotMessage(result.Success
                ? $"Onarim basarili: {result.Message}"
                : $"Onarim basarisiz: {result.Message}");
        }
        catch (Exception ex) { AddBotMessage("Onarim hatasi: " + ex.Message); }
        finally { StatusText.Text = ""; }
    }

    private static SolidColorBrush HexBrush(string hex)
    {
        if (hex.StartsWith("#") && hex.Length == 7)
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255,
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16)));
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 148, 158));
    }

    private static IEnumerable<FindingItem> ParseFindings(string json)
    {
        var list = new List<FindingItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryGetArray(doc.RootElement, out var arr, "findings", "Findings")) return list;
            foreach (var item in arr.EnumerateArray())
            {
                var pid = GetStr(item, "playbook_id", "PlaybookId");
                var sev = GetStr(item, "severity", "Severity") ?? "info";
                var (rl, rb, rf) = RiskBadge(pid, sev);
                list.Add(new FindingItem
                {
                    Severity = sev,
                    Summary = GetStr(item, "summary", "Summary") ?? "-",
                    PlaybookText = pid is null ? "Playbook: yok" : "Playbook: " + pid,
                    RiskLabel = rl, RiskBg = rb, RiskFg = rf
                });
            }
        }
        catch { }
        return list;
    }

    private static (string, string, string) RiskBadge(string? pid, string sev)
    {
        if (pid is null) return sev == "critical" ? ("R3", "#2a0d0d", "#f85149") : ("R0", "#21262d", "#8b949e");
        if (pid.StartsWith("security.")) return ("R3", "#2a0d0d", "#f85149");
        if (pid.StartsWith("devices.") || pid.StartsWith("network.")) return ("R2", "#2a1f0d", "#d29922");
        return ("R1", "#0d2a14", "#3fb950");
    }

    private static string? GetStr(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.ToString();
            }
        return null;
    }

    private static bool TryGetArray(JsonElement root, out JsonElement arr, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var el) && el.ValueKind == JsonValueKind.Array)
            { arr = el; return true; }
        arr = default; return false;
    }
}
'@ | Set-Content "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs" -Encoding UTF8
Write-Host "MainWindow.xaml.cs yazildi" -ForegroundColor Green

# ---------- Dogrulama ----------
Write-Host ""
Write-Host "Dogrulama: Click handler / x:Name eslesmesi" -ForegroundColor Cyan

$xaml = Get-Content "src\Yapair.Desktop.WinUI\MainWindow.xaml" -Raw
$cs = Get-Content "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs" -Raw

$requiredMethods = @(
    "HideBtn_Click","CloseBtn_Click","SendBtn_Click","CozBtn_Click","OtherBtn_Click",
    "ClearBtn_Click","InputBox_KeyDown","ModeCombo_SelectionChanged",
    "ShowOtherIssuesAsync","ShowFindingsPopupAsync","OnFixAsync",
    "ShowWindow","HideWindow","IsWindowVisible","UpdateModeBadge"
)
$missing = @()
foreach ($m in $requiredMethods) {
    if ($cs -notmatch [regex]::Escape($m)) { $missing += $m }
}

if ($missing.Count -gt 0) {
    Write-Host "EKSIK: $($missing -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "  Tum metotlar mevcut" -ForegroundColor Green

$requiredNames = @("TitleBarGrid","MessagesList","InputBox","ModeBadge","ModeBadgeText","ModeCombo","StatusText")
$missingXaml = @()
foreach ($n in $requiredNames) {
    if ($xaml -notmatch "x:Name=`"$n`"") { $missingXaml += $n }
}
if ($missingXaml.Count -gt 0) {
    Write-Host "XAML EKSIK: $($missingXaml -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "  Tum x:Name mevcut" -ForegroundColor Green

# ---------- Build ----------
Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan