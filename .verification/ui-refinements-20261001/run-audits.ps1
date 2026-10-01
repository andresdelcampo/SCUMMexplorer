param([string]$Output='Exe/UiRefinementSession-20261001/audits-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
New-Item -ItemType Directory -Path $Output | Out-Null
& $rtk proxy dotnet run --project .verification/replay-coverage-20261001/ReplayAudit/ReplayAudit.csproj -c Release -- Games/loom Exe/VerificationSession-20261001/native-roots-1.json ($Output+'/literal') *> ($Output+'/literal-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/literal-run.txt') -Tail 10;exit $LASTEXITCODE}
& $rtk proxy dotnet run --project .verification/room-replays-20261001/MembershipAudit/MembershipAudit.csproj -c Release -- Games/loom .verification/room-replays-20261001/established-starts.json ($Output+'/established.json') *> ($Output+'/established-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/established-run.txt');exit $LASTEXITCODE}
& $rtk proxy dotnet run --project .verification/sequence-replays-20261001/SequenceAudit/SequenceAudit.csproj -c Release -- Games/loom .verification/sequence-replays-20261001/sequences.json ($Output+'/sequences.json') *> ($Output+'/sequences-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/sequences-run.txt');exit $LASTEXITCODE}
& $rtk proxy dotnet run --project .verification/RoomAudit/RoomAudit.csproj -c Release -- Games/loom ($Output+'/room-audit') *> ($Output+'/room-run.txt')
if($LASTEXITCODE){Get-Content ($Output+'/room-run.txt');exit $LASTEXITCODE}
& $rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/draft-scopes-20261001/compare-pixels.ps1 -Audit ($Output+'/room-audit') -Output ($Output+'/png-comparison.txt')
exit $LASTEXITCODE
