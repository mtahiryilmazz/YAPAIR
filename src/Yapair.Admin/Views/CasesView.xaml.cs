using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class CasesView : Page
{
    private AdminApiClient? _api;
    private readonly List<CaseItemModel> _allCases = new();
    public ObservableCollection<CaseItemModel> FilteredCases { get; } = new();
    private bool _isArchivedTab;

    public CasesView()
    {
        InitializeComponent();
        CasesListView.ItemsSource = FilteredCases;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is AdminApiClient client)
        {
            _api = client;
            await LoadDataAsync();
        }
    }

    private async void TabSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabSelector.SelectedItem is RadioButton rb)
        {
            _isArchivedTab = rb.Tag?.ToString() == "archived";
            ArchiveBtnText.Text = _isArchivedTab ? "Arşivden Çıkar" : "Arşivle";
            await LoadDataAsync();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        if (_api == null) return;
        var stats = await _api.GetStatsAsync(_isArchivedTab);
        _allCases.Clear();

        if (stats.HasValue && stats.Value.TryGetProperty("cases", out var cProp) && cProp.TryGetProperty("recent", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("case_id", out var idp) ? idp.GetString() ?? "" : "";
                var machine = el.TryGetProperty("machines", out var mp) && mp.ValueKind == JsonValueKind.Object && mp.TryGetProperty("machine_label", out var mlp)
                    ? mlp.GetString() ?? ""
                    : (el.TryGetProperty("machine_key", out var mkp) ? mkp.GetString() ?? "" : "");
                var symptomLabel = el.TryGetProperty("symptom_label", out var slp) ? slp.GetString() ?? "" : "";
                var normalizedText = el.TryGetProperty("normalized_text", out var ntp) ? ntp.GetString() ?? "" : "";
                var playbookId = el.TryGetProperty("playbook_id", out var pip) ? pip.GetString() ?? "-" : "-";
                var outcome = el.TryGetProperty("outcome", out var op) ? op.GetString() ?? "unknown" : "unknown";
                var receivedAt = el.TryGetProperty("received_at", out var rp) ? rp.GetString() ?? "" : "";
                var rawText = el.TryGetProperty("raw_text", out var rtp) ? rtp.GetString() ?? "" : "";

                var (bg, fg) = outcome switch
                {
                    "resolved" => ("#0d2a14", "#3fb950"),
                    "failed" => ("#2a0d0d", "#f85149"),
                    "no_issue" => ("#21262d", "#8b949e"),
                    _ => ("#2a1f0d", "#d29922")
                };

                _allCases.Add(new CaseItemModel
                {
                    CaseId = id,
                    MachineLabel = machine,
                    SymptomLabel = string.IsNullOrEmpty(symptomLabel) ? (string.IsNullOrEmpty(normalizedText) ? id : normalizedText) : symptomLabel,
                    NormalizedText = normalizedText,
                    PlaybookId = playbookId,
                    Outcome = outcome,
                    ReceivedAt = receivedAt.Length > 19 ? receivedAt[..19].Replace("T", " ") : receivedAt,
                    RawText = rawText,
                    OutcomeBg = HexBrush(bg),
                    OutcomeFg = HexBrush(fg)
                });
            }
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        FilteredCases.Clear();
        foreach (var c in _allCases)
        {
            if (string.IsNullOrEmpty(q) ||
                c.CaseId.ToLowerInvariant().Contains(q) ||
                c.MachineLabel.ToLowerInvariant().Contains(q) ||
                c.SymptomLabel.ToLowerInvariant().Contains(q) ||
                c.NormalizedText.ToLowerInvariant().Contains(q))
            {
                FilteredCases.Add(c);
            }
        }
        SummaryText.Text = $"{FilteredCases.Count} vaka listelendi ({(_isArchivedTab ? "Arşiv" : "Aktif")})";
    }

    private XamlRoot? SafeXamlRoot => App.MainWin?.Content?.XamlRoot ?? Content?.XamlRoot ?? XamlRoot;

    private void SelectAll_Checked(object sender, RoutedEventArgs e) => CasesListView.SelectAll();
    private void SelectAll_Unchecked(object sender, RoutedEventArgs e) => CasesListView.SelectedItems.Clear();

    private async void SeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;
        var ok = await _api.SeedDemoDataAsync();
        if (ok) await LoadDataAsync();
    }

    private void CasesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectAllCheckBox != null && CasesListView.Items.Count > 0)
        {
            SelectAllCheckBox.IsChecked = CasesListView.SelectedItems.Count == CasesListView.Items.Count;
        }
    }

    private async void ArchiveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;
        var selected = CasesListView.SelectedItems.Cast<CaseItemModel>().Select(x => x.CaseId).ToList();
        if (selected.Count == 0)
        {
            await ShowMessageAsync("Seçim Yok", "Lütfen arşivlemek veya arşivden çıkarmak için vaka seçin.");
            return;
        }

        bool ok = _isArchivedTab ? await _api.UnarchiveCasesAsync(selected) : await _api.ArchiveCasesAsync(selected);
        if (ok) await LoadDataAsync();
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;
        var selected = CasesListView.SelectedItems.Cast<CaseItemModel>().Select(x => x.CaseId).ToList();
        if (selected.Count == 0)
        {
            await ShowMessageAsync("Seçim Yok", "Lütfen silinecek vakaları seçin.");
            return;
        }

        var xamlRoot = SafeXamlRoot;
        if (xamlRoot == null) return;

        var confirm = new ContentDialog
        {
            Title = "Vakaları Sil",
            Content = $"{selected.Count} adet vakayı kalıcı olarak silmek istediğinizden emin misiniz?",
            PrimaryButtonText = "Evet, Sil",
            CloseButtonText = "İptal",
            XamlRoot = xamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var ok = await _api.DeleteCasesAsync(selected);
            if (ok) await LoadDataAsync();
        }
    }

    private void DetailBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string caseId)
        {
            _ = ShowCaseDetailsAsync(caseId);
        }
    }

    private async Task ShowCaseDetailsAsync(string caseId)
    {
        if (_api == null) return;
        var xamlRoot = SafeXamlRoot;
        if (xamlRoot == null) return;

        var data = await _api.GetCaseAsync(caseId);
        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };

        if (data.HasValue)
        {
            foreach (var p in data.Value.EnumerateObject())
            {
                var row = new StackPanel { Spacing = 2 };
                row.Children.Add(new TextBlock { Text = p.Name, FontSize = 11, Foreground = HexBrush("#8b949e"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                row.Children.Add(new TextBlock { Text = p.Value.ToString(), FontSize = 13, Foreground = HexBrush("#f0f6fc"), TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(row);
            }

            var printBtn = new Button
            {
                Content = "Raporu Tarayıcıda Aç / Yazdır",
                Margin = new Thickness(0, 10, 0, 0),
                Background = HexBrush("#21262d"),
                Foreground = HexBrush("#58a6ff")
            };
            printBtn.Click += (s, ev) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"http://localhost:5080/admin/print?id={Uri.EscapeDataString(caseId)}",
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            panel.Children.Add(printBtn);
        }
        else
        {
            panel.Children.Add(new TextBlock { Text = "Vaka detayı alınamadı.", Foreground = HexBrush("#f85149") });
        }

        var dialog = new ContentDialog
        {
            Title = $"Vaka Detayı: {caseId}",
            Content = new ScrollViewer { Content = panel, MaxHeight = 460 },
            CloseButtonText = "Kapat",
            XamlRoot = xamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ShowMessageAsync(string title, string msg)
    {
        var xamlRoot = SafeXamlRoot;
        if (xamlRoot == null) return;

        var d = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = msg, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Tamam",
            XamlRoot = xamlRoot
        };
        await d.ShowAsync();
    }

    private void ExportCsvBtn_Click(object sender, RoutedEventArgs e)
    {
        if (FilteredCases.Count == 0) return;
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("CaseId,Machine,Symptom,NormalizedText,Playbook,Outcome,ReceivedAt");
            foreach (var c in FilteredCases)
            {
                csv.AppendLine($"\"{c.CaseId}\",\"{c.MachineLabel}\",\"{c.SymptomLabel}\",\"{c.NormalizedText}\",\"{c.PlaybookId}\",\"{c.Outcome}\",\"{c.ReceivedAt}\"");
            }
            var path = Path.Combine(Path.GetTempPath(), $"yapair_cases_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { }
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

public sealed class CaseItemModel
{
    public string CaseId { get; set; } = "";
    public string MachineLabel { get; set; } = "";
    public string SymptomLabel { get; set; } = "";
    public string NormalizedText { get; set; } = "";
    public string PlaybookId { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string ReceivedAt { get; set; } = "";
    public string RawText { get; set; } = "";
    public SolidColorBrush? OutcomeBg { get; set; }
    public SolidColorBrush? OutcomeFg { get; set; }
}
