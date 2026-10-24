$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$p = "src\Yapair.Api\AdminEndpoints.cs"
$c = Get-Content $p -Raw
$need = @()
if ($c -notmatch 'record ResearchRequest')       { $need += 'public sealed record ResearchRequest(string Query);' }
if ($c -notmatch 'record MachineUpdateRequest')  { $need += 'public sealed record MachineUpdateRequest(string MachineKey, string? MachineLabel, string? Notes, string? Owner);' }
if ($c -notmatch 'record AuthAuditRequest')      { $need += 'public sealed record AuthAuditRequest(string? MachineKey, string? ModeBefore, string? ModeAfter, string? Note);' }
if ($c -notmatch 'record AdminTestRequest')      { $need += 'public sealed record AdminTestRequest(string? SymptomCode, string? MachineLabel);' }
if ($c -notmatch 'record IdsRequest')            { $need += 'public sealed record IdsRequest(List<string>? Ids);' }

if ($need.Count -gt 0) {
    $c = $c.TrimEnd() + "`r`n" + ($need -join "`r`n") + "`r`n"
    Set-Content $p -Value $c -Encoding UTF8 -NoNewline
    Write-Host "Eklenen record sayisi: $($need.Count)" -ForegroundColor Green
    $need | ForEach-Object { Write-Host "  + $_" -ForegroundColor DarkGray }
} else {
    Write-Host "Tum record'lar zaten mevcut" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Son 8 satir:" -ForegroundColor Cyan
Get-Content $p -Tail 8

Write-Host ""
Write-Host "Build..." -ForegroundColor Yellow
dotnet build "src\Yapair.Api\Yapair.Api.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD BASARISIZ (exit $LASTEXITCODE)" -ForegroundColor Red
    exit 1
}
Write-Host "BUILD BASARILI" -ForegroundColor Green