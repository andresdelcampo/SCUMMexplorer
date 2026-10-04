param([string]$Filter = '*', [int]$TimeoutSec = 240, [string]$Tag = 'run1')
$ErrorActionPreference = 'Continue'
$root = 'D:\Documents\Personal\Code Projects\SCUMMexplorer'
$scratch = Join-Path $root '.verification\harness-review-20261004\out'   # results are written here (git-ignored)
New-Item -ItemType Directory -Force $scratch | Out-Null
Add-Type @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public class W2 {
 public delegate bool EnumProc(IntPtr h, IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
 [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 public static string Titles(uint pid){ var r=new List<string>(); EnumWindows((h,l)=>{ uint p; GetWindowThreadProcessId(h,out p); if(p==pid && IsWindowVisible(h)){ var sb=new StringBuilder(256); GetWindowText(h,sb,256); if(sb.Length>0) r.Add(sb.ToString()); } return true;},IntPtr.Zero); return string.Join("|", r); } }
"@
$outRoot = Join-Path $scratch "out-$Tag"
New-Item -ItemType Directory -Force $outRoot | Out-Null
$results = Join-Path $scratch "results-$Tag.tsv"
if (-not (Test-Path $results)) { "name`tbuild`texit`tseconds`tnote" | Out-File $results -Encoding utf8 }
$env:SCUMM_SOUND_TEST_ROOT = "$root\Games\loom ega en"
$env:SCUMM_INDY3_EGA_TEST_ROOT = "$root\Games\indy3 ega en"
$env:SCUMM_HARNESS_FAILURE_LOG = "$scratch\failures-$Tag.log"
$projects = Get-ChildItem "$root\.verification" -Recurse -Filter *.csproj | Where-Object { $_.FullName -notmatch '\\obj\\' } | Sort-Object FullName
foreach ($p in $projects) {
  $name = $p.BaseName
  $key = $p.Directory.FullName.Substring($root.Length + 14)
  if ($key -notlike $Filter) { continue }
  if ($key -like '*archive*') { continue }   # records of earlier editions, not live harnesses
  if ($name -in 'CostumeSheet','GlossaryCheck','ObjectActors','ScriptDump','DemoProbe','DemoPreviewCheck') { continue }   # tools with their own arguments, not harnesses
  if (Select-String -Path $results -Pattern ("^\\?" + [regex]::Escape($key) + "`t") -Quiet) { continue }
  $b = & dotnet build $p.FullName -nologo -v q 2>&1
  if ($LASTEXITCODE -ne 0) { "$key`tFAIL`t-`t0`t$(($b | Select-Object -Last 2) -join ' / ')" | Out-File $results -Append -Encoding utf8; continue }
  $exe = Get-ChildItem (Join-Path $root "Exe\$name\Debug") -Recurse -Filter "$name.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
  $out = Join-Path $outRoot $key.Replace('\','_')
  New-Item -ItemType Directory -Force $out | Out-Null
  if (-not $exe) { "$key`tok`t-`t0`tno exe" | Out-File $results -Append -Encoding utf8; continue }
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $psi = New-Object Diagnostics.ProcessStartInfo
  $psi.FileName = $exe.FullName; $psi.WorkingDirectory = $root
  $psi.Arguments = "`"$root\Games\loom ega en`" `"$out`""
  $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.UseShellExecute = $false
  $proc = [Diagnostics.Process]::Start($psi)
  $so = $proc.StandardOutput.ReadToEndAsync(); $se = $proc.StandardError.ReadToEndAsync()
  $code = $null; $modal = ''; $prev = ''; $prevAt = 0
  while ($true) {
    if ($proc.WaitForExit(5000)) { $code = $proc.ExitCode; break }
    $t = $sw.Elapsed.TotalSeconds
    $titles = [W2]::Titles([uint32]$proc.Id)
    $extra = ($titles -split '\|' | Where-Object { $_ -and $_ -ne 'SCUMMexplorer' }) -join '|'
    if ($extra -and $extra -eq $prev) { if ($t - $prevAt -ge 30) { $modal = $extra; $code = 'BLOCKED-MODAL'; break } } else { $prev = $extra; $prevAt = $t }
    if ($t -gt $TimeoutSec) { $code = 'TIMEOUT'; break }
  }
  if ($code -is [string]) { & taskkill /PID $proc.Id /T /F | Out-Null; Start-Sleep 1 }
  $sw.Stop()
  $text = ($so.Result + "`n" + $se.Result)
  $text | Out-File (Join-Path $out 'console.log') -Encoding utf8
  $lines = $text -split "`r?`n" | Where-Object { $_.Trim() }
  $note = (($lines | Select-Object -Last 2) -join ' / ')
  if ($modal) { $note = "modal window '$modal' " + $note }
  if ($note.Length -gt 240) { $note = $note.Substring(0, 240) }
  "$key`tok`t$code`t$([int]$sw.Elapsed.TotalSeconds)`t$note" | Out-File $results -Append -Encoding utf8
}
