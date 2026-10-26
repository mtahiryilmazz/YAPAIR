using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Net.Http;
using System.Net.Http.Json;
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
    private string? _posFile;
    private bool _isPinned;
    public bool IsExplicitExit { get; set; } = false;
    private DispatcherTimer? _repairPollTimer;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        _repoRoot = CliBridge.FindRepoRoot();
        _cliDll = CliBridge.FindCliDll(_repoRoot);
        _executors = new List<IRepairExecutor>
        {
            new SpoolerRestartExecutor(),
            new AudioRestartExecutor(),
            new PrinterQueueClearExecutor(),
            new ServicesControlledRestartExecutor(),
            new NetworkAdapterResetExecutor()
        };

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarGrid);

        _posFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "window-pos.json");

        try
        {
            var aw = GetAppWindow();
            ConfigureWindow(aw);
            aw.Closing += (s, e) =>
            {
                if (!IsExplicitExit)
                {
                    e.Cancel = true;
                    HideWindow();
                }
            };
            aw.Changed += (s, e) =>
            {
                if (e.DidPositionChange || e.DidSizeChange)
                {
                    SaveWindowPos(aw);
                }
            };
        }
        catch { }

        _modeFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "authorization-mode.txt");
        LoadMode();
        UpdateModeBadge();
        StartRemoteRepairPolling();

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
                if ((newMode == AuthorizationMode.AutoLowRisk || newMode == AuthorizationMode.FullAccess) && _mode != newMode)
                {
                    var pinBox = new TextBox { PlaceholderText = "6 haneli onay kodu (PIN)...", MaxLength = 6 };
                    var requestBtn = new Button { Content = "Onay Kodu Talep Et", FontSize = 12 };
                    var statusBlockText = new TextBlock { FontSize = 11, Foreground = HexBrush("#8b949e") };

                    requestBtn.Click += async (s, ev) =>
                    {
                        requestBtn.IsEnabled = false;
                        statusBlockText.Text = "Onay talebi gönderiliyor...";
                        try
                        {
                            using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5080/") };
                            var modeStr = newMode == AuthorizationMode.FullAccess ? "R3" : "R2";
                            var resp = await client.PostAsJsonAsync("admin/authorization/request-approval", new
                            {
                                machine_key = Environment.MachineName,
                                requested_mode = modeStr,
                                reason = "Desktop client yetki modu geçişi"
                            });
                            if (resp.IsSuccessStatusCode)
                            {
                                statusBlockText.Text = "Talep iletildi! Yönetici konsolundan onaylandıktan sonra PIN kodunu giriniz.";
                                statusBlockText.Foreground = HexBrush("#3fb950");
                            }
                            else
                            {
                                statusBlockText.Text = "Talep iletilemedi (API çevrimdışı).";
                                statusBlockText.Foreground = HexBrush("#f85149");
                            }
                        }
                        catch (Exception ex)
                        {
                            statusBlockText.Text = "Hata: " + ex.Message;
                            statusBlockText.Foreground = HexBrush("#f85149");
                        }
                        finally { requestBtn.IsEnabled = true; }
                    };

                    var pinPanel = new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock { Text = $"{newMode} modu kritik güvenlik yetkileri içerir. 2. Onay (Dual Authorization) kapsamında yönetici panelinden üretilen onay kodunu (PIN) giriniz:", TextWrapping = TextWrapping.Wrap },
                            pinBox,
                            requestBtn,
                            statusBlockText
                        }
                    };

                    var confirm = new ContentDialog
                    {
                        Title = $"2. Onay Gerekli ({newMode})",
                        Content = pinPanel,
                        PrimaryButtonText = "Doğrula ve Geç",
                        CloseButtonText = "Vazgeç",
                        XamlRoot = Content.XamlRoot
                    };

                    if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

                    var enteredPin = pinBox.Text?.Trim() ?? "";
                    bool pinValid = false;

                    try
                    {
                        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5080/") };
                        var modeStr = newMode == AuthorizationMode.FullAccess ? "R3" : "R2";
                        var resp = await client.PostAsJsonAsync("admin/authorization/validate-code", new
                        {
                            machine_key = Environment.MachineName,
                            mode = modeStr,
                            code = enteredPin
                        });
                        if (resp.IsSuccessStatusCode)
                        {
                            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                            pinValid = json.TryGetProperty("valid", out var v) && v.GetBoolean();
                        }
                    }
                    catch { }

                    if (!pinValid)
                    {
                        var errDialog = new ContentDialog
                        {
                            Title = "Geçersiz Onay Kodu",
                            Content = new TextBlock { Text = "Girdiğiniz onay kodu doğrulanamadı veya süresi dolmuş. Yetki modu değiştirilmedi.", TextWrapping = TextWrapping.Wrap },
                            CloseButtonText = "Tamam",
                            XamlRoot = Content.XamlRoot
                        };
                        await errDialog.ShowAsync();
                        return;
                    }
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

    public void ForceClose()
    {
        IsExplicitExit = true;
        _repairPollTimer?.Stop();
        Close();
    }

    private void UpdateModeBadge()
    {
        if (ModeBadgeBorder != null && ModeBadgeText != null && ModeBadgeDot != null)
        {
            var (label, fgHex, bgHex, borderHex) = _mode switch
            {
                AuthorizationMode.ReadOnly => ("R0 - Salt Okunur", "#58a6ff", "#0d1d3a", "#1f6feb"),
                AuthorizationMode.Approved => ("R1 - Onaylı", "#d29922", "#2a1f0d", "#d29922"),
                AuthorizationMode.AutoLowRisk => ("R2 - Otomatik", "#3fb950", "#0d2a14", "#238636"),
                AuthorizationMode.FullAccess => ("R3 - Uzman", "#f85149", "#2a0d0d", "#da3633"),
                _ => ("R0 - Salt Okunur", "#58a6ff", "#0d1d3a", "#1f6feb")
            };

            ModeBadgeText.Text = label;
            ModeBadgeText.Foreground = HexBrush(fgHex);
            ModeBadgeDot.Fill = HexBrush(fgHex);
            ModeBadgeBorder.Background = HexBrush(bgHex);
            ModeBadgeBorder.BorderBrush = HexBrush(borderHex);
        }
    }


    private void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        try
        {
            var aw = GetAppWindow();
            if (aw.Presenter is OverlappedPresenter p)
            {
                p.IsAlwaysOnTop = _isPinned;
            }
            PinIcon.Foreground = HexBrush(_isPinned ? "#58a6ff" : "#8b949e");
            ToolTipService.SetToolTip(PinBtn, _isPinned ? "Sabitlemeyi kaldir" : "Pencereyi sabitle (Ustte tut)");
            SaveWindowPos(aw);
        }
        catch { }
    }

    private void ConfigureWindow(AppWindow aw)
    {
        const int defWidth = 420;
        const int defHeight = 560;

        WindowPos? saved = null;
        try
        {
            if (_posFile != null && File.Exists(_posFile))
            {
                var txt = File.ReadAllText(_posFile);
                saved = JsonSerializer.Deserialize<WindowPos>(txt);
            }
        }
        catch { }

        int w = saved != null && saved.Width >= 360 && saved.Width <= 800 ? saved.Width : defWidth;
        int h = saved != null && saved.Height >= 400 && saved.Height <= 900 ? saved.Height : defHeight;

        int x, y;
        if (saved != null && saved.X >= 0 && saved.Y >= 0)
        {
            x = saved.X;
            y = saved.Y;
        }
        else
        {
            var displayArea = DisplayArea.GetFromWindowId(aw.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            x = workArea.X + workArea.Width - w - 16;
            y = workArea.Y + workArea.Height - h - 16;
        }

        aw.MoveAndResize(new Windows.Graphics.RectInt32(x, y, w, h));

        if (aw.Presenter is OverlappedPresenter p)
        {
            p.IsResizable = true;
            p.IsMaximizable = false;
            p.IsMinimizable = true;
            if (saved != null && saved.Pinned)
            {
                _isPinned = true;
                p.IsAlwaysOnTop = true;
                PinIcon.Foreground = HexBrush("#58a6ff");
                ToolTipService.SetToolTip(PinBtn, "Sabitlemeyi kaldir");
            }
        }

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = aw.TitleBar;
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 201, 209, 217);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(60, 255, 255, 255);
            titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 139, 148, 158);
        }
    }

    private void StartRemoteRepairPolling()
    {
        _repairPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _repairPollTimer.Tick += async (s, e) => await CheckRemoteRepairsAsync();
        _repairPollTimer.Start();
    }

    private async Task CheckRemoteRepairsAsync()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5080/") };
            var resp = await client.GetAsync($"api/v1/remote-repair/pending?machine_key={Uri.EscapeDataString(Environment.MachineName)}");
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var rep in items.EnumerateArray())
                {
                    var id = rep.TryGetProperty("id", out var idp) ? idp.GetString() : null;
                    var playbookId = rep.TryGetProperty("playbook_id", out var pip) ? pip.GetString() : null;
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(playbookId)) continue;

                    var executor = _executors.FirstOrDefault(x => string.Equals(x.PlaybookId, playbookId, StringComparison.OrdinalIgnoreCase));
                    if (executor != null)
                    {
                        AddBotMessage($"[UZAKTAN MÜDAHALE] Yönetici konsolundan komut alındı: {playbookId}. Başlatılıyor...");
                        var res = await executor.ExecuteAsync();
                        var status = res.Success ? "completed" : "failed";

                        await client.PostAsJsonAsync("api/v1/remote-repair/result", new
                        {
                            repair_id = id,
                            status = status,
                            output = res.Message
                        });

                        AddBotMessage(res.Success
                            ? $"[UZAKTAN MÜDAHALE] Tamamlandı: {res.Message}"
                            : $"[UZAKTAN MÜDAHALE] Hata: {res.Message}");
                    }
                    else
                    {
                        await client.PostAsJsonAsync("api/v1/remote-repair/result", new
                        {
                            repair_id = id,
                            status = "failed",
                            output = $"Tanınmayan onarım playbook: {playbookId}"
                        });
                    }
                }
            }
        }
        catch { }
    }

    private void SaveWindowPos(AppWindow aw)
    {
        try
        {
            if (_posFile == null) return;
            var pos = aw.Position;
            var size = aw.Size;
            if (size.Width <= 0 || size.Height <= 0) return;
            var wp = new WindowPos(pos.X, pos.Y, size.Width, size.Height, _isPinned);
            Directory.CreateDirectory(Path.GetDirectoryName(_posFile)!);
            File.WriteAllText(_posFile, JsonSerializer.Serialize(wp));
        }
        catch { }
    }

    private sealed record WindowPos(int X, int Y, int Width, int Height, bool Pinned);

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

        // 1. Eğer Google AI Studio (Gemini) yanıt verdiyse, doğrudan sohbet yanıtı olarak öne çıkar
        if (!string.IsNullOrWhiteSpace(research))
        {
            sb.AppendLine(research.Trim());

            // Eğer sistemde yerel bulgular da tespit edildiyse sohbetin altına bilgi notu ekle
            if (findings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"🔍 Tespit Edilen Sistem Bulguları ({findings.Count} adet):");
                foreach (var f in findings.Take(3)) sb.AppendLine($"• [{f.RiskLabel}] {f.Summary}");
                if (findings.Count > 3) sb.AppendLine($"• ...ve {findings.Count - 3} ek bulgu. (Onarmak için 'Çöz' butonunu kullanabilirsiniz.)");
                else sb.AppendLine("Onarım adımını başlatmak için 'Çöz' butonuna tıklayabilirsiniz.");
            }
        }
        else
        {
            // AI anahtarı yoksa veya offline ise standart yerel tarama mesajı
            if (findings.Count == 0)
            {
                sb.AppendLine("Bu konuyla ilgili yerel sistem taramasında herhangi bir anormallik bulunamadı.");
                sb.AppendLine("💡 İpucu: Admin panelinden Google AI Studio (Gemini) anahtarı eklendiğinde mesajlarınıza doğrudan yapay zeka yanıt verecektir.");
            }
            else
            {
                sb.AppendLine($"Yerel taramada {findings.Count} ilgili sistem bulgusu tespit edildi:");
                foreach (var f in findings.Take(5)) sb.AppendLine($"- [{f.RiskLabel}] {f.Summary}");
                if (findings.Count > 5) sb.AppendLine($"... ve {findings.Count - 5} bulgu daha. 'Çöz' butonuyla tümünü görüp onarabilirsiniz.");
                else sb.AppendLine("Onarım için 'Çöz' butonuna tıklayın.");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string?> ResearchAsync(string query)
    {
        if (ResearchCache.TryGetCached(query, out var cached)) return cached;
        if (!await ResearchCache.WaitIfNeededAsync())
            return "(Gemini kota siniri asildi. Biraz bekleyin.)";

        try
        {
            var env = LoadEnv();
            var baseUrl = env.TryGetValue("YAPAIR_API_BASE_URL", out var u) && !string.IsNullOrEmpty(u)
                ? u : (Environment.GetEnvironmentVariable("YAPAIR_API_BASE_URL") ?? "http://localhost:5080");

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            // 1. Admin API Sunucusunun AI durumunu kontrol et
            try
            {
                using var statusResp = await http.GetAsync(baseUrl.TrimEnd('/') + "/api/v1/ai-config/status");
                if (statusResp.IsSuccessStatusCode)
                {
                    var statusBody = await statusResp.Content.ReadAsStringAsync();
                    using var sdoc = JsonDocument.Parse(statusBody);
                    var hasKey = sdoc.RootElement.TryGetProperty("has_key", out var hk) && hk.GetBoolean();
                    var webResearch = !sdoc.RootElement.TryGetProperty("enable_web_research", out var wr) || wr.GetBoolean();

                    if (!hasKey)
                    {
                        return "(YAPAIR Admin panelinde Gemini API anahtarı henüz tanımlanmamış. Admin Paneli > AI Ayarları menüsünden anahtar girildiğinde internet destekli çözümler burada görüntülenecektir.)";
                    }

                    if (!webResearch)
                    {
                        return "(YAPAIR Admin panelinde istemci web araştırma izni kapatılmış.)";
                    }
                }
            }
            catch { }

            // 2. Güvenli API uç noktasından çözümü iste
            var payload = JsonSerializer.Serialize(new { query });
            using var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            if (env.TryGetValue("YAPAIR_API_KEY", out var apiKey) && !string.IsNullOrEmpty(apiKey))
            {
                content.Headers.Add("X-Yapair-Api-Key", apiKey);
            }

            using var resp = await http.PostAsync(baseUrl.TrimEnd('/') + "/api/v1/research", content);
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
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            await AuditLogger.LogRepairActionAsync(_repoRoot, "repair_cancelled", executor.PlaybookId, _mode.ToString(), "Kullanici onaylamadi");
            return;
        }

        await AuditLogger.LogRepairActionAsync(_repoRoot, "repair_approved", executor.PlaybookId, _mode.ToString(), "Kullanici tarafindan onaylandi");
        StatusText.Text = "Onarim uygulaniyor...";
        try
        {
            var result = await executor.ExecuteAsync();
            await AuditLogger.LogRepairActionAsync(_repoRoot, "repair_executed", executor.PlaybookId, _mode.ToString(), result.Message);
            AddBotMessage(result.Success
                ? $"Onarim basarili: {result.Message}"
                : $"Onarim basarisiz: {result.Message}");
        }
        catch (Exception ex)
        {
            await AuditLogger.LogRepairActionAsync(_repoRoot, "repair_failed", executor.PlaybookId, _mode.ToString(), ex.Message);
            AddBotMessage("Onarim hatasi: " + ex.Message);
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
