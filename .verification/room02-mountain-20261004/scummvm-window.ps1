# ScummVM window helper: sv.ps1 shot <out.png> | click <gameX> <gameY> | key <sendkeys text>
param([string]$Cmd, [string]$A, [string]$B)
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class SV {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[SV]::SetProcessDPIAware() | Out-Null
$p = Get-Process scummvm | Select-Object -First 1
$h = $p.MainWindowHandle
[SV]::ShowWindow($h, 9) | Out-Null
[SV]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 300
$r = New-Object SV+RECT; [SV]::GetClientRect($h, [ref]$r) | Out-Null
$o = New-Object SV+POINT; [SV]::ClientToScreen($h, [ref]$o) | Out-Null
$w = $r.R - $r.L; $ht = $r.B - $r.T
if ($Cmd -eq 'shot') {
  $bmp = New-Object System.Drawing.Bitmap $w, $ht
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($o.X, $o.Y, 0, 0, (New-Object System.Drawing.Size $w, $ht))
  $bmp.Save($A, [System.Drawing.Imaging.ImageFormat]::Png)
  "client ${w}x${ht} at ($($o.X),$($o.Y))"
}
elseif ($Cmd -eq 'click') {
  # game coordinates 320x200 scaled to the client area
  $sx = $o.X + [int]([double]$A * $w / 320.0 + 0.5 * $w / 320.0)
  $sy = $o.Y + [int]([double]$B * $ht / 200.0 + 0.5 * $ht / 200.0)
  [SV]::SetCursorPos($sx, $sy) | Out-Null; Start-Sleep -Milliseconds 120
  [SV]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60; [SV]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
  "clicked game ($A,$B) at screen ($sx,$sy)"
}
elseif ($Cmd -eq 'burst') {
  # burst <gameX,gameY> <count>: click, then capture <count> frames as fast as possible (prefix in env SVPREFIX)
  $xy = $A.Split(','); $n = [int]$B
  $sx = $o.X + [int]([double]$xy[0] * $w / 320.0 + 0.5 * $w / 320.0)
  $sy = $o.Y + [int]([double]$xy[1] * $ht / 200.0 + 0.5 * $ht / 200.0)
  [SV]::SetCursorPos($sx, $sy) | Out-Null; Start-Sleep -Milliseconds 120
  $sw = [Diagnostics.Stopwatch]::StartNew()
  [SV]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 50; [SV]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
  $frames = @()
  for ($i = 0; $i -lt $n; $i++) {
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($o.X, $o.Y, 0, 0, (New-Object System.Drawing.Size $w, $ht))
    $g.Dispose()
    $frames += ,@($bmp, $sw.ElapsedMilliseconds)
  }
  $i = 0
  foreach ($f in $frames) { $f[0].Save("$($env:SVPREFIX)-$('{0:00}' -f $i)-$($f[1])ms.png", [System.Drawing.Imaging.ImageFormat]::Png); $i++ }
  "captured $n frames after click at game ($($xy[0]),$($xy[1]))"
}
elseif ($Cmd -eq 'key') { [System.Windows.Forms.SendKeys]::SendWait($A); "sent keys" }
