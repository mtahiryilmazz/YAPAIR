using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;
using System.IO;

namespace Yapair.Desktop.WinUI;

public partial class App : Application
{
    public static TaskbarIcon? Tray { get; private set; }
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
            Tray = new TaskbarIcon
            {
                ToolTipText = "YAPAIR - Sorun Giderici",
                Icon = BuildIcon()
            };
            Tray.LeftClickCommand = new RelayCommand(ToggleWindow);
            Tray.ContextFlyout = BuildMenu();
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
        var show = new MenuFlyoutItem { Text = "Göster / Aç" };
        show.Click += (_, _) => MainWin?.ShowWindow();
        show.Command = new RelayCommand(() => MainWin?.ShowWindow());

        var other = new MenuFlyoutItem { Text = "Diğer Sorunlar..." };
        other.Click += (_, _) => MainWin?.ShowOtherIssuesAsync();
        other.Command = new RelayCommand(() => MainWin?.ShowOtherIssuesAsync());

        var exit = new MenuFlyoutItem { Text = "Kapat (Çıkış)" };
        exit.Click += (_, _) => ExitApp();
        exit.Command = new RelayCommand(ExitApp);

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
        try
        {
            Tray?.Dispose();
        }
        catch { }
        Tray = null;

        try
        {
            if (MainWin != null)
            {
                MainWin.IsExplicitExit = true;
                MainWin.ForceClose();
            }
        }
        catch { }

        try
        {
            Current.Exit();
        }
        catch { }

        // Kesin sonlandırma: Windows sistem tepsisinde asılı kalmayı ve arka plan döngüsünü tamamen bitirir
        Environment.Exit(0);
    }
}
