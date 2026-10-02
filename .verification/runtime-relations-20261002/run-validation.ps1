param([string]$Output='Exe/RuntimeRelationsReproduction-20261002')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if(Test-Path -LiteralPath $Output){throw 'Use a fresh preservation output base.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/source-progression-20261001/run-checks.ps1 -OutputFolder "$Output/checks-final"
if($LASTEXITCODE){throw 'Core/final API checks failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/runtime-relations-20261002/run-evidence.ps1 -Output "$Output/final-evidence"
if($LASTEXITCODE){throw 'Native relation/binding/numeric evidence failed.'}
& $rtkPath proxy $pythonPath -X utf8 .verification/runtime-relations-20261002/analyze_relations.py "$Output/final-evidence"
if($LASTEXITCODE){throw 'Native evidence comparison failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-source-audits.ps1 -Output "$Output/audits-1"
if($LASTEXITCODE){throw 'Roots/memberships/sequences/Blockers failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/certainty-protocol-20261002/run-rendering.ps1 -Output $Output
if($LASTEXITCODE){throw 'Core room rendering comparison failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/completion-observer-20261002/run-final-build.ps1 -Output $Output
if($LASTEXITCODE){throw 'Final main Release/process check failed.'}
& $rtkPath proxy $pythonPath -X utf8 .verification/runtime-relations-20261002/finalize_checks.py $Output "$Output/final-evidence"
if($LASTEXITCODE){throw 'Final completed report aggregation failed.'}
