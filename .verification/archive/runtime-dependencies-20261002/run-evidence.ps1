param([string]$Output='Exe/RuntimeDependenciesFinalSession-20261002/final-evidence')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if(Test-Path -LiteralPath $Output){throw 'Use a fresh evidence output folder.'}
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-dependencies-20261002/create_audit.py "$Output/observer-1"
if($LASTEXITCODE){throw 'Dependency observer generation failed.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/completion-observer-20261002/run-recipes.ps1 -Output $Output
if($LASTEXITCODE){throw 'Observed recipes failed.'}
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-dependencies-20261002/create_audit.py "$Output/expanded-1" --expanded
if($LASTEXITCODE){throw 'Expanded observer generation failed.'}
$project="$Output/expanded-1/CompletionAudit.csproj"
& $rtkPath proxy dotnet build $project -c Release *> "$Output/expanded-build.txt"
if($LASTEXITCODE){throw 'Expanded build failed.'}
$routes=Get-Content .verification/archive/room41-certainty-20261002/room41.json -Raw | ConvertFrom-Json
[IO.File]::WriteAllText((Join-Path $PWD "$Output/raw4-only.json"),(ConvertTo-Json -InputObject @($routes | Where-Object id -eq 'room41-raw4-click') -Depth 50))
& $rtkPath proxy dotnet run --project $project -c Release --no-build -- Games/loom "$Output/raw4-only.json" "$Output/expanded-room41-1" *> "$Output/expanded-run.txt"
if($LASTEXITCODE){throw 'Expanded recipe failed.'}
& $rtkPath proxy dotnet run --project .verification/archive/correlated-core-20261002/NumericAudit/NumericAudit.csproj -c Release -- Games/loom "$Output/numeric-fresh.json" *> "$Output/numeric-run.txt"
if($LASTEXITCODE){throw 'Numeric source audit failed.'}
