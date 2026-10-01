param([string]$Output='Exe/VerificationSession-20261001/replay-ui-run3')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/replay-coverage-20261001/ReplayCheck/ReplayCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$process=Start-Process Exe/ReplayCheck/Release/net10.0-windows/ReplayCheck.exe -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/replay-check.txt')
exit $process.ExitCode
