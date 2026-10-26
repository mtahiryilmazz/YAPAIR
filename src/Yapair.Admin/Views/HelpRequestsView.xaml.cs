using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class HelpRequestsView : Page
{
    private AdminApiClient? _api;
    public ObservableCollection<HelpItemModel> Items { get; } = new();

    public HelpRequestsView()
    {
        InitializeComponent();
        HelpListView.ItemsSource = Items;
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
    private async void StatusFilter_Changed(object sender, SelectionChangedEventArgs e) => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        if (_api == null) return;
        var filter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        var res = await _api.GetHelpRequestsAsync(string.IsNullOrEmpty(filter) ? null : filter);
        Items.Clear();

        if (res.HasValue && res.Value.TryGetProperty("items", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;
                var st = el.TryGetProperty("status", out var stProp) ? stProp.GetString() ?? "open" : "open";
                var machine = el.TryGetProperty("machine_label", out var mProp) ? mProp.GetString() ?? "Bilinmeyen" : "Bilinmeyen";
                var problem = el.TryGetProperty("problem_text", out var pProp) ? pProp.GetString() ?? "" : "";
                var time = el.TryGetProperty("created_at", out var tProp) ? tProp.GetString() ?? "" : "";
                var note = el.TryGetProperty("admin_note", out var nProp) ? nProp.GetString() : null;

                var (bg, fg) = st switch
                {
                    "resolved" => ("#0d2a14", "#3fb950"),
                    "closed" => ("#21262d", "#8b949e"),
                    "in_progress" => ("#2a1f0d", "#d29922"),
                    _ => ("#2a0d0d", "#f85149")
                };

                Items.Add(new HelpItemModel
                {
                    Id = id,
                    MachineLabel = machine,
                    ProblemText = problem,
                    Status = st,
                    CreatedAt = time.Length > 19 ? time[..19].Replace("T", " ") : time,
                    AdminNoteText = string.IsNullOrEmpty(note) ? "Not yok" : "Not: " + note,
                    StatusBg = HexBrush(bg),
                    StatusFg = HexBrush(fg)
                });
            }
        }
    }

    private async void UpdateStatusBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not long id) return;

        var combo = new ComboBox { Width = 200, Margin = new Thickness(0, 8, 0, 8) };
        combo.Items.Add(new ComboBoxItem { Content = "Açık (open)", Tag = "open" });
        combo.Items.Add(new ComboBoxItem { Content = "İşlemde (in_progress)", Tag = "in_progress" });
        combo.Items.Add(new ComboBoxItem { Content = "Çözüldü (resolved)", Tag = "resolved" });
        combo.Items.Add(new ComboBoxItem { Content = "Kapandı (closed)", Tag = "closed" });
        combo.SelectedIndex = 1;

        var noteBox = new TextBox { PlaceholderText = "Admin notu (opsiyonel)...", MinHeight = 60, AcceptsReturn = true };
        var panel = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "Yeni Durum:" }, combo, new TextBlock { Text = "Not:" }, noteBox } };

        var dialog = new ContentDialog
        {
            Title = $"Yardım Talebini Güncelle #{id}",
            Content = panel,
            PrimaryButtonText = "Güncelle",
            CloseButtonText = "İptal",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var newStatus = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "in_progress";
            var note = string.IsNullOrWhiteSpace(noteBox.Text) ? null : noteBox.Text.Trim();
            var ok = await _api.UpdateHelpStatusAsync(id, newStatus, note);
            if (ok) await LoadDataAsync();
        }
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

public sealed class HelpItemModel
{
    public long Id { get; set; }
    public string MachineLabel { get; set; } = "";
    public string ProblemText { get; set; } = "";
    public string Status { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string AdminNoteText { get; set; } = "";
    public SolidColorBrush? StatusBg { get; set; }
    public SolidColorBrush? StatusFg { get; set; }
}
