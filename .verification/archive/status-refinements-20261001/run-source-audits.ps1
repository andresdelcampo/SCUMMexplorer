param([string]$Output='Exe/StatusRefinementsSession-20261001/audits-1')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
New-Item -ItemType Directory -Path $Output -Force | Out-Null
& $rtkPath proxy dotnet run --project .verification/replay-coverage-20261001/ReplayAudit/ReplayAudit.csproj -c Release -- Games/loom Exe/VerificationSession-20261001/native-roots-1.json ($Output+'/literal') *> ($Output+'/literal-run.txt')
if($LASTEXITCODE){throw 'Literal audit failed.'}
& $rtkPath proxy dotnet run --project .verification/room-replays-20261001/MembershipAudit/MembershipAudit.csproj -c Release -- Games/loom .verification/room-replays-20261001/established-starts.json ($Output+'/established.json') *> ($Output+'/established-run.txt')
if($LASTEXITCODE){throw 'Membership audit failed.'}
& $rtkPath proxy dotnet run --project .verification/archive/sequence-replays-20261001/SequenceAudit/SequenceAudit.csproj -c Release -- Games/loom .verification/archive/sequence-replays-20261001/sequences.json ($Output+'/sequences.json') *> ($Output+'/sequences-run.txt')
if($LASTEXITCODE){throw 'Sequence audit failed.'}
& $rtkPath proxy dotnet run --project .verification/random-picks/Blockers/Blockers.csproj -c Release -- Games/loom ($Output+'/blockers.txt') *> ($Output+'/blockers-run.txt')
if($LASTEXITCODE){throw 'Blockers audit failed.'}
