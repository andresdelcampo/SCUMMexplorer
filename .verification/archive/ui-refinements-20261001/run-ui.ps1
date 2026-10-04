param([string]$Output='Exe/UiRefinementSession-20261001/placement-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/archive/ui-refinements-20261001/RefinementCheck/RefinementCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$process=Start-Process Exe/RefinementCheck/Release/net10.0-windows/RefinementCheck.exe -WindowStyle Hidden -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/refinement-check.txt')
exit $process.ExitCode
