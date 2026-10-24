using System.IO;
using System.Text.Json;
using System.Windows;

namespace Yapair.Desktop;

public partial class MainWindow : Window
{
    private readonly string _repoRoot;
    private readonly string _cliDll;
    private readonly string _outcomeDir;

    private string? _lastCaseId;
    private string? _lastCaseJson;
    private string? _lastFindingsJson;
    private bool _outcomeSet;

    public MainWindow()
    {
        InitializeComponent();
        _repoRoot = CliBridge.FindRepoRoot();
        _cliDll = CliBridge.FindCliDll(_repoRoot);
        _outcomeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAPAIR", "outcomes");
        Directory.CreateDirectory(_outcomeDir);
        FooterText.Text = "Repo: " + _repoRoot + "    CLI: " + Path.GetFileName(_cliDll);
    }

    // ---------- Tara + Kaydet + Sync (tek tık) ----------
    private async void Tara_Click(object sender, RoutedEventArgs e)
    {
        var problem = ProblemBox.Text.Trim();
        if (string.IsNullOrEmpty(problem))
        {
            TaraStatus.Text = "Önce sorunu yazın.";
            return;
        }

        TaraBtn.IsEnabled = false;
        TaraStatus.Text = "Taranıyor... (30 sn'ye kadar sürebilir)";
        FindingsList.Items.Clear();
        EmptyHint.Visibility = Visibility.Collapsed;
        CopyBtn.IsEnabled = false;
        ExportBtn.IsEnabled = false;
        OutcomePanel.Visibility = Visibility.Collapsed;
        _outcomeSet = false;
        _lastCaseId = null;
        _lastCaseJson = null;
        _lastFindingsJson = null;

        try
        {
            // 1) Discover
            var (discExit, discOut, discErr) = await CliBridge.RunAsync(
                _cliDll, "discover --format json --pretty", TimeSpan.FromSeconds(60));

            if (string.IsNullOrWhiteSpace(discOut))
            {
                TaraStatus.Text = "Tarama çıktı üretmedi: " + discErr.Trim();
                return;
            }

            _lastFindingsJson = discOut;
            var findings = ParseFindings(discOut);
            if (findings.Count == 0)
            {
                EmptyHint.Text = "Bulgu yok.";
                EmptyHint.Visibility = Visibility.Visible;
            }
            else
            {
                foreach (var f in findings) FindingsList.Items.Add(f);
                CopyBtn.IsEnabled = true;
                ExportBtn.IsEnabled = true;
            }

            // 2) Kaydet
            var textEsc = problem.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var (addExit, addOut, addErr) = await CliBridge.RunAsync(
                _cliDll, $"cases add --text \"{textEsc}\" --format json", TimeSpan.FromSeconds(30));

            if (addExit != 0 || string.IsNullOrWhiteSpace(addOut))
            {
                TaraStatus.Text = $"Tarama tamam ama kayıt hatası: {addErr.Trim()}";
                return;
            }

            _lastCaseJson = addOut;
            _lastCaseId = ExtractCaseId(addOut);

            // 3) Sync
            var (syncOk, syncMsg) = await SyncCaseAsync(addOut, null, null);

            TaraStatus.Text = syncOk
                ? $" {findings.Count} bulgu  {_lastCaseId}  sunucuya gönderildi"
                : $" {findings.Count} bulgu  {_lastCaseId}  sync: {syncMsg}";

            OutcomePanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            TaraStatus.Text = "Hata: " + ex.Message;
        }
        finally
        {
            TaraBtn.IsEnabled = true;
        }
    }

    // ---------- Copy / Export ----------
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastFindingsJson)) return;
        try { Clipboard.SetText(_lastFindingsJson); TaraStatus.Text = "Bulgular panoya kopyalandı."; }
        catch (Exception ex) { TaraStatus.Text = "Kopyalanamadı: " + ex.Message; }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastFindingsJson)) return;
        try
        {
            var d = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"yapair-findings-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                DefaultExt = ".json",
                Filter = "JSON (*.json)|*.json|Metin (*.txt)|*.txt"
            };
            if (d.ShowDialog() == true)
            {
                File.WriteAllText(d.FileName, _lastFindingsJson);
                TaraStatus.Text = "Kaydedildi: " + d.FileName;
            }
        }
        catch (Exception ex) { TaraStatus.Text = "Aktarılamadı: " + ex.Message; }
    }

    // ---------- Outcome ----------
    private async void Evet_Click(object sender, RoutedEventArgs e) => await SetOutcomeAsync("resolved");
    private async void Hayir_Click(object sender, RoutedEventArgs e) => await SetOutcomeAsync("not_resolved");
    private async void EminDegil_Click(object sender, RoutedEventArgs e) => await SetOutcomeAsync("unsure");

    private async Task SetOutcomeAsync(string outcome)
    {
        if (_lastCaseId is null || _lastCaseJson is null) return;
        if (_outcomeSet) return;

        EvetBtn.IsEnabled = HayirBtn.IsEnabled = EminDegilBtn.IsEnabled = false;

        try
        {
            // Local
            var path = Path.Combine(_outcomeDir, _lastCaseId + ".json");
            var payload = new { case_id = _lastCaseId, outcome, saved_at = DateTimeOffset.UtcNow.ToString("O") };
            File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

            // Supabase (merge-duplicates ile satır güncellenir)
            var (ok, msg) = await SyncCaseAsync(_lastCaseJson, outcome, outcome);
            _outcomeSet = true;

            TaraStatus.Text = ok
                ? $" Sonuç kaydedildi: {outcome} (yerel + sunucu)"
                : $"Yerel sonuç kaydedildi ({outcome}), sunucu: {msg}";
        }
        catch (Exception ex)
        {
            TaraStatus.Text = "Sonuç kaydedilemedi: " + ex.Message;
            EvetBtn.IsEnabled = HayirBtn.IsEnabled = EminDegilBtn.IsEnabled = true;
        }
    }

    // ---------- Sync ----------
    private async Task<(bool Ok, string Message)> SyncCaseAsync(string caseJson, string? outcome, string? userConfirmation)
    {
        var env = LoadEnv();
        if (!env.TryGetValue("YAPAIR_API_KEY", out var apiKey) || string.IsNullOrEmpty(apiKey))
            return (false, "YAPAIR_API_KEY bulunamadı");
        var baseUrl = env.TryGetValue("YAPAIR_API_BASE_URL", out var u) && !string.IsNullOrEmpty(u)
            ? u : "http://localhost:5080";

        try
        {
            using var doc = JsonDocument.Parse(caseJson);
            var c = doc.RootElement;

            var payload = new
            {
                batch_id = "gui-" + Guid.NewGuid().ToString("N")[..12],
                cases = new[] {
                    new {
                        case_id = GetStr(c, "case_id", "caseId", "CaseId") ?? _lastCaseId ?? Guid.NewGuid().ToString("N"),
                        machine_key = GetStr(c, "machine_key", "machineKey", "MachineKey"),
                        machine_label = GetStr(c, "machine_label", "machineLabel", "MachineLabel") ?? Environment.MachineName,
                        created_at = GetStr(c, "created_at", "createdAt", "CreatedAt") ?? DateTimeOffset.UtcNow.ToString("O"),
                        normalized_text = GetStr(c, "normalized_text", "normalizedText", "NormalizedText") ?? "",
                        symptom_code = GetStr(c, "symptom_code", "symptomCode", "SymptomCode") ?? "user_reported",
                        symptom_label = GetStr(c, "symptom_label", "symptomLabel", "SymptomLabel") ?? "Kullanıcı bildirimi",
                        issue_family = GetStr(c, "issue_family", "issueFamily", "IssueFamily") ?? "general",
                        domain = GetStr(c, "domain", "Domain") ?? "user",
                        outcome = outcome,
                        playbook_id = GetStr(c, "playbook_id", "playbookId", "PlaybookId"),
                        user_confirmation = userConfirmation,
                        raw_text = (string?)null
                    }
                }
            };

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var req = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post, baseUrl.TrimEnd('/') + "/api/v1/cases");
            req.Headers.Add("X-Yapair-Api-Key", apiKey);
            req.Content = System.Net.Http.Json.JsonContent.Create(payload);
            using var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            return resp.IsSuccessStatusCode
                ? (true, "ok")
                : (false, $"HTTP {(int)resp.StatusCode}: {body}");
        }
        catch (System.Net.Http.HttpRequestException)
        {
            return (false, "API çalışmıyor (Launcher  Başlat)");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ---------- Helpers ----------
    private Dictionary<string, string> LoadEnv()
    {
        var path = Path.Combine(_repoRoot, "supabase", ".env.local");
        var dict = new Dictionary<string, string>();
        if (!File.Exists(path)) return dict;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var i = line.IndexOf('=');
            if (i < 1) continue;
            dict[line[..i].Trim()] = line[(i + 1)..].Trim().Trim('"').Trim('\'');
        }
        return dict;
    }

    private static string? ExtractCaseId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var key in new[] { "case_id", "caseId", "CaseId" })
                if (doc.RootElement.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
                    return el.GetString();
        }
        catch { }
        return null;
    }

    private static string? GetStr(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.ToString();
            }
        return null;
    }

    private static List<FindingItem> ParseFindings(string json)
    {
        var result = new List<FindingItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryGetArray(doc.RootElement, out var arr, "findings", "Findings", "issues", "Issues"))
                return result;

            foreach (var item in arr.EnumerateArray())
            {
                result.Add(new FindingItem
                {
                    Severity = GetStr(item, "severity", "Severity") ?? "",
                    Code = GetStr(item, "code", "Code", "symptom_code") ?? "",
                    Source = GetStr(item, "source", "Source") ?? "",
                    Playbook = GetStr(item, "playbook_id", "PlaybookId") ?? ""
                });
            }
        }
        catch { }
        return result;
    }

    private static bool TryGetArray(JsonElement root, out JsonElement arr, params string[] names)
    {
        foreach (var n in names)
            if (root.TryGetProperty(n, out var el) && el.ValueKind == JsonValueKind.Array)
            { arr = el; return true; }
        arr = default; return false;
    }

    public sealed class FindingItem
    {
        public string Severity { get; init; } = "";
        public string Code { get; init; } = "";
        public string Source { get; init; } = "";
        public string Playbook { get; init; } = "";
    }
}
