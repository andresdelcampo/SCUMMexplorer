param([string]$Output='Exe/StatusRefinementsSession-20261001/navigation-1')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtkPath proxy dotnet build .verification/doorway-clicks/DoorwayClickCheck/DoorwayClickCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
& $rtkPath proxy dotnet build .verification/scene-room-controls-20260930/RoomControlsCheck/RoomControlsCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$taskRoot=(Get-Location).Path
$process=Start-Process Exe/DoorwayClickCheck/Release/net10.0-windows/DoorwayClickCheck.exe -WindowStyle Hidden -ArgumentList @('"'+$taskRoot+'\Games\loom"','"'+$taskRoot+'\'+$Output+'\doorways.txt"','2','3','4','8','10','37') -PassThru
$processHandle=$process.Handle
if(!$process.WaitForExit(180000)){throw 'Owned doorway harness timed out.'}
Get-Content ($Output+'/doorways.txt')
if($process.ExitCode){exit $process.ExitCode}
$process=Start-Process Exe/RoomControlsCheck/Release/net10.0-windows/RoomControlsCheck.exe -WindowStyle Hidden -ArgumentList @('"'+$taskRoot+'\Games\loom"','"'+$taskRoot+'\'+$Output+'\controls"') -PassThru
$processHandle=$process.Handle
if(!$process.WaitForExit(180000)){throw 'Owned controls harness timed out.'}
Get-Content ($Output+'/controls/room-controls-check.txt')
exit $process.ExitCode
