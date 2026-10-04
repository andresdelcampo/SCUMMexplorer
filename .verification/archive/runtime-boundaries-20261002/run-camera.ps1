param([string]$Output)
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (!$Output -or (Test-Path -LiteralPath $Output)) { throw 'Use a fresh separate camera output.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-boundaries-20261002/create_camera_audit.py "$Output/observer-1"
if ($LASTEXITCODE) { throw 'Camera generation failed.' }
$recipes=Get-Content .verification/archive/room41-certainty-20261002/room41.json -Raw | ConvertFrom-Json
$selected=@($recipes | Where-Object id -eq 'room41-raw4-click')
if ($selected.Count -ne 1) { throw 'One unchanged raw4 recipe required.' }
[IO.File]::WriteAllText((Join-Path $PWD "$Output/recipe.json"),(ConvertTo-Json -InputObject $selected -Depth 50))
& $rtkPath proxy dotnet build "$Output/observer-1/CompletionAudit.csproj" -c Release *> "$Output/build.txt"
if ($LASTEXITCODE) { throw 'Camera build failed.' }
& $rtkPath proxy dotnet run --project "$Output/observer-1/CompletionAudit.csproj" -c Release --no-build -- Games/loom "$Output/recipe.json" "$Output/route-1" *> "$Output/run.txt"
if ($LASTEXITCODE) { throw 'Separate camera recipe failed.' }
Get-Content "$Output/run.txt"
