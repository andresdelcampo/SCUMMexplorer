param([string]$Output='Exe/StaffCorrectionSession-20261001/ui-1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/archive/staff-correction-20261001/StaffCheck/StaffCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$process=Start-Process Exe/StaffCheck/Release/net10.0-windows/StaffCheck.exe -WindowStyle Hidden -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/staff-check.txt')
exit $process.ExitCode
