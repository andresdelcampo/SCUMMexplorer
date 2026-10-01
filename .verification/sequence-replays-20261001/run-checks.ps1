param([string]$Output='Exe/SequenceSession-20261001/shared-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$env:SCUMM_SOUND_TEST_ROOT="$PWD\Games\loom"
$env:SCUMM_INDY3_EGA_TEST_ROOT="$PWD\Games\IndyCru\EGA"
& $rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -c Release --filter 'FullyQualifiedName!~LoomObjectLookAuditTests'
if($LASTEXITCODE){exit $LASTEXITCODE}
foreach($name in @('ReplayCheck','RoomReplayCheck')) {
    $project=if($name -eq 'ReplayCheck'){'.verification/replay-coverage-20261001/ReplayCheck/ReplayCheck.csproj'}else{'.verification/room-replays-20261001/RoomReplayCheck/RoomReplayCheck.csproj'}
    & $rtk proxy dotnet build $project -c Release
    if($LASTEXITCODE){exit $LASTEXITCODE}
    $root=(Get-Location).Path
    $arguments=@('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'\'+$name+'"','331')
    if($name -eq 'ReplayCheck'){$arguments+=@('82','249','55')}
    $process=Start-Process ('Exe/'+$name+'/Release/net10.0-windows/'+$name+'.exe') -ArgumentList $arguments -Wait -PassThru
    if($process.ExitCode){exit $process.ExitCode}
}
