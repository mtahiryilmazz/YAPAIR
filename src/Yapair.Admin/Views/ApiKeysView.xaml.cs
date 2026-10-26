using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class ApiKeysView : Page
{
    private AdminApiClient? _api;
    public ObservableCollection<ApiKeyItemModel> Items { get; } = new();

    public ApiKeysView()
    {
        InitializeComponent();
        KeysListView.ItemsSource = Items;
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
        var res = await _api.GetApiKeysAsync();
        Items.Clear();

        if (res.HasValue && res.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in res.Value.EnumerateArray())
            {
                var id = el.TryGetProperty("key_id", out var idp) ? idp.GetString() ?? "" : "";
                var name = el.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                var role = el.TryGetProperty("role", out var rp) ? rp.GetString() ?? "technician" : "technician";
                var created = el.TryGetProperty("created_at", out var cp) ? cp.GetString() ?? "" : "";
                var expires = el.TryGetProperty("expires_at", out var ep) ? ep.GetString() : null;
                var revoked = el.TryGetProperty("revoked", out var rvp) && rvp.GetBoolean();

                var (roleBg, roleFg) = role.ToLowerInvariant() switch
                {
                    "admin" => ("#2a0d0d", "#f85149"),
                    "technician" => ("#2a1f0d", "#d29922"),
                    _ => ("#0d1d3a", "#58a6ff")
                };

                var statusText = revoked ? "İptal Edildi" : "Aktif";
                var (statusBg, statusFg) = revoked ? ("#2a0d0d", "#f85149") : ("#0d2a14", "#3fb950");

                Items.Add(new ApiKeyItemModel
                {
                    KeyId = id,
                    Name = name,
                    RoleText = role.ToUpperInvariant(),
                    CreatedAtText = "Oluşturuldu: " + (created.Length > 19 ? created[..19].Replace("T", " ") : created),
                    ExpiresAtText = string.IsNullOrEmpty(expires) ? "Süresiz" : "Bitiş: " + (expires.Length > 19 ? expires[..19].Replace("T", " ") : expires),
                    StatusText = statusText,
                    RoleBg = HexBrush(roleBg),
                    RoleFg = HexBrush(roleFg),
                    StatusBg = HexBrush(statusBg),
                    StatusFg = HexBrush(statusFg),
                    ActionVisibility = revoked ? Visibility.Collapsed : Visibility.Visible
                });
            }
        }

        SummaryText.Text = $"{Items.Count} anahtar listelendi";
    }

    private async void CreateKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;

        var nameBox = new TextBox { PlaceholderText = "Örn: Ankara Bölge Teknisyeni" };
        var roleCombo = new ComboBox { Width = 220, Margin = new Thickness(0, 4, 0, 8) };
        roleCombo.Items.Add(new ComboBoxItem { Content = "Teknisyen (Technician)", Tag = "technician" });
        roleCombo.Items.Add(new ComboBoxItem { Content = "Yönetici (Admin)", Tag = "admin" });
        roleCombo.Items.Add(new ComboBoxItem { Content = "Salt Okunur (ReadOnly)", Tag = "readonly" });
        roleCombo.SelectedIndex = 0;

        var daysBox = new TextBox { PlaceholderText = "Geçerlilik (gün, 0 = süresiz)", Text = "90" };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Anahtar Tanımı / Adı:" },
                nameBox,
                new TextBlock { Text = "Kullanıcı Rolü (RBAC):" },
                roleCombo,
                new TextBlock { Text = "Süre (Gün):" },
                daysBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Yeni API Anahtarı Oluştur",
            Content = panel,
            PrimaryButtonText = "Oluştur",
            CloseButtonText = "İptal",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var name = nameBox.Text?.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var role = (roleCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "technician";
            _ = int.TryParse(daysBox.Text, out var days);

            var res = await _api.CreateApiKeyAsync(name, role, days > 0 ? days : null);
            if (res.HasValue && res.Value.TryGetProperty("raw_key", out var rkp))
            {
                var secret = rkp.GetString() ?? "";
                await ShowSecretKeyDialogAsync("Yeni API Anahtarı Oluşturuldu", secret);
                await LoadDataAsync();
            }
        }
    }

    private async void RotateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not string id) return;

        var confirm = new ContentDialog
        {
            Title = "Anahtarı Döndür (Rotate)",
            Content = "Mevcut anahtar anında iptal edilecek ve aynı role sahip yeni bir gizli anahtar oluşturulacaktır. Onaylıyor musunuz?",
            PrimaryButtonText = "Döndür",
            CloseButtonText = "İptal",
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var res = await _api.RotateApiKeyAsync(id);
            if (res.HasValue && res.Value.TryGetProperty("raw_key", out var rkp))
            {
                var secret = rkp.GetString() ?? "";
                await ShowSecretKeyDialogAsync("Anahtar Döndürüldü", secret);
                await LoadDataAsync();
            }
        }
    }

    private async void RevokeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not string id) return;

        var confirm = new ContentDialog
        {
            Title = "Anahtarı İptal Et (Revoke)",
            Content = "Bu API anahtarı kalıcı olarak iptal edilecektir. Bu anahtarı kullanan cihazlar erişim sağlayamayacaktır.",
            PrimaryButtonText = "Evet, İptal Et",
            CloseButtonText = "Vazgeç",
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var ok = await _api.RevokeApiKeyAsync(id);
            if (ok) await LoadDataAsync();
        }
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not string id) return;

        var confirm = new ContentDialog
        {
            Title = "API Anahtarını Sil",
            Content = $"'{id}' tanımlı API anahtarı sistemden tamamen silinecektir. Bu işlem geri alınamaz.",
            PrimaryButtonText = "Evet, Tamamen Sil",
            CloseButtonText = "Vazgeç",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var ok = await _api.DeleteApiKeyAsync(id);
            if (ok) await LoadDataAsync();
        }
    }

    private async Task ShowSecretKeyDialogAsync(string title, string rawKey)
    {
        var box = new TextBox { Text = rawKey, IsReadOnly = true, Margin = new Thickness(0, 8, 0, 8) };
        var copyBtn = new Button { Content = "Panoya Kopyala" };
        copyBtn.Click += (s, ev) =>
        {
            var dp = new DataPackage();
            dp.SetText(rawKey);
            Clipboard.SetContent(dp);
            copyBtn.Content = "Kopyalandı!";
        };

        var panel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Bu anahtar yalnızca BİR KEZ gösterilir. Lütfen güvenli bir yere kaydedin:", Foreground = HexBrush("#d29922") },
                box,
                copyBtn
            }
        };

        var dlg = new ContentDialog
        {
            Title = title,
            Content = panel,
            CloseButtonText = "Kapat",
            XamlRoot = XamlRoot
        };
        await dlg.ShowAsync();
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

public sealed class ApiKeyItemModel
{
    public string KeyId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RoleText { get; set; } = "";
    public string CreatedAtText { get; set; } = "";
    public string ExpiresAtText { get; set; } = "";
    public string StatusText { get; set; } = "";
    public SolidColorBrush? RoleBg { get; set; }
    public SolidColorBrush? RoleFg { get; set; }
    public SolidColorBrush? StatusBg { get; set; }
    public SolidColorBrush? StatusFg { get; set; }
    public Visibility ActionVisibility { get; set; }
}
