# install-admin-shortcut.ps1
# YAPAIR Admin Konsolu Masaustu Kisayolu Olusturucu

$ErrorActionPreference = "Stop"

$repo = $PSScriptRoot
$adminExe = Join-Path $repo "src\Yapair.Admin\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Yapair.Admin.exe"
$publishExe = Join-Path $repo "publish\admin\Yapair.Admin.exe"

$targetExe = if (Test-Path $publishExe) { $publishExe } elseif (Test-Path $adminExe) { $adminExe } else { $adminExe }

$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "YAPAIR Admin Konsolu.lnk"

$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = Split-Path $targetExe
$shortcut.Description = "YAPAIR IT Yonetici Konsolu"
$shortcut.Save()

Write-Host "Masaustu kisayolu olusturuldu: $shortcutPath" -ForegroundColor Green
Write-Host "Hedef: $targetExe" -ForegroundColor DarkGray
