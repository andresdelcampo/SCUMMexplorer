param([string]$Output='Exe/RuntimeAccessReproduction-20261002')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if(Test-Path -LiteralPath $Output){throw 'Use a fresh preservation output base.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-access-20261002/run-api.ps1 -Output "$Output/checks-final"
if($LASTEXITCODE){throw 'Core/Release failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-access-20261002/run-evidence.ps1 -Output "$Output/final-evidence"
if($LASTEXITCODE){throw 'Observed evidence failed.'}
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-access-20261002/analyze_access.py "$Output/final-evidence"
if($LASTEXITCODE){throw 'Evidence comparison failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/status-refinements-20261001/run-source-audits.ps1 -Output "$Output/audits-1"
if($LASTEXITCODE){throw 'Roots/memberships/sequences/Blockers failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/certainty-protocol-20261002/run-rendering.ps1 -Output $Output
if($LASTEXITCODE){throw 'Core rendering failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/completion-observer-20261002/run-final-build.ps1 -Output $Output
if($LASTEXITCODE){throw 'Final main Release/process check failed.'}
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-access-20261002/finalize_checks.py $Output
if($LASTEXITCODE){throw 'Completed report aggregation failed.'}
