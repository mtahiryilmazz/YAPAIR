$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$ts = Get-Date -Format "yyyyMMdd-HHmmss"
Write-Host "=== FAZ 1: Servis + Yardim Talebi + Startup ===" -ForegroundColor Cyan
Write-Host ""

# ============ 1) Migration ============
$mig = "supabase\migrations\20261024200000_help_requests.sql"
if (-not (Test-Path $mig)) {
    @'
create table if not exists public.help_requests (
    id           bigserial primary key,
    machine_key  text,
    machine_label text,
    case_id      text,
    problem_text text not null,
    status       text not null default 'open'
                 check (status in ('open','in_progress','resolved','closed')),
    assigned_to  text,
    admin_note   text,
    created_at   timestamptz not null default now(),
    updated_at   timestamptz not null default now()
);

create index if not exists idx_help_status on public.help_requests (status);
create index if not exists idx_help_created on public.help_requests (created_at desc);

alter table public.help_requests enable row level security;
alter table public.help_requests force row level security;
revoke all on public.help_requests from anon, authenticated;
grant select, insert, update, delete on public.help_requests to service_role;

comment on table public.help_requests is
    'Kullanici yardim talepleri - IT ekibi admin panelden gorur';
'@ | Set-Content $mig -Encoding UTF8
    Write-Host "  Migration: $mig" -ForegroundColor Green
} else {
    Write-Host "  Migration zaten var" -ForegroundColor DarkGray
}

# ============ 2) Windows Service paketi ============
Write-Host "Windows Service paketi ekleniyor..." -ForegroundColor Cyan
$csproj = "src\Yapair.Api\Yapair.Api.csproj"
$c = Get-Content $csproj -Raw
if ($c -notmatch 'Microsoft.Extensions.Hosting.WindowsServices') {
    dotnet add $csproj package Microsoft.Extensions.Hosting.WindowsServices 2>&1 | Out-Null
    Write-Host "  Paket eklendi" -ForegroundColor Green
} else {
    Write-Host "  Paket zaten var" -ForegroundColor DarkGray
}

# ============ 3) Program.cs = UseWindowsService ============
Write-Host "Program.cs guncelleniyor..." -ForegroundColor Cyan
$prog = "src\Yapair.Api\Program.cs"
Copy-Item $prog "$prog.bak-$ts" -Force
$pc = Get-Content $prog -Raw
if ($pc -notmatch 'UseWindowsService') {
    $pc = $pc -replace 'using Microsoft.AspNetCore.Http.Json;',
        "using Microsoft.AspNetCore.Http.Json;`r`nusing Microsoft.Extensions.Hosting.WindowsServices;"
    $pc = $pc -replace 'var builder = WebApplication\.CreateBuilder\(args\);',
        "var builder = WebApplication.CreateBuilder(args);`r`n`r`nif (!Environment.UserInteractive)`r`n{`r`n    builder.Host.UseWindowsService();`r`n}"
    Set-Content $prog -Value $pc -Encoding UTF8 -NoNewline
    Write-Host "  UseWindowsService eklendi" -ForegroundColor Green
} else {
    Write-Host "  Zaten var" -ForegroundColor DarkGray
}

# ============ 4) SupabaseClient: help_requests metotlari ============
Write-Host "SupabaseClient.cs guncelleniyor..." -ForegroundColor Cyan
$sbPath = "src\Yapair.Api\SupabaseClient.cs"
Copy-Item $sbPath "$sbPath.bak-$ts" -Force
$sb = Get-Content $sbPath -Raw

if ($sb -notmatch 'InsertHelpRequestAsync') {
    $methods = @'

    public async Task<long> InsertHelpRequestAsync(string? machineKey, string? machineLabel, string problemText, string? caseId, CancellationToken ct = default)
    {
        var row = new { machine_key = machineKey, machine_label = machineLabel, problem_text = problemText, case_id = caseId };
        using var req = new HttpRequestMessage(HttpMethod.Post, "rest/v1/help_requests")
        { Content = JsonContent.Create(row, options: Json) };
        req.Headers.Add("Prefer", "return=representation");
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        await EnsureSuccess(resp, "help_request insert", ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            return doc.RootElement[0].GetProperty("id").GetInt64();
        return 0;
    }

    public async Task<List<Dictionary<string, object?>>> GetHelpRequestsAsync(string? status = null, CancellationToken ct = default)
    {
        var url = "rest/v1/help_requests?select=*&order=created_at.desc&limit=100";
        if (!string.IsNullOrEmpty(status)) url += $"&status=eq.{Uri.EscapeDataString(status)}";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"help_requests query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        var list = new List<Dictionary<string, object?>>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var d = new Dictionary<string, object?>();
            foreach (var p in row.EnumerateObject()) d[p.Name] = ConvertElement(p.Value);
            list.Add(d);
        }
        return list;
    }

    public async Task UpdateHelpStatusAsync(long id, string status, string? adminNote, string? assignedTo, CancellationToken ct = default)
    {
        var url = $"rest/v1/help_requests?id=eq.{id}";
        var row = new { status, admin_note = adminNote, assigned_to = assignedTo, updated_at = DateTimeOffset.UtcNow };
        var body = JsonSerializer.Serialize(row, Json);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "help_request update", ct);
    }
'@
    $sb = $sb -replace '(\s+private static async Task EnsureSuccess)', ($methods + '$1')
    Set-Content $sbPath -Value $sb -Encoding UTF8 -NoNewline
    Write-Host "  + 3 metot eklendi" -ForegroundColor Green
} else {
    Write-Host "  Zaten var" -ForegroundColor DarkGray
}

# ============ 5) AdminEndpoints: help_requests route'lari ============
Write-Host "AdminEndpoints.cs guncelleniyor..." -ForegroundColor Cyan
$adminPath = "src\Yapair.Api\AdminEndpoints.cs"
Copy-Item $adminPath "$adminPath.bak-$ts" -Force
$admin = Get-Content $adminPath -Raw

if ($admin -notmatch 'admin/help-requests') {
    $routes = @'

        app.MapPost("/admin/help-requests", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            HelpRequest? body;
            try { body = await req.ReadFromJsonAsync<HelpRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.ProblemText))
                return Results.BadRequest(new { error = "empty_problem" });
            try
            {
                var id = await sb.InsertHelpRequestAsync(body.MachineKey, body.MachineLabel, body.ProblemText, body.CaseId, ct);
                return Results.Ok(new { status = "ok", id });
            }
            catch (Exception ex) { return Results.Json(new { error = "help_insert_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapGet("/admin/help-requests", async (HttpRequest req, SupabaseClient sb, string? status, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            try
            {
                var rows = await sb.GetHelpRequestsAsync(status, ct);
                return Results.Ok(new { total = rows.Count, items = rows });
            }
            catch (Exception ex) { return Results.Json(new { error = "help_list_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/help-requests/{id}/status", async (HttpRequest req, SupabaseClient sb, long id, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            HelpStatusUpdate? body;
            try { body = await req.ReadFromJsonAsync<HelpStatusUpdate>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrWhiteSpace(body.Status))
                return Results.BadRequest(new { error = "empty_status" });
            try
            {
                await sb.UpdateHelpStatusAsync(id, body.Status, body.AdminNote, body.AssignedTo, ct);
                return Results.Ok(new { status = "ok" });
            }
            catch (Exception ex) { return Results.Json(new { error = "help_update_failed", detail = ex.Message }, statusCode: 500); }
        });
'@
    $admin = $admin -replace '(\s+return app;)', ($routes + '$1')

    if ($admin -notmatch 'record HelpRequest') {
        $admin = $admin.TrimEnd() + "`r`npublic sealed record HelpRequest(string? MachineKey, string? MachineLabel, string? CaseId, string ProblemText);`r`npublic sealed record HelpStatusUpdate(string Status, string? AdminNote, string? AssignedTo);`r`n"
    }
    Set-Content $adminPath -Value $admin -Encoding UTF8 -NoNewline
    Write-Host "  + 3 route eklendi" -ForegroundColor Green
} else {
    Write-Host "  Zaten var" -ForegroundColor DarkGray
}

# ============ 6) WinUI: Yardim Iste butonu (XAML + CS) ============
Write-Host "WinUI: Yardim Iste butonu..." -ForegroundColor Cyan
$xamlPath = "src\Yapair.Desktop.WinUI\MainWindow.xaml"
Copy-Item $xamlPath "$xamlPath.bak-$ts" -Force
$xaml = Get-Content $xamlPath -Raw

if ($xaml -notmatch 'HelpBtn_Click') {
    $helpBtn = @'
<Button Click="HelpBtn_Click" Background="#da3633" Foreground="White" BorderThickness="0" Padding="10,5">
          <TextBlock Text="Yardim Iste" FontSize="11" FontWeight="SemiBold"/>
        </Button>
        <Button Click="ClearBtn_Click"
'@
    $xaml = $xaml.Replace('<Button Click="ClearBtn_Click"', $helpBtn)
    Set-Content $xamlPath -Value $xaml -Encoding UTF8 -NoNewline
    Write-Host "  XAML: Buton eklendi" -ForegroundColor Green
} else {
    Write-Host "  XAML: Zaten var" -ForegroundColor DarkGray
}

$csPath = "src\Yapair.Desktop.WinUI\MainWindow.xaml.cs"
Copy-Item $csPath "$csPath.bak-$ts" -Force
$cs = Get-Content $csPath -Raw

if ($cs -notmatch 'HelpBtn_Click') {
    $helpMethod = @'
private async void HelpBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = LoadEnv();
            if (!env.TryGetValue("YAPAIR_API_KEY", out var apiKey)) { AddBotMessage("YAPAIR_API_KEY yok"); return; }
            var baseUrl = env.TryGetValue("YAPAIR_API_BASE_URL", out var u) && !string.IsNullOrEmpty(u) ? u : "http://localhost:5080";

            var problemText = _lastFindings.Count > 0
                ? $"{_lastFindings.Count} bulgu cozulemedi. Ilk: {_lastFindings[0].Summary}"
                : "Kullanici yardim istedi";

            var payload = JsonSerializer.Serialize(new {
                machine_key = Environment.MachineName,
                machine_label = Environment.MachineName,
                problem_text = problemText,
                case_id = (string?)null
            });

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            content.Headers.Add("X-Yapair-Api-Key", apiKey);
            using var resp = await http.PostAsync(baseUrl.TrimEnd('/') + "/admin/help-requests", content);

            AddBotMessage(resp.IsSuccessStatusCode
                ? "Yardim talebiniz iletildi. Bilgi Islem ekibi en kisa surede donecek."
                : "Yardim talebi gonderilemedi: HTTP " + (int)resp.StatusCode);
        }
        catch (Exception ex) { AddBotMessage("Yardim talebi hatasi: " + ex.Message); }
    }

    private void ClearBtn_Click
'@
    $cs = $cs.Replace('private void ClearBtn_Click', $helpMethod)
    Set-Content $csPath -Value $cs -Encoding UTF8 -NoNewline
    Write-Host "  CS: Handler eklendi" -ForegroundColor Green
} else {
    Write-Host "  CS: Zaten var" -ForegroundColor DarkGray
}

# ============ 7) Admin panel: Yardim Talepleri sekmesi ============
Write-Host "Admin panel: Yardim Talepleri..." -ForegroundColor Cyan
$htmlPath = "src\Yapair.Api\wwwroot\admin\index.html"
Copy-Item $htmlPath "$htmlPath.bak-$ts" -Force
$html = Get-Content $htmlPath -Raw

if ($html -notmatch 'helpTable') {
    $helpHtmlBlock = @'
<h2>Yardim Talepleri <span class="hint" id="helpCount"></span></h2>
<div class="toolbar">
  <select id="helpStatusFilter" onchange="renderHelp()" style="background:#0d1117;color:#c9d1d9;border:1px solid #30363d;padding:4px 8px;border-radius:4px">
    <option value="">Tumu</option>
    <option value="open">Acik</option>
    <option value="in_progress">Islemde</option>
    <option value="resolved">Cozuldu</option>
    <option value="closed">Kapandi</option>
  </select>
  <button class="sec small" onclick="refreshHelp()">Yenile</button>
</div>
<table id="helpTable"><tbody><tr><td class="empty">Yukleniyor...</td></tr></tbody></table>

'@
    $html = $html.Replace('<h2>Vakalar</h2>', $helpHtmlBlock + '<h2>Vakalar</h2>')

    $helpJs = @'

async function refreshHelp(){
  try {
    const r = await fetch('/admin/help-requests').then(x => x.json());
    state.helpRows = r.items || [];
    renderHelp();
  } catch(e) { console.error('help fetch', e); }
}

function renderHelp(){
  const t = document.getElementById('helpTable');
  if (!t) return;
  const filter = document.getElementById('helpStatusFilter')?.value || '';
  const rows = (state.helpRows || []).filter(r => !filter || r.status === filter);
  const cnt = document.getElementById('helpCount');
  if (cnt) cnt.textContent = `(${rows.length})`;
  if (!rows.length){ t.innerHTML = '<tr><td class="empty">Yardim talebi yok</td></tr>'; return; }
  let html = '<thead><tr><th>ID</th><th>Makine</th><th>Sorun</th><th>Durum</th><th>Zaman</th><th>Not</th><th></th></tr></thead><tbody>';
  for (const r of rows){
    const st = r.status || 'open';
    const stCls = st === 'resolved' ? 'resolved' : st === 'closed' ? 'unknown' : st === 'in_progress' ? 'contradicted' : 'not_resolved';
    html += `<tr>
      <td><code>${esc(r.id)}</code></td>
      <td><code>${esc(r.machine_label || r.machine_key || '-')}</code></td>
      <td class="clip">${esc(r.problem_text || '')}</td>
      <td><span class="badge ${stCls}">${esc(st)}</span></td>
      <td>${esc(r.created_at || '')}</td>
      <td class="clip">${esc(r.admin_note || '-')}</td>
      <td><button class="sec small" onclick="updateHelpStatus(${r.id}, '${esc(st)}')">Durum</button></td>
    </tr>`;
  }
  t.innerHTML = html + '</tbody>';
}

async function updateHelpStatus(id, current){
  const status = prompt('Yeni durum (open, in_progress, resolved, closed):', current === 'open' ? 'in_progress' : current);
  if (!status) return;
  const note = prompt('Admin notu (opsiyonel):') || null;
  try {
    const r = await fetch('/admin/help-requests/' + id + '/status', {
      method: 'POST', headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ status, admin_note: note, assigned_to: null })
    });
    if (!r.ok) { alert('Hata: ' + await r.text()); return; }
    await refreshHelp();
  } catch(e) { alert('Hata: ' + e.message); }
}

setInterval(refreshHelp, 10000);

'@
    $html = $html.Replace('refresh();', 'refreshHelp();' + "`r`n" + $helpJs + 'refresh();')
    Set-Content $htmlPath -Value $html -Encoding UTF8 -NoNewline
    Write-Host "  index.html: Yardim Talepleri eklendi" -ForegroundColor Green
} else {
    Write-Host "  Zaten var" -ForegroundColor DarkGray
}

# ============ 8) Yardimci scriptler ============
Write-Host "Yardimci scriptler olusturuluyor..." -ForegroundColor Cyan

@'
$ErrorActionPreference = "Stop"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Bu script YONETICI olarak calistirilmalidir!" -ForegroundColor Red; exit 1 }

$repo = "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"
$dll = "$repo\src\Yapair.Api\bin\Debug\net10.0\Yapair.Api.dll"
$contentRoot = "$repo\src\Yapair.Api"
if (-not (Test-Path $dll)) { Write-Host "Once build: dotnet build src\Yapair.Api\Yapair.Api.csproj" -ForegroundColor Red; exit 1 }

$dotnetExe = (Get-Command dotnet).Source
$binPath = "`"$dotnetExe`" exec `"$dll`" --contentRoot `"$contentRoot`" --urls http://localhost:5080"

if (Get-Service -Name "YapairApi" -ErrorAction SilentlyContinue) {
    Write-Host "Mevcut servis kaldiriliyor..." -ForegroundColor Yellow
    Stop-Service -Name "YapairApi" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete YapairApi | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Servis olusturuluyor: YapairApi" -ForegroundColor Green
sc.exe create YapairApi binPath= $binPath start= auto DisplayName= "YAPAIR API" | Out-Null
sc.exe description YapairApi "YAPAIR Windows sorun giderici - yerel API + admin panel" | Out-Null
sc.exe failure YapairApi reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

Write-Host "Servis baslatiliyor..." -ForegroundColor Green
Start-Service -Name "YapairApi"
Start-Sleep -Seconds 3

$status = Get-Service -Name "YapairApi"
Write-Host "Servis durumu: $($status.Status)" -ForegroundColor Green
Write-Host ""
try {
    $r = Invoke-RestMethod "http://localhost:5080/health" -TimeoutSec 5
    Write-Host "Health: $($r.status) - $($r.service) v$($r.version)" -ForegroundColor Green
} catch {
    Write-Host "Health check basarisiz: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Event Log: Get-EventLog -LogName Application -Source YapairApi -Newest 5" -ForegroundColor Yellow
}
'@ | Set-Content "install-service.ps1" -Encoding UTF8
Write-Host "  install-service.ps1" -ForegroundColor Green

@'
$ErrorActionPreference = "Stop"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Yonetici gerekli" -ForegroundColor Red; exit 1 }

if (-not (Get-Service -Name "YapairApi" -ErrorAction SilentlyContinue)) {
    Write-Host "YapairApi servisi zaten yok" -ForegroundColor Yellow; exit 0
}

Stop-Service -Name "YapairApi" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
sc.exe delete YapairApi | Out-Null
Write-Host "YapairApi servisi kaldirildi" -ForegroundColor Green
'@ | Set-Content "uninstall-service.ps1" -Encoding UTF8
Write-Host "  uninstall-service.ps1" -ForegroundColor Green

@'
$ErrorActionPreference = "Stop"
$repo = "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$exe = $null
$candidates = @(
    "$repo\publish\desktop\Yapair.Desktop.WinUI.exe",
    "$repo\src\Yapair.Desktop.WinUI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Yapair.Desktop.WinUI.exe",
    "$repo\src\Yapair.Desktop.WinUI\bin\Release\net10.0-windows10.0.19041.0\win-x64\Yapair.Desktop.WinUI.exe"
)
foreach ($p in $candidates) { if (Test-Path $p) { $exe = $p; break } }

if (-not $exe) { Write-Host "WinUI exe bulunamadi. Once build/publish et." -ForegroundColor Red; exit 1 }

$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup "YAPAIR Sorun Giderici.lnk"

$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = Split-Path $exe
$sc.Description = "YAPAIR - Sorun Giderici"
$sc.Save()
Write-Host "Startup kisayolu: $lnk" -ForegroundColor Green
Write-Host "  Hedef: $exe" -ForegroundColor DarkGray
'@ | Set-Content "install-startup.ps1" -Encoding UTF8
Write-Host "  install-startup.ps1" -ForegroundColor Green

@'
$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup "YAPAIR Sorun Giderici.lnk"
if (Test-Path $lnk) { Remove-Item $lnk -Force; Write-Host "Kisayol kaldirildi" -ForegroundColor Green }
else { Write-Host "Kisayol yok" -ForegroundColor Yellow }
'@ | Set-Content "uninstall-startup.ps1" -Encoding UTF8
Write-Host "  uninstall-startup.ps1" -ForegroundColor Green

# ============ 9) Build ============
Write-Host ""
Write-Host "Build ediliyor..." -ForegroundColor Yellow

dotnet build "src\Yapair.Api\Yapair.Api.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "API BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "  API OK" -ForegroundColor Green

Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "WinUI BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "  WinUI OK" -ForegroundColor Green

# ============ 10) Dogrulama ============
Write-Host ""
Write-Host "Dogrulama:" -ForegroundColor Cyan

$checks = @{
    "Migration help_requests" = (Test-Path $mig)
    "UseWindowsService"       = ((Get-Content $prog -Raw) -match 'UseWindowsService')
    "SupabaseClient help"     = ((Get-Content $sbPath -Raw) -match 'InsertHelpRequestAsync')
    "AdminEndpoints help"     = ((Get-Content $adminPath -Raw) -match 'admin/help-requests')
    "WinUI HelpBtn XAML"      = ((Get-Content $xamlPath -Raw) -match 'HelpBtn_Click')
    "WinUI HelpBtn CS"        = ((Get-Content $csPath -Raw) -match 'HelpBtn_Click')
    "Panel helpTable"         = ((Get-Content $htmlPath -Raw) -match 'helpTable')
    "install-service.ps1"     = (Test-Path "install-service.ps1")
    "install-startup.ps1"     = (Test-Path "install-startup.ps1")
}
$allOk = $true
foreach ($k in $checks.Keys) {
    $ok = $checks[$k]
    if (-not $ok) { $allOk = $false }
    Write-Host ("  {0,-28} {1}" -f $k, $(if ($ok) {"OK"} else {"EKSIK"})) -ForegroundColor $(if ($ok) {"Green"} else {"Red"})
}

Write-Host ""
if (-not $allOk) { Write-Host "Eksikler var!" -ForegroundColor Red; exit 1 }

Write-Host "=== FAZ 1 TAMAM ===" -ForegroundColor Green
Write-Host ""
Write-Host "Sirada yapilacaklar:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1) Migration uygula:" -ForegroundColor Cyan
Write-Host "   powershell -ExecutionPolicy Bypass -File scripts\db-setup.ps1 -EnvFile supabase\.env.local" -ForegroundColor White
Write-Host ""
Write-Host "2) Servisi kur (YONETICI PowerShell):" -ForegroundColor Cyan
Write-Host "   powershell -ExecutionPolicy Bypass -File install-service.ps1" -ForegroundColor White
Write-Host ""
Write-Host "3) WinUI'yi startup'a ekle:" -ForegroundColor Cyan
Write-Host "   powershell -ExecutionPolicy Bypass -File install-startup.ps1" -ForegroundColor White
Write-Host ""
Write-Host "4) Test:" -ForegroundColor Cyan
Write-Host "   WinUI'de 'Yardim Iste' butonu -> panele dusmeli" -ForegroundColor White
Write-Host "   Admin panelde 'Yardim Talepleri' sekmesi -> talep gorunmeli" -ForegroundColor White