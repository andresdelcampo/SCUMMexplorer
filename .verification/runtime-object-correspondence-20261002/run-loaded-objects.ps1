param([string]$Output, [string]$RecipeFile='.verification/room41-certainty-20261002/room41.json',
    [string]$RecipeId='room41-raw4-click')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (!$Output -or (Test-Path -LiteralPath $Output)) { throw 'Use a fresh loaded-object output.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/runtime-object-correspondence-20261002/create_loaded_object_audit.py "$Output/observer-1"
if ($LASTEXITCODE) { throw 'Loaded-object generation failed.' }
$recipes=Get-Content -LiteralPath $RecipeFile -Raw | ConvertFrom-Json
$selected=@($recipes | Where-Object id -eq $RecipeId)
if ($selected.Count -ne 1) { throw 'One exact preserved recipe required.' }
[IO.File]::WriteAllText((Join-Path $PWD "$Output/recipe.json"),(ConvertTo-Json -InputObject $selected -Depth 50))
& $rtkPath proxy dotnet build "$Output/observer-1/CompletionAudit.csproj" -c Release *> "$Output/build.txt"
if ($LASTEXITCODE) { throw 'Loaded-object build failed.' }
& $rtkPath proxy dotnet run --project "$Output/observer-1/CompletionAudit.csproj" -c Release --no-build -- Games/loom "$Output/recipe.json" "$Output/route-1" *> "$Output/run.txt"
if ($LASTEXITCODE) { throw 'Loaded-object recipe failed.' }
Get-Content "$Output/run.txt"
