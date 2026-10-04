param([string]$Output='Exe/DraftScopeSession-20261001/final-audits-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
New-Item -ItemType Directory -Path $Output | Out-Null
$env:SCUMM_SOUND_TEST_ROOT="$PWD\Games\loom"
$env:SCUMM_INDY3_EGA_TEST_ROOT="$PWD\Games\IndyCru\EGA"
& $rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -c Release --filter 'FullyQualifiedName!~LoomObjectLookAuditTests' *> ($Output+'/tests.txt')
if($LASTEXITCODE){Get-Content ($Output+'/tests.txt');exit $LASTEXITCODE}
& $rtk proxy dotnet run --project .verification/replay-coverage-20261001/ReplayAudit/ReplayAudit.csproj -c Release -- Games/loom Exe/VerificationSession-20261001/native-roots-1.json ($Output+'/literal') *> ($Output+'/literal-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/literal-run.txt') -Tail 10;exit $LASTEXITCODE}
& $rtk proxy dotnet run --project .verification/room-replays-20261001/MembershipAudit/MembershipAudit.csproj -c Release -- Games/loom .verification/room-replays-20261001/established-starts.json ($Output+'/established.json') *> ($Output+'/established-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/established-run.txt');exit $LASTEXITCODE}
& $rtk proxy dotnet run --project .verification/archive/sequence-replays-20261001/SequenceAudit/SequenceAudit.csproj -c Release -- Games/loom .verification/archive/sequence-replays-20261001/sequences.json ($Output+'/sequences.json') *> ($Output+'/sequences-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/sequences-run.txt');exit $LASTEXITCODE}
& $rtk proxy dotnet run --project .verification/RoomAudit/RoomAudit.csproj -c Release -- Games/loom ($Output+'/room-audit') *> ($Output+'/room-run.txt')
exit $LASTEXITCODE
