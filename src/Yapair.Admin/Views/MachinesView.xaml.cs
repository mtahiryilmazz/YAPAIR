using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Yapair.Admin.Services;

namespace Yapair.Admin.Views;

public sealed partial class MachinesView : Page
{
    private AdminApiClient? _api;
    private readonly List<MachineItemModel> _allMachines = new();
    public ObservableCollection<MachineItemModel> FilteredMachines { get; } = new();

    public MachinesView()
    {
        InitializeComponent();
        MachinesListView.ItemsSource = FilteredMachines;
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

    private async Task LoadDataAsync()
    {
        if (_api == null) return;
        var stats = await _api.GetStatsAsync();
        _allMachines.Clear();

        if (stats.HasValue && stats.Value.TryGetProperty("machines", out var mProp) && mProp.TryGetProperty("recent", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                var key = el.TryGetProperty("machine_key", out var kp) ? kp.GetString() ?? "" : "";
                var label = el.TryGetProperty("machine_label", out var lp) ? lp.GetString() ?? key : key;
                var owner = el.TryGetProperty("owner", out var op) ? op.GetString() : null;
                var notes = el.TryGetProperty("notes", out var np) ? np.GetString() : null;
                var lastSeen = el.TryGetProperty("last_seen", out var lsp) ? lsp.GetString() ?? "" : "";

                _allMachines.Add(new MachineItemModel
                {
                    MachineKey = key,
                    MachineLabel = string.IsNullOrEmpty(label) ? key : label,
                    Owner = owner ?? "",
                    OwnerText = string.IsNullOrEmpty(owner) ? "Atanmamış" : owner,
                    Notes = notes ?? "",
                    NotesText = string.IsNullOrEmpty(notes) ? "Not yok" : notes,
                    LastSeen = lastSeen,
                    LastSeenText = lastSeen.Length > 19 ? lastSeen[..19].Replace("T", " ") : lastSeen
                });
            }
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        FilteredMachines.Clear();
        foreach (var m in _allMachines)
        {
            if (string.IsNullOrEmpty(q) ||
                m.MachineLabel.ToLowerInvariant().Contains(q) ||
                m.MachineKey.ToLowerInvariant().Contains(q) ||
                m.Owner.ToLowerInvariant().Contains(q) ||
                m.Notes.ToLowerInvariant().Contains(q))
            {
                FilteredMachines.Add(m);
            }
        }
        SummaryText.Text = $"{FilteredMachines.Count} makine listelendi";
    }

    private async void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not MachineItemModel item) return;

        var labelBox = new TextBox { Text = item.MachineLabel, PlaceholderText = "Makine Etiketi..." };
        var ownerBox = new TextBox { Text = item.Owner, PlaceholderText = "Kullanıcı / Departman..." };
        var notesBox = new TextBox { Text = item.Notes, PlaceholderText = "Makine notları...", AcceptsReturn = true, MinHeight = 60 };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Makine Adı / Etiketi:" },
                labelBox,
                new TextBlock { Text = "Atanan Sahip / Departman:" },
                ownerBox,
                new TextBlock { Text = "Notlar:" },
                notesBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = $"Makineyi Düzenle: {item.MachineLabel}",
            Content = panel,
            PrimaryButtonText = "Kaydet",
            CloseButtonText = "İptal",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var ok = await _api.UpdateMachineAsync(item.MachineKey, labelBox.Text.Trim(), notesBox.Text.Trim(), ownerBox.Text.Trim());
            if (ok) await LoadDataAsync();
        }
    }

    private async void RdpMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menu || menu.Tag is not MachineItemModel item) return;
        var target = await PromptTargetAddressAsync("RDP (Uzak Masaüstü)", item.MachineLabel);
        if (string.IsNullOrEmpty(target)) return;

        await RemoteDesktopService.ConnectRdpAsync(target, item.MachineLabel, async (act, note) =>
        {
            if (_api != null) await _api.LogAuditAsync(act, item.MachineKey, note);
        });
    }

    private async void RustDeskMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menu || menu.Tag is not MachineItemModel item) return;
        var target = await PromptTargetAddressAsync("RustDesk", item.MachineKey);
        if (string.IsNullOrEmpty(target)) return;

        await RemoteDesktopService.ConnectRustDeskAsync(target, item.MachineLabel, async (act, note) =>
        {
            if (_api != null) await _api.LogAuditAsync(act, item.MachineKey, note);
        });
    }

    private async void AnyDeskMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menu || menu.Tag is not MachineItemModel item) return;
        var target = await PromptTargetAddressAsync("AnyDesk", item.MachineKey);
        if (string.IsNullOrEmpty(target)) return;

        await RemoteDesktopService.ConnectAnyDeskAsync(target, item.MachineLabel, async (act, note) =>
        {
            if (_api != null) await _api.LogAuditAsync(act, item.MachineKey, note);
        });
    }

    private XamlRoot? SafeXamlRoot => App.MainWin?.Content?.XamlRoot ?? Content?.XamlRoot ?? XamlRoot;

    private async void RemoteRepairBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null || sender is not Button btn || btn.Tag is not MachineItemModel item) return;
        var xamlRoot = SafeXamlRoot;
        if (xamlRoot == null) return;

        var playbookCombo = new ComboBox
        {
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 0
        };
        playbookCombo.Items.Add(new ComboBoxItem { Content = "Yazıcı Biriktiricisi Yeniden Başlat (printers.spooler.restart.v1)", Tag = "printers.spooler.restart.v1" });
        playbookCombo.Items.Add(new ComboBoxItem { Content = "Yazıcı Kuyruğu Temizle (printers.queue.clear.v1)", Tag = "printers.queue.clear.v1" });
        playbookCombo.Items.Add(new ComboBoxItem { Content = "Windows Ses Hizmetini Yeniden Başlat (audio.restart.v1)", Tag = "audio.restart.v1" });
        playbookCombo.Items.Add(new ComboBoxItem { Content = "Ağ Bağdaştırıcısını Sıfırla (network.adapter.reset.v1)", Tag = "network.adapter.reset.v1" });
        playbookCombo.Items.Add(new ComboBoxItem { Content = "Kilitli Windows Servislerini Yeniden Başlat (services.controlled.restart.v1)", Tag = "services.controlled.restart.v1" });
        playbookCombo.Items.Add(new ComboBoxItem { Content = "Geçici Dosyaları Temizle (storage.temp.cleanup.v1)", Tag = "storage.temp.cleanup.v1" });

        var noteBox = new TextBox { PlaceholderText = "İsteğe bağlı not veya parametre...", AcceptsReturn = false };

        var panel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = $"Hedef Cihaz: {item.MachineLabel} ({item.MachineKey})", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = HexBrush("#58a6ff") },
                new TextBlock { Text = "Çalıştırılacak Onarım Playbook'u:" },
                playbookCombo,
                new TextBlock { Text = "Müdahale Notu:" },
                noteBox,
                new TextBlock { Text = "Not: Hedef cihazın arka plan ajanı bu komutu otomatik olarak alıp yürütecek ve sonucunu yönetici konsoluna geri raporlayacaktır.", FontSize = 11, Foreground = HexBrush("#8b949e"), TextWrapping = TextWrapping.Wrap }
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Uzaktan Müdahale ve Otomatik Onarım",
            Content = panel,
            PrimaryButtonText = "Müdahaleyi Gönder",
            CloseButtonText = "İptal",
            XamlRoot = xamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var selectedTag = (playbookCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "printers.spooler.restart.v1";
            var repairId = await _api.DispatchRemoteRepairAsync(item.MachineKey, selectedTag, "Admin", noteBox.Text.Trim());

            var resultMsg = string.IsNullOrEmpty(repairId)
                ? "Uzaktan müdahale komutu iletilemedi (API çevrimdışı)."
                : $"Uzaktan müdahale komutu kuyruğa eklendi!\nKomut ID: {repairId}\nCihaz ilk kontrolde komutu uygulayacaktır.";

            var notifyDialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(repairId) ? "Hata" : "Komut İletildi",
                Content = new TextBlock { Text = resultMsg, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "Tamam",
                XamlRoot = xamlRoot
            };
            await notifyDialog.ShowAsync();
        }
    }

    private async Task<string?> PromptTargetAddressAsync(string protocol, string defaultVal)
    {
        var xamlRoot = SafeXamlRoot;
        if (xamlRoot == null) return null;

        var box = new TextBox { Text = defaultVal, PlaceholderText = "IP adresi veya Cihaz ID..." };
        var dialog = new ContentDialog
        {
            Title = $"{protocol} Bağlantısı Başlat",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"Bağlanılacak IP veya Kimlik ({protocol}):" },
                    box
                }
            },
            PrimaryButtonText = "Bağlan",
            CloseButtonText = "İptal",
            XamlRoot = xamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            return string.IsNullOrWhiteSpace(box.Text) ? defaultVal : box.Text.Trim();
        }
        return null;
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_api == null) return;
        var xamlRoot = SafeXamlRoot;
        if (xamlRoot == null) return;

        var selected = MachinesListView.SelectedItems.Cast<MachineItemModel>().Select(x => x.MachineKey).ToList();
        if (selected.Count == 0) return;

        var confirm = new ContentDialog
        {
            Title = "Makineleri Sil",
            Content = $"{selected.Count} adet kayıtlı makineyi silmek istediğinizden emin misiniz?",
            PrimaryButtonText = "Evet, Sil",
            CloseButtonText = "İptal",
            XamlRoot = xamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var ok = await _api.DeleteMachinesAsync(selected);
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

public sealed class MachineItemModel
{
    public string MachineKey { get; set; } = "";
    public string MachineLabel { get; set; } = "";
    public string Owner { get; set; } = "";
    public string OwnerText { get; set; } = "";
    public string Notes { get; set; } = "";
    public string NotesText { get; set; } = "";
    public string LastSeen { get; set; } = "";
    public string LastSeenText { get; set; } = "";
}
