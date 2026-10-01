param([string]$Output = 'Exe/VerificationSession-20261001/picker-task1', [switch]$Advanced)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$rtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/global-cutscene-picker-20261001/PickerCheck/PickerCheck.csproj -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }
$arguments = @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"')
if ($Advanced) { $arguments += 'advanced' }
$process = Start-Process -FilePath Exe/PickerCheck/Release/net10.0-windows/PickerCheck.exe -ArgumentList $arguments -Wait -PassThru
Get-Content -LiteralPath ($Output+'/picker-check.txt')
exit $process.ExitCode
