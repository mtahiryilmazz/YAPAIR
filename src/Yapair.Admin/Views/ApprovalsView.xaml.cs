using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class ApprovalsView : Page
{
    private AdminApiClient? _api;
    public ObservableCollection<ApprovalItemModel> Items { get; } = new();
    private string? _currentStatusFilter = "pending";

    public ApprovalsView()
    {
        InitializeComponent();
        ApprovalsListView.ItemsSource = Items;
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

    private async void StatusSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusSelector.SelectedItem is RadioButton rb)
        {
            _currentStatusFilter = rb.Tag?.ToString();
            await LoadDataAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        if (_api == null) return;
        var res = await _api.GetApprovalsAsync(_currentStatusFilter);
        Items.Clear();

        if (res.HasValue && res.Value.TryGetProperty("items", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idp) ? idp.GetString() ?? "" : "";
                var machine = el.TryGetProperty("machine_key", out var mkp) ? mkp.GetString() ?? "" : "";
                var mode = el.TryGetProperty("requested_mode", out var rmp) ? rmp.GetString() ?? "" : "";
                var reason = el.TryGetProperty("reason", out var rsp) ? rsp.GetString() : null;
                var st = el.TryGetProperty("status", out var stp) ? stp.GetString() ?? "pending" : "pending";
                var code = el.TryGetProperty("approval_code", out var acp) ? acp.GetString() : null;
                var reqAt = el.TryGetProperty("requested_at", out var rap) ? rap.GetString() ?? "" : "";

                var isPending = st.Equals("pending", StringComparison.OrdinalIgnoreCase);

                var (statusBg, statusFg) = st.ToLowerInvariant() switch
                {
                    "approved" => ("#0d2a14", "#3fb950"),
                    "rejected" => ("#2a0d0d", "#f85149"),
                    _ => ("#2a1f0d", "#d29922")
                };

                var (modeBg, modeFg) = mode.ToUpperInvariant() switch
                {
                    "R3" => ("#2a0d0d", "#f85149"),
                    "R2" => ("#0d2a14", "#3fb950"),
                    _ => ("#21262d", "#8b949e")
                };

                Items.Add(new ApprovalItemModel
                {
                    Id = id,
                    MachineKey = machine,
                    RequestedMode = mode,
                    ReasonText = string.IsNullOrEmpty(reason) ? "Gerekçe belirtilmedi" : reason,
                    Status = st,
                    CodeDisplay = st == "approved" && !string.IsNullOrEmpty(code) ? $"PIN: {code}" : "-",
                    RequestedAt = reqAt.Length > 19 ? reqAt[..19].Replace("T", " ") : reqAt,
                    StatusBg = HexBrush(statusBg),
                    StatusFg = HexBrush(statusFg),
                    ModeBg = HexBrush(modeBg),
                    ModeFg = HexBrush(modeFg),
                    ActionVisibility = isPending ? Visibility.Visible : Visibility.Collapsed
                });
            }
        }

        SummaryText.Text = $"{Items.Count} talep listelendi";
    }

    private async void ApproveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not string id) return;

        var res = await _api.ApproveRequestAsync(id, "Admin");
        if (res.HasValue)
        {
            var code = res.Value.TryGetProperty("code", out var cp) ? cp.GetString() ?? "" : "";
            var dialog = new ContentDialog
            {
                Title = "Talep Onaylandı",
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = "Bu yetki geçişi başarıyla onaylandı.", Foreground = HexBrush("#3fb950") },
                        new TextBlock { Text = "Uç noktada girilecek 6 Haneli Onay Kodu (PIN):", FontSize = 12, Foreground = HexBrush("#8b949e") },
                        new Border
                        {
                            Background = HexBrush("#161b22"),
                            BorderBrush = HexBrush("#238636"),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(12),
                            Child = new TextBlock { Text = code, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = HexBrush("#3fb950"), HorizontalAlignment = HorizontalAlignment.Center }
                        },
                        new TextBlock { Text = "Bu kod 1 saat boyunca geçerlidir.", FontSize = 11, Foreground = HexBrush("#8b949e") }
                    }
                },
                CloseButtonText = "Kapat",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            await LoadDataAsync();
        }
    }

    private async void RejectBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not string id) return;

        var ok = await _api.RejectRequestAsync(id, "Admin");
        if (ok) await LoadDataAsync();
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

public sealed class ApprovalItemModel
{
    public string Id { get; set; } = "";
    public string MachineKey { get; set; } = "";
    public string RequestedMode { get; set; } = "";
    public string ReasonText { get; set; } = "";
    public string Status { get; set; } = "";
    public string CodeDisplay { get; set; } = "";
    public string RequestedAt { get; set; } = "";
    public SolidColorBrush? StatusBg { get; set; }
    public SolidColorBrush? StatusFg { get; set; }
    public SolidColorBrush? ModeBg { get; set; }
    public SolidColorBrush? ModeFg { get; set; }
    public Visibility ActionVisibility { get; set; }
}
