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
        StatusText.Text = "TaranÄ±yor...";

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
            StatusText.Text = "Tum sorunlar aranÄ±yor...";
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
