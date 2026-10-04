param([string]$Output='Exe/StatusRefinementsSession-20261001/ui-2')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtkPath proxy dotnet build .verification/archive/status-refinements-20261001/StatusRefinementCheck/StatusRefinementCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$taskRoot=(Get-Location).Path
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$process=Start-Process Exe/StatusRefinementCheck/Release/net10.0-windows/StatusRefinementCheck.exe -WindowStyle Hidden -ArgumentList @('"'+$taskRoot+'\Games\loom"','"'+$taskRoot+'\'+$Output+'"') -PassThru
$processHandle=$process.Handle
if(!$process.WaitForExit(240000)){throw 'Owned harness did not finish within four minutes.'}
Get-Content ($Output+'/staff-check.txt')
exit $process.ExitCode
