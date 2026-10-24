$ErrorActionPreference = "Stop"
Set-Location "C:\Users\YILMAZ\Documents\deepseek-harness\default-workspace\yapair"

$p = "src\Yapair.Core\Playbooks\SpoolerRestartExecutor.cs"
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
if (Test-Path $p) { Copy-Item $p "$p.bak-$ts" -Force }

@'
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Yapair.Core.Playbooks;

[SupportedOSPlatform("windows")]
public sealed class SpoolerRestartExecutor : IRepairExecutor
{
    public string PlaybookId => "printers.spooler.restart.v1";
    public RiskLevel Risk => RiskLevel.R1;

    public async Task<RepairResult> ExecuteAsync(CancellationToken ct = default)
    {
        try
        {
            using var sc = new ServiceController("Spooler");
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                if (!sc.CanStop) return new RepairResult(false, "Spooler durdurulamaz");
                sc.Stop();
                await WaitAsync(sc, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30), ct);
            }
            sc.Start();
            await WaitAsync(sc, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30), ct);
            return new RepairResult(true, "Spooler yeniden baslatildi");
        }
        catch (System.TimeoutException tex)
        {
            return new RepairResult(false, "Zaman asimi: " + tex.Message);
        }
        catch (InvalidOperationException iex)
        {
            return new RepairResult(false, "Spooler servisi bulunamadi: " + iex.Message);
        }
        catch (Exception ex)
        {
            return new RepairResult(false, ex.Message);
        }
    }

    private static async Task WaitAsync(ServiceController sc, ServiceControllerStatus want, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            sc.Refresh();
            if (sc.Status == want) return;
            await Task.Delay(500, ct);
        }
        throw new System.TimeoutException($"Servis {want} durumuna ulasmadi");
    }
}
'@ | Set-Content $p -Encoding UTF8
Write-Host "SpoolerRestartExecutor.cs duzeltildi" -ForegroundColor Green

# Yapair.Core.csproj'a NoWarn CA1416 ekle (Windows-only API uyarilari)
$csproj = "src\Yapair.Core\Yapair.Core.csproj"
$c = Get-Content $csproj -Raw
if ($c -notmatch 'CA1416') {
    if ($c -match '<NoWarn>') {
        $c = $c -replace '<NoWarn>', '<NoWarn>CA1416;'
    } else {
        $c = $c -replace '</PropertyGroup>', '  <NoWarn>$(NoWarn);CA1416</NoWarn>' + "`r`n  </PropertyGroup>"
    }
    Set-Content $csproj -Value $c -Encoding UTF8 -NoNewline
    Write-Host "CA1416 uyarisi bastirildi (Yapair.Core.csproj)" -ForegroundColor Green
} else {
    Write-Host "CA1416 zaten bastirilmis" -ForegroundColor DarkGray
}

# Build
Write-Host ""
Write-Host "Build ediliyor..." -ForegroundColor Yellow
dotnet build "src\Yapair.Core\Yapair.Core.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "Core BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "  Core OK" -ForegroundColor Green

dotnet build "src\Yapair.Api\Yapair.Api.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "API BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "  API OK" -ForegroundColor Green

Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\Yapair.Desktop.WinUI\bin -ErrorAction SilentlyContinue
dotnet build "src\Yapair.Desktop.WinUI\Yapair.Desktop.WinUI.csproj" -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "WinUI BUILD BASARISIZ" -ForegroundColor Red; exit 1 }
Write-Host "  WinUI OK" -ForegroundColor Green

Write-Host ""
Write-Host "BUILD TAMAM" -ForegroundColor Green
Write-Host ""
Write-Host "Calistir: dotnet run --project src\Yapair.Desktop.WinUI -r win-x64" -ForegroundColor Cyan