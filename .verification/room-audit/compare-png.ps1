# Compares each audit BMP with the committed PNG of the same room, pixel by
# pixel. With -Refresh, rewrites the PNG of every room whose render changed.
param([switch]$Refresh)
Add-Type -AssemblyName System.Drawing
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
# Load from memory so GDI+ keeps no lock on the file.
function Load([string]$path) { [System.Drawing.Bitmap]::new([System.IO.MemoryStream]::new([System.IO.File]::ReadAllBytes($path))) }
function Pixels([string]$path) {
    $image = Load $path
    try {
        $rect = [System.Drawing.Rectangle]::new(0, 0, $image.Width, $image.Height)
        $data = $image.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $bytes = [byte[]]::new($data.Stride * $image.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        $image.UnlockBits($data)
        "$($image.Width)x$($image.Height):" + [Convert]::ToBase64String([System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes))
    } finally { $image.Dispose() }
}
$changed = @()
foreach ($bmp in Get-ChildItem -Path $dir -Filter 'room-*.bmp' | Sort-Object Name) {
    $png = [System.IO.Path]::ChangeExtension($bmp.FullName, '.png')
    if ((Test-Path $png) -and ((Pixels $bmp.FullName) -eq (Pixels $png))) { continue }
    $changed += $bmp.BaseName
    if ($Refresh) {
        $image = Load $bmp.FullName
        try { $image.Save($png, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $image.Dispose() }
    }
}
Remove-Item -Path (Join-Path $dir '*.png.new') -ErrorAction SilentlyContinue
"Changed renders: $($changed.Count) $($changed -join ', ')"
