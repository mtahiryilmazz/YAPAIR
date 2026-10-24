# Panodaki metni tekrar yaz (eksiksiz kopyaladigindan emin ol)
$text = Get-Clipboard -Raw
[IO.File]::WriteAllText("$PWD\scripts\generate-sync.ps1", $text, [Text.Encoding]::UTF8)