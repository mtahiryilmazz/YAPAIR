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

public sealed partial class AuditView : Page
{
    private AdminApiClient? _api;
    private readonly List<AuditItemModel> _allAudits = new();
    public ObservableCollection<AuditItemModel> FilteredAudits { get; } = new();

    public AuditView()
    {
        InitializeComponent();
        AuditListView.ItemsSource = FilteredAudits;
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
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private async void ActionFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        if (_api == null) return;
        var act = (ActionFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var audits = await _api.GetAuditsAsync(action: string.IsNullOrEmpty(act) ? null : act);
        _allAudits.Clear();

        if (audits.HasValue && audits.Value.TryGetProperty("items", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                var time = el.TryGetProperty("created_at", out var tp) ? tp.GetString() ?? "" : "";
                var machine = el.TryGetProperty("machine_key", out var mp) ? mp.GetString() ?? "" : "";
                var action = el.TryGetProperty("action", out var ap) ? ap.GetString() ?? "" : "";
                var target = el.TryGetProperty("target_id", out var tip) ? tip.GetString() ?? "-" : "-";
                var note = el.TryGetProperty("note", out var np) ? np.GetString() ?? "" : "";

                var (bg, fg) = action switch
                {
                    "repair_action" => ("#0d2a14", "#3fb950"),
                    "mode_change" => ("#2a1f0d", "#d29922"),
                    var a when a.StartsWith("remote_desktop") => ("#0d1d3a", "#58a6ff"),
                    _ => ("#21262d", "#8b949e")
                };

                _allAudits.Add(new AuditItemModel
                {
                    CreatedAt = time.Length > 19 ? time[..19].Replace("T", " ") : time,
                    MachineKey = machine,
                    Action = action,
                    TargetId = target,
                    Note = note,
                    ActionBg = HexBrush(bg),
                    ActionFg = HexBrush(fg)
                });
            }
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        FilteredAudits.Clear();
        foreach (var a in _allAudits)
        {
            if (string.IsNullOrEmpty(q) ||
                a.MachineKey.ToLowerInvariant().Contains(q) ||
                a.Note.ToLowerInvariant().Contains(q) ||
                a.TargetId.ToLowerInvariant().Contains(q))
            {
                FilteredAudits.Add(a);
            }
        }
        SummaryText.Text = $"{FilteredAudits.Count} audit kaydı listelendi";
    }

    private void ExportCsvBtn_Click(object sender, RoutedEventArgs e)
    {
        if (FilteredAudits.Count == 0) return;
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("CreatedAt,MachineKey,Action,TargetId,Note");
            foreach (var a in FilteredAudits)
            {
                csv.AppendLine($"\"{a.CreatedAt}\",\"{a.MachineKey}\",\"{a.Action}\",\"{a.TargetId}\",\"{a.Note}\"");
            }
            var path = Path.Combine(Path.GetTempPath(), $"yapair_audit_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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

public sealed class AuditItemModel
{
    public string CreatedAt { get; set; } = "";
    public string MachineKey { get; set; } = "";
    public string Action { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Note { get; set; } = "";
    public SolidColorBrush? ActionBg { get; set; }
    public SolidColorBrush? ActionFg { get; set; }
}
