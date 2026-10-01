param([string]$Output='Exe/GameStatusSession-20261001/ui-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/game-status-20261001/StatusUiCheck/StatusUiCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$process=Start-Process Exe/StatusUiCheck/Release/net10.0-windows/StatusUiCheck.exe -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/status-ui-check.txt');exit $process.ExitCode
