param([string]$Output='Exe/VerificationSession-20261001/replay-regressions')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/doorway-clicks/DoorwayClickCheck/DoorwayClickCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
& $rtk proxy dotnet build .verification/archive/scene-room-controls-20260930/RoomControlsCheck/RoomControlsCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
New-Item -ItemType Directory -Path $Output | Out-Null
$root=(Get-Location).Path
$process=Start-Process Exe/DoorwayClickCheck/Release/net10.0-windows/DoorwayClickCheck.exe -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'\doorways.txt"','2','3','4','8','10','37') -Wait -PassThru
if($process.ExitCode){exit $process.ExitCode}
Get-Content ($Output+'/doorways.txt')
$process=Start-Process Exe/RoomControlsCheck/Release/net10.0-windows/RoomControlsCheck.exe -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'\controls"') -Wait -PassThru
Get-Content ($Output+'/controls/room-controls-check.txt')
exit $process.ExitCode
