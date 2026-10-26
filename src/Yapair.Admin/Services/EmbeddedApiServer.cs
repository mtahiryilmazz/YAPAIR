using Microsoft.AspNetCore.Builder;
using Yapair.Api;

namespace Yapair.Admin.Services;

public static class EmbeddedApiServer
{
    private static WebApplication? _app;
    public static bool IsRunning { get; private set; }
    public static string ListenUrl { get; } = "http://localhost:5080";
    public static string StatusMessage { get; private set; } = "Başlatılmadı";

    public static async Task StartAsync()
    {
        // Önce portta zaten aktif bir servis var mı kontrol et
        try
        {
            using var pingClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            var ping = await pingClient.GetAsync(ListenUrl + "/health");
            if (ping.IsSuccessStatusCode)
            {
                IsRunning = true;
                StatusMessage = "API Aktif (Port 5080)";
                return;
            }
        }
        catch { }

        try
        {
            _app = ApiHost.BuildApp(listenUrl: ListenUrl);
            _ = _app.RunAsync();

            // Kestrel'in ayağa kalkmasını 1 saniye bekle ve doğrula
            await Task.Delay(1000);
            using var verifyClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var verify = await verifyClient.GetAsync(ListenUrl + "/health");
            if (verify.IsSuccessStatusCode)
            {
                IsRunning = true;
                StatusMessage = "API Aktif (Port 5080)";
            }
            else
            {
                IsRunning = false;
                StatusMessage = "API Yanıt Vermedi";
            }
        }
        catch (Exception ex)
        {
            IsRunning = false;
            StatusMessage = "Başlatma Hatası: " + ex.Message;
        }
    }

    public static async Task StopAsync()
    {
        if (_app != null)
        {
            try
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
            catch { }
            _app = null;
            IsRunning = false;
            StatusMessage = "Durduruldu";
        }
    }

    public static async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }
}
