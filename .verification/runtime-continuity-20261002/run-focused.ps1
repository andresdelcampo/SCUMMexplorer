param([string]$Output, [string]$Filter='FullyQualifiedName~LoomImplicitAccessTests|FullyQualifiedName~LoomExpressionObservationTests|FullyQualifiedName~LoomSourceAccessTests|FullyQualifiedName~LoomExtendedSourceAccessTests|FullyQualifiedName~LoomObservationBoundaryTests|FullyQualifiedName~LoomCompletionObserverTests|FullyQualifiedName~LoomRuntimeRelationTests|FullyQualifiedName~LoomObservedLineageTests')
$ErrorActionPreference='Stop'
if (!$Output -or (Test-Path -LiteralPath $Output)) { throw 'Use a fresh output folder.' }
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
New-Item -ItemType Directory -Path $Output | Out-Null
$env:SCUMM_SOUND_TEST_ROOT=Join-Path $PWD 'Games/loom'
$env:SCUMM_INDY3_EGA_TEST_ROOT=Join-Path $PWD 'Games/IndyCru/EGA'
& $rtkPath proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -c Release --filter $Filter *> "$Output/tests.txt"
$result=$LASTEXITCODE
Get-Content "$Output/tests.txt" | Select-Object -Last 24
if ($result) { throw 'Focused tests failed; diagnostics retained.' }
