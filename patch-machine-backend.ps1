$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$ts = Get-Date -Format "yyyyMMdd-HHmmss"

# ============ 1) SupabaseClient.cs ============
Write-Host "SupabaseClient.cs guncelleniyor..." -ForegroundColor Cyan
$sbPath = "src\Yapair.Api\SupabaseClient.cs"
Copy-Item $sbPath "$sbPath.bak-$ts" -Force
$sb = Get-Content $sbPath -Raw

# machines select'ine notes,owner ekle
$sb = $sb -replace '"machine_key,machine_label,first_seen,last_seen"',
    '"machine_key,machine_label,first_seen,last_seen,notes,owner"'

# Yeni metotlar (EnsureSuccess oncesine)
$newMethods = @'

    public async Task<Dictionary<string, object?>?> GetMachineAsync(string machineKey, CancellationToken ct = default)
    {
        const string select = "machine_key,machine_label,first_seen,last_seen,notes,owner";
        var url = $"rest/v1/machines?machine_key=eq.{Uri.EscapeDataString(machineKey)}&select={select}&limit=1";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"machine query failed: HTTP {(int)resp.StatusCode} {body}");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;
        var row = doc.RootElement[0];
        var dict = new Dictionary<string, object?>();
        foreach (var p in row.EnumerateObject()) dict[p.Name] = ConvertElement(p.Value);
        return dict;
    }

    public async Task UpdateMachineAsync(string machineKey, string? machineLabel, string? notes, string? owner, CancellationToken ct = default)
    {
        var url = $"rest/v1/machines?machine_key=eq.{Uri.EscapeDataString(machineKey)}";
        var body = JsonSerializer.Serialize(new { machine_label = machineLabel, notes, owner }, Json);
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machine update", ct);
    }

    public async Task<int> DeleteMachinesAsync(IReadOnlyList<string> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0) return 0;
        var url = "rest/v1/machines?machine_key=in.(" + BuildIdList(keys) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "machines delete", ct);
        return keys.Count;
    }

    public async Task<int> DeleteBatchesAsync(IReadOnlyList<string> batchIds, CancellationToken ct = default)
    {
        if (batchIds.Count == 0) return 0;
        var url = "rest/v1/sync_receipts?batch_id=in.(" + BuildIdList(batchIds) + ")";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("Prefer", "return=minimal");
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccess(resp, "batches delete", ct);
        return batchIds.Count;
    }
'@

if ($sb -notmatch 'GetMachineAsync') {
    $sb = $sb -replace '(\s+private static async Task EnsureSuccess)', ($newMethods + '$1')
    Write-Host "  + 4 metot eklendi" -ForegroundColor Green
} else {
    Write-Host "  Metotlar zaten var" -ForegroundColor DarkGray
}

Set-Content $sbPath -Value $sb -Encoding UTF8 -NoNewline

# ============ 2) AdminEndpoints.cs ============
Write-Host "AdminEndpoints.cs guncelleniyor..." -ForegroundColor Cyan
$adminPath = "src\Yapair.Api\AdminEndpoints.cs"
Copy-Item $adminPath "$adminPath.bak-$ts" -Force
$admin = Get-Content $adminPath -Raw

# Yeni route'lar (return app; oncesine)
$newRoutes = @'

        app.MapGet("/admin/machine/{key}", async (HttpRequest req, SupabaseClient sb, string key, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            try
            {
                var row = await sb.GetMachineAsync(key, ct);
                return row is null ? Results.NotFound(new { error = "not_found" }) : Results.Ok(row);
            }
            catch (Exception ex) { return Results.Json(new { error = "machine_detail_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/machines/update", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            MachineUpdateRequest? body;
            try { body = await req.ReadFromJsonAsync<MachineUpdateRequest>(ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json", detail = ex.Message }); }
            if (body is null || string.IsNullOrEmpty(body.MachineKey))
                return Results.BadRequest(new { error = "missing_machine_key" });
            try
            {
                await sb.UpdateMachineAsync(body.MachineKey, body.MachineLabel, body.Notes, body.Owner, ct);
                return Results.Ok(new { status = "ok" });
            }
            catch (Exception ex) { return Results.Json(new { error = "update_failed", detail = ex.Message }, statusCode: 500); }
        });

        app.MapPost("/admin/machines/delete", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteMachinesAsync(ids, ct), "deleted");
        });

        app.MapPost("/admin/batches/delete", async (HttpRequest req, SupabaseClient sb, CancellationToken ct) =>
        {
            if (!IsLocalhost(req)) return Results.Unauthorized();
            return await HandleIdsAction(req, ids => sb.DeleteBatchesAsync(ids, ct), "deleted");
        });
'@

if ($admin -notmatch 'admin/machine/\{key\}') {
    $admin = $admin -replace '(\s+return app;)', ($newRoutes + '$1')
    Write-Host "  + 4 route eklendi" -ForegroundColor Green
} else {
    Write-Host "  Route'lar zaten var" -ForegroundColor DarkGray
}

# MachineUpdateRequest record (yoksa)
if ($admin -notmatch 'record MachineUpdateRequest') {
    $admin = $admin.TrimEnd() + "`r`npublic sealed record MachineUpdateRequest(string MachineKey, string? MachineLabel, string? Notes, string? Owner);`r`n"
    Write-Host "  + MachineUpdateRequest record eklendi" -ForegroundColor Green
}

Set-Content $adminPath -Value $admin -Encoding UTF8 -NoNewline

# ============ 3) Dogrulama ============
Write-Host ""
Write-Host "Dogrulama:" -ForegroundColor Cyan
$sb2 = Get-Content $sbPath -Raw
$admin2 = Get-Content $adminPath -Raw

$checks = @{
    "GetMachineAsync"       = $sb2 -match 'GetMachineAsync'
    "UpdateMachineAsync"    = $sb2 -match 'UpdateMachineAsync'
    "DeleteMachinesAsync"   = $sb2 -match 'DeleteMachinesAsync'
    "DeleteBatchesAsync"    = $sb2 -match 'DeleteBatchesAsync'
    "admin/machine/{key}"   = $admin2 -match 'admin/machine/\{key\}'
    "admin/machines/update" = $admin2 -match 'admin/machines/update'
    "admin/machines/delete" = $admin2 -match 'admin/machines/delete'
    "admin/batches/delete"  = $admin2 -match 'admin/batches/delete'
    "MachineUpdateRequest"  = $admin2 -match 'record MachineUpdateRequest'
}
$allOk = $true
foreach ($k in $checks.Keys) {
    $ok = $checks[$k]
    if (-not $ok) { $allOk = $false }
    Write-Host ("  {0,-24} {1}" -f $k, $(if ($ok) {"OK"} else {"EKSIK"})) -ForegroundColor $(if ($ok) {"Green"} else {"Red"})
}

if (-not $allOk) { Write-Host "Eksik var!" -ForegroundColor Red; exit 1 }

# ============ 4) Build ============
Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
dotnet build "src\Yapair.Api\Yapair.Api.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "BUILD BASARILI" -ForegroundColor Green
Write-Host ""
Write-Host "Sirada: API'yi yeniden baslat, panelde Ctrl+F5" -ForegroundColor Cyan