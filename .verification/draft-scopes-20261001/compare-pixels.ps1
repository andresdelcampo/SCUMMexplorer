param([string]$Audit='Exe/DraftScopeSession-20261001/final-audits-1/room-audit',[string]$Output='Exe/DraftScopeSession-20261001/final-audits-1/png-comparison.txt')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
function HashPixels([string]$path) {
    $bitmap=[System.Drawing.Bitmap]::new($path)
    try {
        $data=$bitmap.LockBits([System.Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $bytes=[byte[]]::new($data.Stride*$bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$bytes,0,$bytes.Length)
        $bitmap.UnlockBits($data)
        [Convert]::ToBase64String([Security.Cryptography.SHA256]::Create().ComputeHash($bytes))
    } finally {$bitmap.Dispose()}
}
$changed=@();$checked=0
foreach($bmp in Get-ChildItem $Audit -Filter 'room-*.bmp') {
    $png=Join-Path '.verification/room-audit' ($bmp.BaseName+'.png')
    if(!(Test-Path -LiteralPath $png) -or (HashPixels $bmp.FullName) -ne (HashPixels ((Resolve-Path $png).Path))){$changed+=$bmp.Name}
    $checked++
}
"Compared $checked room renders; changed: $($changed.Count) $($changed -join ', ')" | Tee-Object -FilePath $Output
if($checked -ne 76 -or $changed.Count){exit 1}
