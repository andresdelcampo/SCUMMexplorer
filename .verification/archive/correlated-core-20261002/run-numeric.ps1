param([string]$Output='Exe/CorrelatedCoreSession-20261002')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
if(Test-Path -LiteralPath $Output){throw 'Numeric output already exists.'}
New-Item -ItemType Directory -Path $Output | Out-Null
& $rtkPath proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -c Release --filter 'FullyQualifiedName~LoomCorrelatedNumericTests' *> "$Output/numeric-tests.txt"
if($LASTEXITCODE){throw 'Numeric tests failed.'}
& $rtkPath proxy dotnet run --project .verification/archive/correlated-core-20261002/NumericAudit/NumericAudit.csproj -c Release -- Games/loom "$Output/numeric-worlds.json" *> "$Output/numeric-run.txt"
if($LASTEXITCODE){throw 'Native source numeric audit failed.'}
Get-Content -LiteralPath "$Output/numeric-run.txt"
