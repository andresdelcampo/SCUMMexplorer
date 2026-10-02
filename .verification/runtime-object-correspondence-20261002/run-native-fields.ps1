param([string]$Output)
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$pythonPath='C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (!$Output -or (Test-Path -LiteralPath $Output)) { throw 'Use a fresh native field output.' }
& $rtkPath proxy $pythonPath -X utf8 .verification/runtime-object-correspondence-20261002/create_field_audit.py "$Output/field-audit"
if ($LASTEXITCODE) { throw 'Field generator failed.' }
& $rtkPath proxy dotnet build "$Output/field-audit/ExpressionAudit.csproj" -c Release *> "$Output/build.txt"
if ($LASTEXITCODE) { throw 'Field build failed.' }
& $rtkPath proxy dotnet run --project "$Output/field-audit/ExpressionAudit.csproj" -c Release --no-build -- Games/loom "$Output/fields.json" .verification/source-progression-20261002/boot.json *> "$Output/run.txt"
if ($LASTEXITCODE) { throw 'Field audit failed.' }
Get-Content "$Output/run.txt"
