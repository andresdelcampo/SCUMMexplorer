# Contact sheet: one row per scene folder (its pictures in name order, at most 5), each picture cropped to the window's centre column and scaled.
# usage: powershell -File sheet.ps1 <root> <out.png> <folder>...
param([string]$Root, [string]$Out, [Parameter(ValueFromRemainingArguments = $true)][string[]]$Folders)
Add-Type -AssemblyName System.Drawing
$scale = 0.42
$cropX = 250; $cropY = 60; $cropW = 900; $cropH = 640
$tileW = [int]($cropW * $scale); $tileH = [int]($cropH * $scale); $label = 18
$rows = @()
foreach ($f in $Folders) {
    $dir = Join-Path $Root $f
    $pics = @(Get-ChildItem -LiteralPath $dir -Filter *.png -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike 'status-*' } | Sort-Object Name | Select-Object -First 5)
    $rows += ,@($f, $pics)
}
$cols = 5
$bmp = New-Object System.Drawing.Bitmap ($cols * $tileW), ($rows.Count * ($tileH + $label))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Black)
$font = New-Object System.Drawing.Font 'Arial', 10
$y = 0
foreach ($row in $rows) {
    $g.DrawString(($row[0] + '  (' + (($row[1] | ForEach-Object { $_.BaseName -replace '^.*-(run\d+-)?', '' }) -join ' ') + ')'), $font, [System.Drawing.Brushes]::Yellow, 2, $y + 1)
    $x = 0
    foreach ($p in $row[1]) {
        $img = [System.Drawing.Image]::FromFile($p.FullName)
        $src = New-Object System.Drawing.Rectangle $cropX, $cropY, ([Math]::Min($cropW, $img.Width - $cropX)), ([Math]::Min($cropH, $img.Height - $cropY))
        $dst = New-Object System.Drawing.Rectangle $x, ($y + $label), $tileW, $tileH
        $g.DrawImage($img, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
        $img.Dispose()
        $x += $tileW
    }
    $y += $tileH + $label
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
