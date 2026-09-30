Add-Type -AssemblyName System.Drawing
function HashPixels([string]$path) {
    $bitmap = [System.Drawing.Bitmap]::new($path)
    try {
        $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $bytes = [byte[]]::new($data.Stride * $bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$bytes,0,$bytes.Length)
        $bitmap.UnlockBits($data)
        [Convert]::ToBase64String([Security.Cryptography.SHA256]::Create().ComputeHash($bytes))
    } finally { $bitmap.Dispose() }
}
$root = 'D:\Documents\Personal\Code Projects\SCUMMexplorer'
$changed = @(); $checked = 0
foreach ($bmp in Get-ChildItem "$root/.verification/score-preservation-20260930/room-audit" -Filter 'room-*.bmp') {
    $png = Join-Path "$root/.verification/room-audit" ($bmp.BaseName + '.png')
    if (!(Test-Path -LiteralPath $png) -or (HashPixels $bmp.FullName) -ne (HashPixels $png)) { $changed += $bmp.Name }
    $checked++
}
"Compared $checked audit renders; changed: $($changed.Count) $($changed -join ', ')" | Tee-Object -FilePath "$root/.verification/score-preservation-20260930/png-comparison.txt"
