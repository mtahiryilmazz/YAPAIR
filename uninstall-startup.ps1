$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup "YAPAIR Sorun Giderici.lnk"
if (Test-Path $lnk) { Remove-Item $lnk -Force; Write-Host "Kisayol kaldirildi" -ForegroundColor Green }
else { Write-Host "Kisayol yok" -ForegroundColor Yellow }
