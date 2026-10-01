param([string]$Output='Exe/VerificationSession-20261001/room-replays-ui-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/room-replays-20261001/RoomReplayCheck/RoomReplayCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$process=Start-Process Exe/RoomReplayCheck/Release/net10.0-windows/RoomReplayCheck.exe -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/room-replays-check.txt')
exit $process.ExitCode
