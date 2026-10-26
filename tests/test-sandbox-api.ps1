# Live Sandbox Integration Test for Yapair API & Remote Repair
$ErrorActionPreference = "Stop"

Write-Host "=== 1. Starting Yapair API in background ===" -ForegroundColor Cyan
$apiProc = Start-Process -FilePath "dotnet" -ArgumentList "run --project src/Yapair.Api/Yapair.Api.csproj --urls http://localhost:5080" -PassThru -WindowStyle Hidden

try {
    Start-Sleep -Seconds 4
    
    Write-Host "=== 2. Testing /health ===" -ForegroundColor Cyan
    $health = Invoke-RestMethod -Uri "http://localhost:5080/health" -Method Get
    Write-Host "Health: $($health.status), Mode: $($health.mode)" -ForegroundColor Green

    Write-Host "=== 3. Testing /admin/stats (Veri Gelme Kontrolu) ===" -ForegroundColor Cyan
    $stats = Invoke-RestMethod -Uri "http://localhost:5080/admin/stats" -Method Get
    Write-Host "Cases Total: $($stats.cases.total), Machines: $($stats.machines.total)" -ForegroundColor Green
    if ($stats.cases.total -lt 5) { throw "Seed cases not loaded!" }

    Write-Host "=== 4. Testing Case Detail /admin/case/CASE-2026-001 ===" -ForegroundColor Cyan
    $detail = Invoke-RestMethod -Uri "http://localhost:5080/admin/case/CASE-2026-001" -Method Get
    Write-Host "Detail: $($detail.case_id) - $($detail.symptom_label) - $($detail.playbook_id)" -ForegroundColor Green

    Write-Host "=== 5. Testing Remote Repair Dispatch ===" -ForegroundColor Cyan
    $dispatchBody = @{
        machine_key = "MK-IST-DESK-01"
        playbook_id = "printers.spooler.restart.v1"
        initiated_by = "AdminSandbox"
        parameters = ""
    } | ConvertTo-Json
    $dispatch = Invoke-RestMethod -Uri "http://localhost:5080/admin/remote-repair/dispatch" -Method Post -Body $dispatchBody -ContentType "application/json"
    $repairId = $dispatch.repair_id
    Write-Host "Dispatched Repair ID: $repairId" -ForegroundColor Green

    Write-Host "=== 6. Testing Remote Repair Pending Polling ===" -ForegroundColor Cyan
    $pending = Invoke-RestMethod -Uri "http://localhost:5080/api/v1/remote-repair/pending?machine_key=MK-IST-DESK-01" -Method Get
    Write-Host "Pending Repairs: $($pending.count)" -ForegroundColor Green
    if ($pending.count -lt 1) { throw "Pending repair not found!" }

    Write-Host "=== 7. Testing Remote Repair Result Submission ===" -ForegroundColor Cyan
    $resultBody = @{
        repair_id = $repairId
        status = "completed"
        output = "Yazici biriktiricisi servisi sandboxta basariyla yeniden baslatildi."
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "http://localhost:5080/api/v1/remote-repair/result" -Method Post -Body $resultBody -ContentType "application/json"
    Write-Host "Result Status: $($res.status)" -ForegroundColor Green

    Write-Host "=== 8. Testing Remote Repairs History List ===" -ForegroundColor Cyan
    $history = Invoke-RestMethod -Uri "http://localhost:5080/admin/remote-repairs?machine_key=MK-IST-DESK-01" -Method Get
    Write-Host "Total Remote Repairs in History: $($history.count)" -ForegroundColor Green

    Write-Host "=== 9. Testing Web UI /admin/index.html (404 Fallback Testi) ===" -ForegroundColor Cyan
    $wc = New-Object System.Net.WebClient
    $htmlContent = $wc.DownloadString("http://localhost:5080/admin/index.html")
    Write-Host "Web UI Content-Length: $($htmlContent.Length)" -ForegroundColor Green
    if (-not ($htmlContent -match "YAPAIR Admin")) { throw "Web UI index.html failed!" }

    Write-Host "=== 10. Testing API Key Creation & Deletion ===" -ForegroundColor Cyan
    $createKeyBody = @{
        name = "Gecici Test Teknisyeni"
        role = "technician"
        expires_days = 30
    } | ConvertTo-Json
    $createdKey = Invoke-RestMethod -Uri "http://localhost:5080/admin/auth/keys/create" -Method Post -Body $createKeyBody -ContentType "application/json"
    $keyId = $createdKey.key.key_id
    Write-Host "Created Key ID: $keyId, Raw Key: $($createdKey.raw_key)" -ForegroundColor Green

    $delBody = @{ key_id = $keyId } | ConvertTo-Json
    $delResp = Invoke-RestMethod -Uri "http://localhost:5080/admin/auth/keys/delete" -Method Post -Body $delBody -ContentType "application/json"
    Write-Host "Delete Key Status: $($delResp.status)" -ForegroundColor Green
    if ($delResp.status -ne "ok") { throw "Delete key failed!" }

    Write-Host "=== 11. Testing AI Configuration & Masking ===" -ForegroundColor Cyan
    $aiCfgBody = @{
        gemini_api_key = "AIzaSyTestApiKeyForYapairGeminiLiveIntegration12345"
        model = "gemini-1.5-flash"
        enable_duckduckgo = $true
        enable_web_research = $true
    } | ConvertTo-Json
    $aiSaved = Invoke-RestMethod -Uri "http://localhost:5080/admin/ai-config" -Method Post -Body $aiCfgBody -ContentType "application/json"
    Write-Host "AI Config Saved. Masked Key: $($aiSaved.config.masked_key), HasKey: $($aiSaved.config.has_key)" -ForegroundColor Green
    if ($aiSaved.config.masked_key -eq "AIzaSyTestApiKeyForYapairGeminiLiveIntegration12345" -or -not ($aiSaved.config.masked_key -match "\.\.\.")) { throw "API key was not masked!" }

    Write-Host "=== 12. Testing Desktop Client AI Status Discovery ===" -ForegroundColor Cyan
    $clientStatus = Invoke-RestMethod -Uri "http://localhost:5080/api/v1/ai-config/status" -Method Get
    Write-Host "Client AI Discovery: HasKey=$($clientStatus.has_key), Model=$($clientStatus.model), WebResearch=$($clientStatus.enable_web_research)" -ForegroundColor Green
    if (-not $clientStatus.has_key) { throw "Desktop client could not discover AI status!" }

    Write-Host "`n>>> ALL LIVE SANDBOX TESTS (INCLUDING WEB UI, API KEY DELETION & AI MASKING) PASSED SUCCESSFULLY! <<<" -ForegroundColor Green
}
finally {
    Write-Host "Stopping API process..." -ForegroundColor Yellow
    Stop-Process -Id $apiProc.Id -Force -ErrorAction SilentlyContinue
}
