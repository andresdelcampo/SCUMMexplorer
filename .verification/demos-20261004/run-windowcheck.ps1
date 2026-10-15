# Opens a LOOM demo folder in the real MainWindow (WindowCheck) and shows the output or the error.
# Usage: run-windowcheck.ps1 short|long <out folder> <room>...
param([string]$Demo, [string]$Out, [int[]]$Rooms)
$games = if ($env:SCUMM_GAMES_ROOT) { $env:SCUMM_GAMES_ROOT } else { 'D:\Juegos\Lucas' }   # game data outside the repository since 15 October 2026
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$exe = Join-Path $root "Exe\WindowCheck\Debug\net10.0-windows\WindowCheck.exe"
$folder = Join-Path $games "loom demo $Demo ega en"
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
$stdout = "$Out.stdout.txt"; $stderr = "$Out.stderr.txt"
$arguments = @("`"$folder`"", "`"$Out`"") + ($Rooms | ForEach-Object { "$_" })
$p = Start-Process $exe -ArgumentList $arguments -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if (-not $p.WaitForExit(120000)) { $p.Kill(); "TIMEOUT (a modal window is probably open)" }
"exit $($p.ExitCode)"
if (Test-Path $stdout) { Get-Content $stdout }
if (Test-Path $stderr) { Get-Content $stderr | Select-Object -First 10 }
if (Test-Path "$Out\error.txt") { Get-Content "$Out\error.txt" | Select-Object -First 12 }
if (Test-Path $Out) { "files: " + ((Get-ChildItem $Out | ForEach-Object Name) -join ", ") }
