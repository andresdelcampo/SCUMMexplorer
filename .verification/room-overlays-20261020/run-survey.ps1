# Runs OverlaySurvey over one copy of every game with a live session. Usage: run-survey.ps1 <label> [game folder names...]
# Writes .verification/room-overlays-20261020/out-<label>/<game>/survey.txt and survey-<label>.txt (the SUMMARY lines).
param([string]$Label = 'after', [string[]]$Games)
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $root '..\..')
$exe = Get-ChildItem -Recurse -Filter OverlaySurvey.exe (Join-Path $repo 'Exe\OverlaySurvey') | Select-Object -First 1
$lucas = if ($env:SCUMM_GAMES_ROOT) { $env:SCUMM_GAMES_ROOT } else { 'D:\Juegos\Lucas' }
$all = [ordered]@{
  'loom ega mt32 en' = '13'; 'loom vga en' = '13'; 'indy3 ega en' = ''; 'indy3 vga en' = ''; 'monkey1 ega en' = ''; 'monkey1 vga en' = '';
  'mi2 mt32 en' = ''; 'indy4 vga en' = ''; 'mmansion ega en' = ''; 'mmansionE ega es' = ''; 'zak ega en' = ''; 'zakE ega en' = ''
}
if (-not $Games) { $Games = @($all.Keys) }
$lines = @()
foreach ($g in $Games) {
  $out = Join-Path $root "out-$Label\$g"
  if (Test-Path $out) { Remove-Item -Recurse -Force $out }
  $arguments = @("`"$lucas\$g`"", "`"$out`"")
  if ($all[$g]) { $arguments += $all[$g] }
  $p = Start-Process -FilePath $exe.FullName -ArgumentList $arguments -PassThru -Wait
  $summary = Get-Content (Join-Path $out 'survey.txt') -ErrorAction SilentlyContinue | Where-Object { $_ -like 'SUMMARY*' }
  if (-not $summary) { $summary = 'FAILED: ' + ((Get-Content (Join-Path $out 'survey.txt') -ErrorAction SilentlyContinue | Select-Object -Last 3) -join ' ') }
  $lines += "$g (exit $($p.ExitCode)): $summary"
}
$lines | Set-Content -Encoding utf8 (Join-Path $root "survey-$Label.txt")
$lines
