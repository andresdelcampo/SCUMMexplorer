param([string]$Output='Exe/SourceProgressionSession-20261002/input-1')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtkPath proxy dotnet build .verification/automatic-pointer-20261001/InputCheck/InputCheck.csproj -c Release
if($LASTEXITCODE){throw 'InputCheck build failed'}
$taskRoot=(Get-Location).Path
$process=Start-Process Exe/InputCheck/Release/net10.0-windows/InputCheck.exe -WindowStyle Hidden -ArgumentList @('"'+$taskRoot+'\Games\loom"','"'+$taskRoot+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/input-check.txt')
if($process.ExitCode){throw 'InputCheck failed'}
