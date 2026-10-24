$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$proj = "src\Yapair.Desktop.WinUI"
New-Item -ItemType Directory -Force -Path $proj | Out-Null
Write-Host "Klasor hazir: $proj" -ForegroundColor Green

# ============ 1) csproj ============
$csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>Yapair.Desktop.WinUI</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMsixTooling>true</EnableMsixTooling>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.250205002" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.1742" />
    <PackageReference Include="H.NotifyIcon.WinUI" Version="2.3.0" />
    <PackageReference Include="System.Drawing.Common" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Yapair.Core\Yapair.Core.csproj" />
  </ItemGroup>
</Project>
'@
Set-Content -Path (Join-Path $proj "Yapair.Desktop.WinUI.csproj") -Value $csproj -Encoding UTF8
Write-Host "  csproj" -ForegroundColor Green

# ============ 2) app.manifest ============
$manifest = @'
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="Yapair.Desktop.WinUI.app"/>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">permonitorv2,permonitor</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
'@
Set-Content -Path (Join-Path $proj "app.manifest") -Value $manifest -Encoding UTF8
Write-Host "  app.manifest" -ForegroundColor Green

# ============ 3) App.xaml ============
$appxaml = @'
<Application
    x:Class="Yapair.Desktop.WinUI.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
'@
Set-Content -Path (Join-Path $proj "App.xaml") -Value $appxaml -Encoding UTF8
Write-Host "  App.xaml" -ForegroundColor Green

# ============ 4) RelayCommand.cs ============
$relay = @'
using System.Windows.Input;

namespace Yapair.Desktop.WinUI;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
'@
Set-Content -Path (Join-Path $proj "RelayCommand.cs") -Value $relay -Encoding UTF8
Write-Host "  RelayCommand.cs" -ForegroundColor Green

# ============ 5) CliBridge.cs ============
$bridge = @'
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Yapair.Desktop.WinUI;

public static class CliBridge
{
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Yapair.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }

    public static string FindCliDll(string root)
    {
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var p = Path.Combine(root, "src", "Yapair.Cli", "bin", cfg, "net10.0-windows", "yapair.dll");
            if (File.Exists(p)) return p;
        }
        return Path.Combine(root, "src", "Yapair.Cli", "bin", "Debug", "net10.0-windows", "yapair.dll");
    }

    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string cliDll, string args, TimeSpan timeout, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{cliDll}\" {args}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        var eb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) eb.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return (-1, sb.ToString(), "Zaman asimi");
        }
        return (p.ExitCode, sb.ToString(), eb.ToString());
    }
}
'@
Set-Content -Path (Join-Path $proj "CliBridge.cs") -Value $bridge -Encoding UTF8
Write-Host "  CliBridge.cs" -ForegroundColor Green

# ============ 6) IntentDetector.cs ============
$intent = @'
namespace Yapair.Desktop.WinUI;

public static class IntentDetector
{
    private static readonly (string[] Keywords, string Domain)[] Map =
    {
        (new[] { "bluetooth", "kablosuz" }, "bluetooth"),
        (new[] { "yazici", "yazdır", "yazdir", "printer", "spooler" }, "printers"),
        (new[] { "ag ", "ağ", "internet", "wifi", "ethernet", "baglanti", "bağlantı" }, "network"),
        (new[] { "ses", "hoparlor", "hoparlör", "mikrofon", "audio" }, "audio"),
        (new[] { "usb", "aygit", "aygıt", "surucu", "sürücü", "driver" }, "devices"),
        (new[] { "guncelleme", "güncelleme", "update" }, "windows_update"),
        (new[] { "uac", "yonetici", "yönetici", "izin", "yetki", "firewall" }, "security")
    };

    public static string? Detect(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var lower = " " + message.ToLowerInvariant() + " ";
        foreach (var (keys, domain) in Map)
            if (keys.Any(k => lower.Contains(k))) return domain;
        return null;
    }
}
'@
Set-Content -Path (Join-Path $proj "IntentDetector.cs") -Value $intent -Encoding UTF8
Write-Host "  IntentDetector.cs" -ForegroundColor Green

# ============ 7) ChatMessage.cs ============
$msg = @'
namespace Yapair.Desktop.WinUI;

public sealed class ChatMessage
{
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsUser { get; set; }
    public string TimeDisplay { get; set; } = "";
    public string AuthorColor => IsUser ? "#3fb950" : "#58a6ff";
    public string BubbleBg => IsUser ? "#14324b" : "#161b22";
}

public sealed class FindingItem
{
    public string Severity { get; set; } = "info";
    public string Summary { get; set; } = "";
    public string PlaybookText { get; set; } = "";
    public string RiskLabel { get; set; } = "R0";
    public string RiskBg { get; set; } = "#21262d";
    public string RiskFg { get; set; } = "#8b949e";
}
'@
Set-Content -Path (Join-Path $proj "ChatMessage.cs") -Value $msg -Encoding UTF8
Write-Host "  ChatMessage.cs" -ForegroundColor Green

# ============ 8) App.xaml.cs ============
$appcs = @'
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;
using System.IO;

namespace Yapair.Desktop.WinUI;

public partial class App : Application
{
    public static TrayIcon? Tray { get; private set; }
    public static MainWindow? MainWin { get; private set; }

    public App()
    {
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Dark;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWin = new MainWindow();
        MainWin.Activate();
        CreateTray();
    }

    private void CreateTray()
    {
        try
        {
            Tray = new TrayIcon
            {
                ToolTipText = "YAPAIR - Sorun Giderici",
                Icon = BuildIcon()
            };
            Tray.LeftClickCommand = new RelayCommand(ToggleWindow);
            Tray.ContextMenu = BuildMenu();
            Tray.ForceCreate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Tray init failed: " + ex.Message);
        }
    }

    private static Icon BuildIcon()
    {
        try
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255, 31, 111, 235));
                using var font = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(Color.White);
                var s = g.MeasureString("Y", font);
                g.DrawString("Y", font, brush, (32 - s.Width) / 2, (32 - s.Height) / 2);
            }
            var h = bmp.GetHicon();
            return (Icon)Icon.FromHandle(h).Clone();
        }
        catch { return SystemIcons.Application; }
    }

    private MenuFlyout BuildMenu()
    {
        var m = new MenuFlyout();
        var show = new MenuFlyoutItem { Text = "Ac" };
        show.Click += (_, _) => MainWin?.ShowWindow();
        var other = new MenuFlyoutItem { Text = "Diger Sorunlar" };
        other.Click += (_, _) => MainWin?.ShowOtherIssuesAsync();
        var exit = new MenuFlyoutItem { Text = "Cikis" };
        exit.Click += (_, _) => ExitApp();
        m.Items.Add(show);
        m.Items.Add(other);
        m.Items.Add(new MenuFlyoutSeparator());
        m.Items.Add(exit);
        return m;
    }

    private void ToggleWindow()
    {
        if (MainWin is null) return;
        if (MainWin.IsWindowVisible) MainWin.HideWindow();
        else MainWin.ShowWindow();
    }

    public static void ExitApp()
    {
        try { Tray?.Dispose(); } catch { }
        Tray = null;
        Current.Exit();
    }
}
'@
Set-Content -Path (Join-Path $proj "App.xaml.cs") -Value $appcs -Encoding UTF8
Write-Host "  App.xaml.cs" -ForegroundColor Green

# ============ 9) MainWindow.xaml ============
$mwxaml = @'
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
        <TextBlock x:Name="IntentText" Foreground="#8b949e" FontSize="11"
                   VerticalAlignment="Center" Margin="6,0,0,0"/>
      </StackPanel>
      <Border Grid.Column="1" Background="Transparent"/>
      <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Top">
        <Button Click="MinBtn_Click" Background="Transparent" BorderThickness="0"
                Width="46" Height="40" Padding="0">
          <FontIcon Glyph="&#xE921;" FontSize="10" Foreground="#c9d1d9"/>
        </Button>
        <Button Click="CloseBtn_Click" Background="Transparent" BorderThickness="0"
                Width="46" Height="40" Padding="0">
          <FontIcon Glyph="&#xE8BB;" FontSize="10" Foreground="#c9d1d9"/>
        </Button>
      </StackPanel>
    </Grid>

    <ListView x:Name="MessagesList" Grid.Row="1" SelectionMode="None"
              Background="Transparent" BorderThickness="0" Padding="12">
      <ListView.ItemContainerStyle>
        <Style TargetType="ListViewItem">
          <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
          <Setter Property="Padding" Value="0"/>
          <Setter Property="Margin" Value="0"/>
          <Setter Property="Background" Value="Transparent"/>
          <Setter Property="BorderThickness" Value="0"/>
        </Style>
      </ListView.ItemContainerStyle>
      <ListView.ItemTemplate>
        <DataTemplate>
          <Grid Padding="0,4">
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="6" Margin="6,0,0,3">
              <TextBlock Text="{Binding Author}" FontSize="11" FontWeight="SemiBold"
                         Foreground="{Binding AuthorColor}"/>
              <TextBlock Text="{Binding TimeDisplay}" FontSize="10" Foreground="#484f58"/>
            </StackPanel>
            <Border Grid.Row="1" Background="{Binding BubbleBg}"
                    CornerRadius="8" Padding="12,8" MaxWidth="460" HorizontalAlignment="Left">
              <TextBlock Text="{Binding Text}" Foreground="#c9d1d9" FontSize="13"
                         TextWrapping="Wrap" IsTextSelectionEnabled="True"/>
            </Border>
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
                 BorderThickness="1" Padding="10,8" CornerRadius="6"/>
        <Button Grid.Column="1" Click="SendBtn_Click"
                Background="#238636" Foreground="White" BorderThickness="0"
                Padding="18,8" CornerRadius="6" Margin="8,0,0,0">
          <TextBlock Text="Gonder" FontWeight="SemiBold" FontSize="13"/>
        </Button>
      </Grid>
    </Border>

    <Border Grid.Row="3" Background="#0d1117" BorderBrush="#30363d"
            BorderThickness="0,1,0,0" Padding="12,6">
      <StackPanel Orientation="Horizontal" Spacing="8">
        <Button Click="OtherBtn_Click"
                Background="#21262d" Foreground="#c9d1d9" BorderBrush="#30363d"
                BorderThickness="1" Padding="10,5" CornerRadius="6">
          <TextBlock Text="Diger Sorunlar" FontSize="11"/>
        </Button>
        <Button Click="ClearBtn_Click"
                Background="#21262d" Foreground="#c9d1d9" BorderBrush="#30363d"
                BorderThickness="1" Padding="10,5" CornerRadius="6">
          <TextBlock Text="Sohbeti Temizle" FontSize="11"/>
        </Button>
        <TextBlock x:Name="StatusText" Foreground="#484f58" FontSize="10"
                   VerticalAlignment="Center" Margin="8,0,0,0"/>
      </StackPanel>
    </Border>
  </Grid>
</Window>
'@
Set-Content -Path (Join-Path $proj "MainWindow.xaml") -Value $mwxaml -Encoding UTF8
Write-Host "  MainWindow.xaml" -ForegroundColor Green

# ============ 10) MainWindow.xaml.cs ============
$mwcs = @'
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

namespace Yapair.Desktop.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly string _repoRoot;
    private readonly string _cliDll;
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        _repoRoot = CliBridge.FindRepoRoot();
        _cliDll = CliBridge.FindCliDll(_repoRoot);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarGrid);
        MessagesList.ItemsSource = Messages;

        try
        {
            var appWindow = GetAppWindow();
            appWindow.Resize(new Windows.Graphics.SizeInt32(520, 720));
            if (appWindow.Presenter is OverlappedPresenter p)
            {
                p.IsResizable = true;
                p.IsMaximizable = false;
                p.IsMinimizable = true;
            }
            appWindow.Closing += (s, e) =>
            {
                e.Cancel = true;
                HideWindow();
            };
        }
        catch { }

        AddBotMessage("Merhaba! Ben YAPAIR. Sorununuzu yazin, sisteminizi tarayip cozum onerileri sunayim.");
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

    private void MinBtn_Click(object sender, RoutedEventArgs e)
    {
        if (GetAppWindow().Presenter is OverlappedPresenter p) p.Minimize();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => HideWindow();

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            SendBtn_Click(sender, e);
            e.Handled = true;
        }
    }

    private async void SendBtn_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        InputBox.Text = "";

        AddUserMessage(text);
        var domain = IntentDetector.Detect(text);
        IntentText.Text = domain is null ? "" : $"Odak: {domain}";
        StatusText.Text = "Taranıyor...";

        try
        {
            var args = domain is null
                ? "discover --format json --pretty"
                : $"discover --format json --pretty --focus {domain}";
            var (exit, stdout, stderr) = await CliBridge.RunAsync(_cliDll, args, TimeSpan.FromSeconds(60));

            var findings = string.IsNullOrWhiteSpace(stdout)
                ? new List<FindingItem>()
                : ParseFindings(stdout).ToList();

            var researchAnswer = await ResearchAsync(text);
            var botText = BuildBotResponse(findings, researchAnswer);
            AddBotMessage(botText);
        }
        catch (Exception ex)
        {
            AddBotMessage("Uzgunum, hata: " + ex.Message);
        }
        finally
        {
            StatusText.Text = "";
        }
    }

    private string BuildBotResponse(List<FindingItem> findings, string? research)
    {
        var sb = new System.Text.StringBuilder();
        if (findings.Count == 0)
        {
            sb.AppendLine("Yerel taramada bu sorunla ilgili bulgu bulamadim.");
        }
        else
        {
            sb.AppendLine($"Yerel taramada {findings.Count} ilgili bulgu:");
            foreach (var f in findings.Take(5))
                sb.AppendLine($"- [{f.RiskLabel}] {f.Summary}");
            if (findings.Count > 5)
                sb.AppendLine($"... ve {findings.Count - 5} bulgu daha (Diger Sorunlar)");
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
            return doc.RootElement.TryGetProperty("answer", out var a) ? a.GetString() : null;
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
    {
        Author = "Siz", Text = text, IsUser = true,
        TimeDisplay = DateTime.Now.ToString("HH:mm")
    });

    private void AddBotMessage(string text) => Messages.Add(new ChatMessage
    {
        Author = "YAPAIR", Text = text, IsUser = false,
        TimeDisplay = DateTime.Now.ToString("HH:mm")
    });

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        Messages.Clear();
        AddBotMessage("Sohbet temizlendi.");
    }

    private async void OtherBtn_Click(object sender, RoutedEventArgs e) => await ShowOtherIssuesAsync();

    public async Task ShowOtherIssuesAsync()
    {
        try
        {
            StatusText.Text = "Tum sorunlar aranıyor...";
            var (exit, stdout, stderr) = await CliBridge.RunAsync(
                _cliDll, "discover --format json --pretty", TimeSpan.FromSeconds(60));
            if (string.IsNullOrWhiteSpace(stdout))
            {
                AddBotMessage("Tarama cikti uretmedi.");
                return;
            }
            var findings = ParseFindings(stdout).ToList();
            if (findings.Count == 0)
            {
                AddBotMessage("Cihazinizda sorun bulunmadi.");
                return;
            }
            var panel = new StackPanel { Spacing = 4 };
            foreach (var f in findings)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Thickness(2, 4, 2, 4) };
                row.Children.Add(new Border
                {
                    Background = HexBrush(f.RiskBg),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = f.RiskLabel, Foreground = HexBrush(f.RiskFg), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold }
                });
                row.Children.Add(new TextBlock { Text = f.Summary, Foreground = HexBrush("#c9d1d9"), TextWrapping = TextWrapping.Wrap, FontSize = 12, MaxWidth = 380 });
                panel.Children.Add(row);
            }
            var scroll = new ScrollViewer { Content = panel, MaxHeight = 420 };
            var dialog = new ContentDialog
            {
                Title = $"Cihazinizdaki Sorunlar ({findings.Count})",
                Content = scroll,
                CloseButtonText = "Kapat",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            AddBotMessage("Sorunlar yuklenemedi: " + ex.Message);
        }
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
            if (!TryGetArray(doc.RootElement, out var arr, "findings", "Findings", "issues", "Issues"))
                return list;
            foreach (var item in arr.EnumerateArray())
            {
                var playbookId = GetStr(item, "playbook_id", "PlaybookId");
                var severity = GetStr(item, "severity", "Severity") ?? "info";
                var (riskLabel, riskBg, riskFg) = RiskBadge(playbookId, severity);
                list.Add(new FindingItem
                {
                    Severity = severity,
                    Summary = GetStr(item, "summary", "Summary") ?? "-",
                    PlaybookText = playbookId is null ? "Playbook: yok" : "Playbook: " + playbookId,
                    RiskLabel = riskLabel, RiskBg = riskBg, RiskFg = riskFg
                });
            }
        }
        catch { }
        return list;
    }

    private static (string, string, string) RiskBadge(string? playbookId, string severity)
    {
        if (playbookId is null)
            return severity == "critical" ? ("R3", "#2a0d0d", "#f85149") : ("R0", "#21262d", "#8b949e");
        if (playbookId.StartsWith("security.")) return ("R3", "#2a0d0d", "#f85149");
        if (playbookId.StartsWith("devices.driver.") || playbookId.StartsWith("network.")) return ("R2", "#2a1f0d", "#d29922");
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
'@
Set-Content -Path (Join-Path $proj "MainWindow.xaml.cs") -Value $mwcs -Encoding UTF8
Write-Host "  MainWindow.xaml.cs" -ForegroundColor Green

# ============ 11) Solution'a ekle ============
$existing = (dotnet sln Yapair.slnx list 2>$null) -join "`n"
if ($existing -notmatch "Yapair\.Desktop\.WinUI") {
    dotnet sln Yapair.slnx add "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj"
    Write-Host "  Solution'a eklendi" -ForegroundColor Green
} else {
    Write-Host "  Zaten solution'da" -ForegroundColor Yellow
}

# ============ 12) Build ============
Write-Host ""
Write-Host "Build ediliyor..." -ForegroundColor Yellow
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD BASARISIZ (exit $LASTEXITCODE)" -ForegroundColor Red
    exit 1
}
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan