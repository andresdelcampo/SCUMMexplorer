param([string]$Output='Exe/CompletionObserverSession-20261002')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtkPath proxy dotnet build SCUMMexplorer.slnx -c Release *> "$Output/final-release-build.txt"
if($LASTEXITCODE){throw 'Final main Release build failed.'}
$instances=@(Get-Process -Name 'SCUMMexplorer.App' -ErrorAction SilentlyContinue)
if($instances.Count){throw 'An unexpected App instance is running.'}
[IO.File]::WriteAllText((Join-Path $PWD "$Output/app-state.txt"),'SCUMMexplorer processes: 0'+[Environment]::NewLine)
Get-Content -LiteralPath "$Output/final-release-build.txt" | Select-Object -Last 6
