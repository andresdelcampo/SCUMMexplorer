param([string]$Output, [string]$ProjectFolder='passive-expressions-1')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (Test-Path -LiteralPath "$Output/passive-expressions.json") { throw 'Use a fresh passive report.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/archive/runtime-continuity-20261002/create_expression_audit.py "$Output/$ProjectFolder"
if ($LASTEXITCODE) { throw 'Passive generator failed.' }
& $rtkPath proxy dotnet build "$Output/$ProjectFolder/ExpressionAudit.csproj" -c Release *> "$Output/passive-expressions-build.txt"
if ($LASTEXITCODE) { throw 'Passive build failed.' }
$ErrorActionPreference='Continue'
& $rtkPath proxy dotnet run --project "$Output/$ProjectFolder/ExpressionAudit.csproj" -c Release --no-build -- Games/loom "$Output/passive-expressions.json" .verification/archive/source-progression-20261002/boot.json *> "$Output/passive-expressions-run.txt"
$result=$LASTEXITCODE
$ErrorActionPreference='Stop'
if ($result) { throw 'Passive audit failed; complete diagnostics retained.' }
Copy-Item -LiteralPath "$Output/$ProjectFolder/source-manifest.json" -Destination "$Output/passive-expression-source-manifest.json"
Get-Content "$Output/passive-expressions-run.txt"
