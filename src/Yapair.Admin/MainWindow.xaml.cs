using System.Diagnostics;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Yapair.Admin.Services;
using Yapair.Admin.Views;

namespace Yapair.Admin;

public sealed partial class MainWindow : Window
{
    private readonly AdminApiClient _api;
    private readonly AdminNotificationService _notificationService;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarGrid);

        _api = new AdminApiClient();
        _notificationService = new AdminNotificationService(_api);

        try
        {
            var aw = GetAppWindow();
            aw.Resize(new Windows.Graphics.SizeInt32(1150, 780));
            CenterWindow(aw);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = aw.TitleBar;
                // 48px başlık çubuğu ile sistem butonlarının yüksekliği eşleşsin
                titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
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
        catch { }

        // Başlık çubuğu içindeki tıklanabilir öğeleri sürükleme alanından hariç tut
        TitleBarGrid.Loaded += (_, _) => UpdateTitleBarRegions();
        TitleBarGrid.SizeChanged += (_, _) => UpdateTitleBarRegions();
        ApiStatusBadge.SizeChanged += (_, _) => UpdateTitleBarRegions();
        TitleBarActions.SizeChanged += (_, _) => UpdateTitleBarRegions();

        _notificationService.OnNewHelpRequest += (req) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                NotificationBar.Title = "Yeni Yardım Talebi!";
                NotificationBar.Message = $"[{req.MachineLabel}] {req.ProblemText}";
                NotificationBar.IsOpen = true;

                var btn = new Button { Content = "Talepleri Aç", Padding = new Thickness(8, 4, 8, 4), FontSize = 12 };
                btn.Click += (s, ev) =>
                {
                    NavigateToTag("Help");
                    NotificationBar.IsOpen = false;
                };
                NotificationBar.ActionButton = btn;
            });
        };
        _notificationService.Start();

        Closed += async (s, e) =>
        {
            _notificationService.Dispose();
            await EmbeddedApiServer.StopAsync();
        };

        CheckApiHealthAsync();

        // Default navigation
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private AppWindow GetAppWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var id = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(id);
    }

    private static void CenterWindow(AppWindow aw)
    {
        var dispArea = DisplayArea.GetFromWindowId(aw.Id, DisplayAreaFallback.Nearest);
        if (dispArea != null)
        {
            var cx = (dispArea.WorkArea.Width - aw.Size.Width) / 2;
            var cy = (dispArea.WorkArea.Height - aw.Size.Height) / 2;
            aw.Move(new Windows.Graphics.PointInt32(Math.Max(0, cx), Math.Max(0, cy)));
        }
    }

    private void UpdateTitleBarRegions()
    {
        try
        {
            if (TitleBarGrid.XamlRoot == null) return;
            var aw = GetAppWindow();
            var scale = TitleBarGrid.XamlRoot.RasterizationScale;

            // Sistem butonlarının gerçek genişliği kadar boşluk bırak
            if (aw.TitleBar.RightInset > 0)
                CaptionButtonsColumn.Width = new GridLength(aw.TitleBar.RightInset / scale);

            var rects = new List<Windows.Graphics.RectInt32>();
            foreach (FrameworkElement el in new FrameworkElement[] { ApiStatusBadge, TitleBarActions })
            {
                if (el.ActualWidth <= 0) continue;
                var b = el.TransformToVisual(null).TransformBounds(
                    new Windows.Foundation.Rect(0, 0, el.ActualWidth, el.ActualHeight));
                rects.Add(new Windows.Graphics.RectInt32(
                    (int)Math.Round(b.X * scale), (int)Math.Round(b.Y * scale),
                    (int)Math.Round(b.Width * scale), (int)Math.Round(b.Height * scale)));
            }

            InputNonClientPointerSource.GetForWindowId(aw.Id)
                .SetRegionRects(NonClientRegionKind.Passthrough, rects.ToArray());
        }
        catch { }
    }

    private async void CheckApiHealthAsync()
    {
        var health = await _api.GetHealthAsync();
        if (health.HasValue)
        {
            var mode = health.Value.TryGetProperty("mode", out var m) ? m.GetString() : "online";
            var modeLabel = mode == "local_embedded" ? "Entegre API (Port 5080)" : "Bağlı (Bulut)";

            ApiStatusBadge.Background = HexBrush("#0d2a14");
            ApiStatusBadge.BorderBrush = HexBrush("#238636");
            ApiStatusText.Text = $"API: {modeLabel}";
            ApiStatusText.Foreground = HexBrush("#3fb950");
            StatusDot.Fill = HexBrush("#3fb950");
        }
        else
        {
            ApiStatusBadge.Background = HexBrush("#2a0d0d");
            ApiStatusBadge.BorderBrush = HexBrush("#da3633");
            ApiStatusText.Text = EmbeddedApiServer.IsRunning ? "API: Çevrimdışı" : "API: Sunucu Durduruldu";
            ApiStatusText.Foreground = HexBrush("#f85149");
            StatusDot.Fill = HexBrush("#f85149");
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateToTag(tag);
        }
    }

    private void NavigateToTag(string tag)
    {
        Type? pageType = tag switch
        {
            "Overview" => typeof(OverviewView),
            "Help" => typeof(HelpRequestsView),
            "Cases" => typeof(CasesView),
            "Batches" => typeof(BatchesView),
            "Machines" => typeof(MachinesView),
            "Audit" => typeof(AuditView),
            "Approvals" => typeof(ApprovalsView),
            "ApiKeys" => typeof(ApiKeysView),
            "Research" => typeof(ResearchView),
            _ => null
        };

        if (pageType != null)
        {
            ContentFrame.Navigate(pageType, _api);

            foreach (var mi in NavView.MenuItems.OfType<NavigationViewItem>())
            {
                if (mi.Tag?.ToString() == tag)
                {
                    NavView.SelectedItem = mi;
                    break;
                }
            }
        }
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        CheckApiHealthAsync();
        if (ContentFrame.Content is OverviewView ov) _ = ov.RefreshDataAsync();
        else if (ContentFrame.Content is Page p)
        {
            // Re-navigating refreshes page
            ContentFrame.Navigate(p.GetType(), _api);
        }
    }

    private async void StartServer_Click(object sender, RoutedEventArgs e)
    {
        await EmbeddedApiServer.StartAsync();
        CheckApiHealthAsync();
        ShowNotification("Sunucu Başlatıldı", "Yerel API sunucusu Port 5080 üzerinden çalışıyor.");
    }

    private async void StopServer_Click(object sender, RoutedEventArgs e)
    {
        await EmbeddedApiServer.StopAsync();
        CheckApiHealthAsync();
        ShowNotification("Sunucu Durduruldu", "Yerel API sunucusu kapatıldı.");
    }

    private async void RestartServer_Click(object sender, RoutedEventArgs e)
    {
        await EmbeddedApiServer.RestartAsync();
        CheckApiHealthAsync();
        ShowNotification("Sunucu Yeniden Başlatıldı", "Port 5080 üzerinden servis yeniden aktif.");
    }

    private async void SeedData_Click(object sender, RoutedEventArgs e)
    {
        var ok = await _api.SeedDemoDataAsync();
        if (ok)
        {
            ShowNotification("Demo Verileri Yüklendi", "5 vaka, 3 makine, audit ve onarım kayıtları yerel veritabanına eklendi.");
            RefreshBtn_Click(sender, e);
        }
        else
        {
            ShowNotification("Hata", "Demo verileri yüklenirken sunucuya erişilemedi.");
        }
    }

    private void OpenDataDir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YAPAIR");
            Directory.CreateDirectory(p);
            Process.Start(new ProcessStartInfo
            {
                FileName = p,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void WebUiBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:5080/admin/index.html",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void ShowNotification(string title, string msg)
    {
        NotificationBar.Title = title;
        NotificationBar.Message = msg;
        NotificationBar.IsOpen = true;
        NotificationBar.ActionButton = null;
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
}
