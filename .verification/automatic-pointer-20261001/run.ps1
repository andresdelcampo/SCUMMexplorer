param([string]$Output='Exe/VerificationSession-20261001/automatic-pointer-run1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/automatic-pointer-20261001/InputCheck/InputCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$process=Start-Process Exe/InputCheck/Release/net10.0-windows/InputCheck.exe -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/input-check.txt')
exit $process.ExitCode
