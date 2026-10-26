using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class OverviewView : Page
{
    private AdminApiClient? _api;

    public OverviewView()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is AdminApiClient client)
        {
            _api = client;
            await RefreshDataAsync();
        }
    }

    public async Task RefreshDataAsync()
    {
        if (_api == null) return;

        var health = await _api.GetHealthAsync();
        if (health.HasValue)
        {
            ServiceStatusText.Text = "Çalışıyor (Active)";
            ApiVersionText.Text = health.Value.TryGetProperty("version", out var v) ? v.GetString() ?? "v1" : "v1";
        }
        else
        {
            ServiceStatusText.Text = "Erişilemedi (Offline)";
        }

        var stats = await _api.GetStatsAsync();
        if (stats.HasValue)
        {
            var root = stats.Value;
            if (root.TryGetProperty("cases", out var cProp) && cProp.TryGetProperty("total", out var ct))
                CasesCountText.Text = ct.ToString();

            if (root.TryGetProperty("batches", out var bProp) && bProp.TryGetProperty("total", out var bt))
                BatchesCountText.Text = bt.ToString();

            if (root.TryGetProperty("machines", out var mProp) && mProp.TryGetProperty("total", out var mt))
                MachinesCountText.Text = mt.ToString();
        }

        var help = await _api.GetHelpRequestsAsync("open");
        if (help.HasValue && help.Value.TryGetProperty("total", out var ht))
            HelpCountText.Text = ht.ToString();
        else
            HelpCountText.Text = "0";
    }
}
