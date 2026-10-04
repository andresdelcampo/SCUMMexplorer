param([string]$Output, [string]$BuildRoot='Exe/SoundClockValidation')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$root=(Get-Location).Path
& $rtk proxy dotnet build .verification/archive/sound-lifetimes-20261001/SoundLifetimeCheck/SoundLifetimeCheck.csproj -c Release ('-p:BaseOutputPath='+$root.Replace('\','/')+'/'+$BuildRoot+'/')
if($LASTEXITCODE){exit $LASTEXITCODE}
$process=Start-Process ($BuildRoot+'/Release/net10.0-windows/SoundLifetimeCheck.exe') -ArgumentList @('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"') -Wait -PassThru
Get-Content ($Output+'/sound-lifetime-check.txt')
exit $process.ExitCode
