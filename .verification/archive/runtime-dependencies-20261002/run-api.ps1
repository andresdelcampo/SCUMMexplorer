param([string]$Output='Exe/RuntimeDependenciesSession-20261002/api-1')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
if(Test-Path -LiteralPath $Output){throw 'Use a fresh API output folder.'}
& $rtkPath proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/source-progression-20261001/run-checks.ps1 -OutputFolder $Output
if($LASTEXITCODE){throw 'Core/Release checks failed.'}
