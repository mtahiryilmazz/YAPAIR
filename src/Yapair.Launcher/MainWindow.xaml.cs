using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Yapair.Launcher;

public partial class MainWindow : Window
{
    private Process? _apiProcess;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _healthTimer;
    private readonly string _repoRoot;
    private readonly string _apiDllPath;
    private readonly string _desktopExePath;
    private readonly string _logDir;
    private readonly string _logFile;
    private DateTime _startedAt = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        _repoRoot = FindRepoRoot();
        _apiDllPath = FindApiDll(_repoRoot);
        _desktopExePath = FindDesktopExe(_repoRoot);

        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR");
        Directory.CreateDirectory(_logDir);
        _logFile = Path.Combine(_logDir, "launcher.log");

        LogPathText.Text = _logFile;
        FooterText.Text = "Repo: " + _repoRoot + "  ·  API: " + _apiDllPath;

        LogFile("=== Launcher basladi ===");
        LogFile("Repo: " + _repoRoot);
        LogFile("API: " + _apiDllPath);
        LogFile("Log: " + _logFile);

        _healthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _healthTimer.Tick += async (_, _) => await CheckHealthAsync();

        Closed += (_, _) => StopApi();
        Loaded += async (_, _) => await CheckHealthAsync();
    }

    // ---------- Dosya arama ----------
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Yapair.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }

    private static string FindApiDll(string root)
    {
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var p = Path.Combine(root, "src", "Yapair.Api", "bin", cfg, "net10.0", "Yapair.Api.dll");
            if (File.Exists(p)) return p;
        }
        return Path.Combine(root, "src", "Yapair.Api", "bin", "Debug", "net10.0", "Yapair.Api.dll");
    }
    private static string FindDesktopExe(string root)
    {
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var p = Path.Combine(root, "src", "Yapair.Desktop", "bin", cfg, "net10.0-windows", "Yapair.exe");
            if (File.Exists(p)) return p;
        }
        var pub = Path.Combine(root, "publish", "desktop", "Yapair.exe");
        if (File.Exists(pub)) return pub;
        return Path.Combine(root, "src", "Yapair.Desktop", "bin", "Debug", "net10.0-windows", "Yapair.exe");
    }

    // ---------- Log ----------
    private void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        LogBox.AppendText(line + Environment.NewLine);
        LogBox.ScrollToEnd();
        LogFile(line);
    }

    private void LogFile(string line)
    {
        try
        {
            File.AppendAllText(_logFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { /* log yazilamazsa sessizce gec */ }
    }

    // ---------- Baslat / Durdur ----------
    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_apiProcess is not null && !_apiProcess.HasExited) return;

        if (await IsHealthyAsync())
        {
            Log("[i] API zaten calisiyor (baska bir surecte).");
            SetRunning(true);
            _healthTimer.Start();
            return;
        }

        if (!File.Exists(_apiDllPath))
        {
            Log("[HATA] API derlenmemis. Once: dotnet build Yapair.slnx");
            LogFile("[HATA] DLL bulunamadi: " + _apiDllPath);
            return;
        }

        var wwwroot = Path.Combine(Path.GetDirectoryName(_apiDllPath)!, "wwwroot");
        if (!Directory.Exists(wwwroot))
        {
            Log("[UYARI] wwwroot bin'e kopyalanmamis. Admin panel 404 verebilir.");
            LogFile("[UYARI] wwwroot yok: " + wwwroot);
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"exec \"{_apiDllPath}\" --urls http://localhost:5080 --contentRoot \"{Path.Combine(_repoRoot, "src", "Yapair.Api")}\"",
                WorkingDirectory = _repoRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            _apiProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _apiProcess.OutputDataReceived += (_, ev) =>
            {
                if (ev.Data != null) Dispatcher.Invoke(() => Log(ev.Data));
            };
            _apiProcess.ErrorDataReceived += (_, ev) =>
            {
                if (ev.Data != null) Dispatcher.Invoke(() => Log("[ERR] " + ev.Data));
            };
            _apiProcess.Exited += (_, _) => Dispatcher.Invoke(() =>
            {
                int code = -1;
                try { code = _apiProcess?.ExitCode ?? -1; } catch { }
                Log($"[!] API process kapandi (exit {code}).");
            });

            _startedAt = DateTime.UtcNow;
            _apiProcess.Start();
            _apiProcess.BeginOutputReadLine();
            _apiProcess.BeginErrorReadLine();

            Log("[OK] API baslatildi. PID: " + _apiProcess.Id);
            LogFile("[OK] API PID=" + _apiProcess.Id);
            PidText.Text = "PID: " + _apiProcess.Id;

            SetRunning(true);
            SetStatus(false, "Başlatılıyor...", "Sunucu ayağa kalkıyor");
            _healthTimer.Start();
        }
        catch (Exception ex)
        {
            Log("[HATA] " + ex.Message);
            LogFile("[HATA] Baslatma: " + ex);
        }
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e) => StopApi();

    private void StopApi()
    {
        _healthTimer.Stop();
        try
        {
            if (_apiProcess is not null && !_apiProcess.HasExited)
            {
                Log("[..] API durduruluyor...");
                _apiProcess.Kill(entireProcessTree: true);
                _apiProcess.WaitForExit(3000);
                Log("[OK] API durduruldu.");
            }
        }
        catch (Exception ex)
        {
            Log("[HATA] " + ex.Message);
            LogFile("[HATA] Durdurma: " + ex);
        }
        finally
        {
            _apiProcess?.Dispose();
            _apiProcess = null;
            PidText.Text = "PID: —";
            SetRunning(false);
            SetStatus(false, "Durdu", "API çalışmıyor");
        }
    }

    // ---------- Paneli / Log'u ac ----------
    private void DesktopBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(_desktopExePath))
            {
                Log("[HATA] Masaüstü uygulaması derlenmemiş: " + _desktopExePath);
                Log("        Çözüm: dotnet build Yapair.slnx");
                return;
            }
            Log("[..] Masaüstü uygulaması açılıyor: " + Path.GetFileName(_desktopExePath));
            Process.Start(new ProcessStartInfo
            {
                FileName = _desktopExePath,
                WorkingDirectory = Path.GetDirectoryName(_desktopExePath)!,
                UseShellExecute = true
            });
        }
        catch (Exception ex) { Log("[HATA] " + ex.Message); }
    }
    private void OpenBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:5080/admin/index.html",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { Log("[HATA] " + ex.Message); }
    }

    private void OpenLogBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(_logFile))
            {
                Log("[i] Log dosyasi henuz olusmadi.");
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{_logFile}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { Log("[HATA] " + ex.Message); }
    }

    // ---------- Health check ----------
    private async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var resp = await _http.GetAsync("http://localhost:5080/health");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task CheckHealthAsync()
    {
        var healthy = await IsHealthyAsync();

        if (healthy)
        {
            SetStatus(true, "Çalışıyor", "API aktif · http://localhost:5080");
            SetRunning(true);
            return;
        }

        if (_apiProcess is not null && !_apiProcess.HasExited)
        {
            var elapsed = DateTime.UtcNow - _startedAt;
            if (elapsed < TimeSpan.FromSeconds(30))
                SetStatus(false, "Başlatılıyor...", "Sunucu ayağa kalkıyor");
            else
                SetStatus(false, "Yanıt vermiyor", "Health check başarısız");
            SetRunning(true);
            return;
        }

        SetStatus(false, "Durdu", "API çalışmıyor");
        SetRunning(false);
        _healthTimer.Stop();
    }

    // ---------- UI durum ----------
    private void SetRunning(bool running)
    {
        StartBtn.IsEnabled = !running;
        StopBtn.IsEnabled = running;
        OpenBtn.IsEnabled = running;
    }

    private void SetStatus(bool healthy, string title, string subtitle)
    {
        StatusText.Text = title;
        StatusSub.Text = subtitle;
        StatusDot.Fill = new SolidColorBrush(healthy
            ? (Color)ColorConverter.ConvertFromString("#3fb950")
            : (Color)ColorConverter.ConvertFromString("#da3633"));
        StatusDot.Stroke = new SolidColorBrush(healthy
            ? (Color)ColorConverter.ConvertFromString("#2ea043")
            : (Color)ColorConverter.ConvertFromString("#f85149"));
    }
}
