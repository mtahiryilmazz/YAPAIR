using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class BatchesView : Page
{
    private AdminApiClient? _api;
    public ObservableCollection<BatchItemModel> Items { get; } = new();

    public BatchesView()
    {
        InitializeComponent();
        BatchesListView.ItemsSource = Items;
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

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        if (_api == null) return;
        var stats = await _api.GetStatsAsync();
        Items.Clear();

        if (stats.HasValue && stats.Value.TryGetProperty("batches", out var bProp) && bProp.TryGetProperty("recent", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("batch_id", out var idp) ? idp.GetString() ?? "" : "";
                var count = el.TryGetProperty("case_count", out var cp) ? cp.GetInt32() : 0;
                var ver = el.TryGetProperty("api_version", out var vp) ? vp.GetString() ?? "-" : "-";
                var rec = el.TryGetProperty("received_at", out var rp) ? rp.GetString() ?? "" : "";

                Items.Add(new BatchItemModel
                {
                    BatchId = id,
                    CaseCountText = $"{count} Vaka",
                    ApiVersion = ver,
                    ReceivedAt = rec.Length > 19 ? rec[..19].Replace("T", " ") : rec
                });
            }
        }
        SummaryText.Text = $"{Items.Count} batch listelendi";
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;
        var selected = BatchesListView.SelectedItems.Cast<BatchItemModel>().Select(x => x.BatchId).ToList();
        if (selected.Count == 0) return;

        var confirm = new ContentDialog
        {
            Title = "Batch Kayıtlarını Sil",
            Content = $"{selected.Count} adet senkronizasyon batch kaydını silmek istediğinizden emin misiniz?",
            PrimaryButtonText = "Evet, Sil",
            CloseButtonText = "İptal",
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var ok = await _api.DeleteBatchesAsync(selected);
            if (ok) await LoadDataAsync();
        }
    }
}

public sealed class BatchItemModel
{
    public string BatchId { get; set; } = "";
    public string CaseCountText { get; set; } = "";
    public string ApiVersion { get; set; } = "";
    public string ReceivedAt { get; set; } = "";
}
