using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class ResearchView : Page
{
    private AdminApiClient? _api;
    public ObservableCollection<SearchSourceModel> Sources { get; } = new();

    public ResearchView()
    {
        InitializeComponent();
        SourcesList.ItemsSource = Sources;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is AdminApiClient client)
        {
            _api = client;
            await LoadAiConfigAsync();
        }
    }

    private async Task LoadAiConfigAsync()
    {
        if (_api == null) return;
        try
        {
            var res = await _api.GetAiConfigAsync();
            if (res.HasValue)
            {
                var root = res.Value;
                var hasKey = root.TryGetProperty("has_key", out var hk) && hk.GetBoolean();
                var masked = root.TryGetProperty("masked_key", out var mk) ? mk.GetString() ?? "" : "";
                var model = root.TryGetProperty("model", out var mp) ? mp.GetString() ?? "gemini-2.5-flash" : "gemini-2.5-flash";
                var ddg = !root.TryGetProperty("enable_duckduckgo", out var dp) || dp.GetBoolean();
                var web = !root.TryGetProperty("enable_web_research", out var wp) || wp.GetBoolean();

                if (hasKey)
                {
                    KeyStatusBadge.Background = HexBrush("#0d2a14");
                    KeyStatusText.Text = $"Aktif: {masked}";
                    KeyStatusText.Foreground = HexBrush("#3fb950");
                }
                else
                {
                    KeyStatusBadge.Background = HexBrush("#2a1f0d");
                    KeyStatusText.Text = "Anahtar Eksik (Webden Çözüm Kapalı)";
                    KeyStatusText.Foreground = HexBrush("#d29922");
                }

                DuckDuckGoSwitch.IsOn = ddg;
                WebResearchSwitch.IsOn = web;

                // Select model in combo
                foreach (var item in ModelCombo.Items)
                {
                    if (item is ComboBoxItem cbi && cbi.Tag?.ToString() == model)
                    {
                        ModelCombo.SelectedItem = cbi;
                        break;
                    }
                }
            }
        }
        catch { }
    }

    private async void SaveConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;
        var key = ApiKeyBox.Password?.Trim();
        var selectedModel = (ModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "gemini-2.5-flash";
        var ddg = DuckDuckGoSwitch.IsOn;
        var web = WebResearchSwitch.IsOn;

        var ok = await _api.UpdateAiConfigAsync(
            string.IsNullOrEmpty(key) ? null : key,
            selectedModel,
            ddg,
            web);

        if (ok)
        {
            ApiKeyBox.Password = "";
            await LoadAiConfigAsync();
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Message = "AI ve Web Araştırması yapılandırması başarıyla kaydedildi. İstemciler artık güvenli şekilde çözümlere erişebilir.";
            StatusInfoBar.IsOpen = true;
        }
        else
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Message = "Yapılandırma kaydedilemedi.";
            StatusInfoBar.IsOpen = true;
        }
    }

    private async void TestConnectionBtn_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Text = "windows update error 0x80070002";
        SearchBtn_Click(sender, e);
    }

    private void ToggleConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigBodyPanel.Visibility == Visibility.Visible)
        {
            ConfigBodyPanel.Visibility = Visibility.Collapsed;
            ConfigChevronIcon.Glyph = "\uE70D"; // ChevronDown
            ConfigToggleText.Text = "Genişlet";
        }
        else
        {
            ConfigBodyPanel.Visibility = Visibility.Visible;
            ConfigChevronIcon.Glyph = "\uE70E"; // ChevronUp
            ConfigToggleText.Text = "Daralt";
        }
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush HexBrush(string hex)
    {
        if (hex.StartsWith("#") && hex.Length == 7)
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255,
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16)));
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 148, 158));
    }

    private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SearchBtn_Click(sender, e);
        }
    }

    private async void SearchBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;
        var query = QueryBox.Text?.Trim();
        if (string.IsNullOrEmpty(query)) return;

        SearchBtn.IsEnabled = false;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        SearchIcon.Visibility = Visibility.Collapsed;
        SearchBtnText.Text = "Araştırılıyor...";
        StatusInfoBar.IsOpen = false;
        ResultCard.Visibility = Visibility.Collapsed;
        Sources.Clear();

        try
        {
            var res = await _api.ResearchAsync(query);
            if (res.HasValue)
            {
                var root = res.Value;
                var answer = root.TryGetProperty("answer", out var ap) ? ap.GetString() ?? "" : "";
                AnswerTextBlock.Text = answer;

                if (root.TryGetProperty("sources", out var sp) && sp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sp.EnumerateArray())
                    {
                        var title = s.TryGetProperty("title", out var tp) ? tp.GetString() ?? "" : "";
                        var snippet = s.TryGetProperty("snippet", out var snp) ? snp.GetString() ?? "" : "";
                        Sources.Add(new SearchSourceModel { Title = title, Snippet = snippet });
                    }
                }

                ResultCard.Visibility = Visibility.Visible;
            }
            else
            {
                StatusInfoBar.Message = "Araştırma servisine erişilemedi veya GEMINI_API_KEY yapılandırılmamış.";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            StatusInfoBar.Message = "Hata: " + ex.Message;
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            SearchBtn.IsEnabled = true;
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            SearchIcon.Visibility = Visibility.Visible;
            SearchBtnText.Text = "Araştır (AI)";
        }
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AnswerTextBlock.Text)) return;
        var dp = new DataPackage();
        dp.SetText(AnswerTextBlock.Text);
        Clipboard.SetContent(dp);
    }
}

public sealed class SearchSourceModel
{
    public string Title { get; set; } = "";
    public string Snippet { get; set; } = "";
}
