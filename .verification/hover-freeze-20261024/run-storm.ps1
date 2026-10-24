# Usage: run-storm.ps1 <label> <game folder name under D:\Juegos\Lucas> <room> [seconds per pass] [normal|advanced]
# Runs HoverStorm (build it first) with a hard kill after 7 minutes; prints storm.txt. Output: out-<label>\<game>-<room>\storm.txt
param([string]$Label, [string]$Game, [int]$Room, [int]$Seconds = 4, [string]$Only = 'both', [string]$Intro = '')
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $root '..\..')
$lucas = if ($env:SCUMM_GAMES_ROOT) { $env:SCUMM_GAMES_ROOT } else { 'D:\Juegos\Lucas' }
$out = Join-Path $root "out-$Label\$($Game -replace ' ','_')-$Room"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
$exe = Join-Path $repo 'Exe\HoverStorm\Debug\net10.0-windows\HoverStorm.exe'
$arguments = @("`"$lucas\$Game`"", "`"$out`"", "$Room", "$Seconds")
$arguments += $Only; if ($Intro) { $arguments += $Intro }  # Only: normal|advanced|both; Intro: 'intro' = press Play Game Intro first
$p = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
if (-not $p.WaitForExit(420000)) { $p.Kill(); 'KILLED after 420 s' }
"exit $($p.ExitCode)"
Get-Content (Join-Path $out 'storm.txt') -ErrorAction SilentlyContinue
