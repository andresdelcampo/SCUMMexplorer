param([string]$Output='Exe/DraftScopeSession-20261001/regressions-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
New-Item -ItemType Directory -Path $Output | Out-Null
$env:SCUMM_SOUND_TEST_ROOT="$PWD\Games\loom"
$env:SCUMM_INDY3_EGA_TEST_ROOT="$PWD\Games\IndyCru\EGA"
& $rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -c Release --filter 'FullyQualifiedName!~LoomObjectLookAuditTests' *> ($Output+'/tests.txt')
if($LASTEXITCODE){Get-Content ($Output+'/tests.txt');exit $LASTEXITCODE}
$root=(Get-Location).Path
foreach($name in @('ReplayCheck','RoomReplayCheck')) {
    $project=if($name -eq 'ReplayCheck'){'.verification/replay-coverage-20261001/ReplayCheck/ReplayCheck.csproj'}else{'.verification/room-replays-20261001/RoomReplayCheck/RoomReplayCheck.csproj'}
    & $rtk proxy dotnet build $project -c Release *> ($Output+'/'+$name+'-build.txt')
    if($LASTEXITCODE){Get-Content ($Output+'/'+$name+'-build.txt');exit $LASTEXITCODE}
    $arguments=@('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'\'+$name+'"','334')
    if($name -eq 'ReplayCheck'){$arguments+=@('84','250','50')}
    $process=Start-Process ('Exe/'+$name+'/Release/net10.0-windows/'+$name+'.exe') -ArgumentList $arguments -Wait -PassThru
    if($process.ExitCode){exit $process.ExitCode}
}
& $rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/replay-coverage-20261001/run-regressions.ps1 -Output ($Output+'/controls') *> ($Output+'/controls-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/controls-run.txt');exit $LASTEXITCODE}
& $rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/automatic-pointer-20261001/run.ps1 -Output ($Output+'/input') *> ($Output+'/input-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/input-run.txt');exit $LASTEXITCODE}
& $rtk proxy dotnet build .verification/archive/audio-modes-20260930/AudioCheck/AudioCheck.csproj -c Release *> ($Output+'/audio-build.txt')
if($LASTEXITCODE){exit $LASTEXITCODE}
$process=Start-Process Exe/AudioCheck/Release/net10.0-windows/AudioCheck.exe -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'\audio"') -Wait -PassThru
if($process.ExitCode){exit $process.ExitCode}
$audioReport=Get-Content ($Output+'/audio/audio-check.txt') -Raw
if($audioReport -notmatch 'Failures: 0' -or $audioReport -match 'FAIL '){throw 'Audio report failed (legacy harness exit alone is insufficient).'}
& $rtk proxy dotnet run --project .verification/random-picks/Blockers/Blockers.csproj -c Release -- Games/loom ($Output+'/blockers.txt') *> ($Output+'/blockers-run.txt')
if($LASTEXITCODE){exit $LASTEXITCODE}
& $rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/draft-scopes-20261001/compare-pixels.ps1
exit $LASTEXITCODE
