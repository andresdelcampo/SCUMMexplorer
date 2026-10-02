param([string]$Output='Exe/CertaintyProtocolSession-20261002')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
if(Test-Path -LiteralPath "$Output/room-audit-2"){throw 'Rendering output already exists.'}
& $rtkPath proxy dotnet run --project .verification/RoomAudit/RoomAudit.csproj -c Release -- Games/loom "$Output/room-audit-2" *> "$Output/room-audit-2-run.txt"
if($LASTEXITCODE){throw 'RoomAudit failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/draft-scopes-20261001/compare-pixels.ps1 -Audit "$Output/room-audit-2" -Output "$Output/png-comparison-2.txt"
if($LASTEXITCODE){throw 'Pixel comparison failed.'}
