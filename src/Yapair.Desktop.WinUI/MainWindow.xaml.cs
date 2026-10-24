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

    private void UpdateModeBadge()
    {
        // Yetki modu artik Ayarlar dialog'unda gosteriliyor - ana pencerede badge yok
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

    private async void HelpBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = LoadEnv();
            if (!env.TryGetValue("YAPAIR_API_KEY", out var apiKey)) { AddBotMessage("YAPAIR_API_KEY yok"); return; }
            var baseUrl = env.TryGetValue("YAPAIR_API_BASE_URL", out var u) && !string.IsNullOrEmpty(u) ? u : "http://localhost:5080";

            var problemText = _lastFindings.Count > 0
                ? $"{_lastFindings.Count} bulgu cozulemedi. Ilk: {_lastFindings[0].Summary}"
                : "Kullanici yardim istedi";

            var payload = JsonSerializer.Serialize(new {
                machine_key = Environment.MachineName,
                machine_label = Environment.MachineName,
                problem_text = problemText,
                case_id = (string?)null
            });

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            content.Headers.Add("X-Yapair-Api-Key", apiKey);
            using var resp = await http.PostAsync(baseUrl.TrimEnd('/') + "/admin/help-requests", content);

            AddBotMessage(resp.IsSuccessStatusCode
                ? "Yardim talebiniz iletildi. Bilgi Islem ekibi en kisa surede donecek."
                : "Yardim talebi gonderilemedi: HTTP " + (int)resp.StatusCode);
        }
        catch (Exception ex) { AddBotMessage("Yardim talebi hatasi: " + ex.Message); }
    }

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
