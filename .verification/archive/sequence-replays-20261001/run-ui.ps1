param([string]$Output='Exe/SequenceSession-20261001/ui-1',[switch]$ControlsOnly)
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/archive/sequence-replays-20261001/SequenceUiCheck/SequenceUiCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$arguments=@('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"')
if($ControlsOnly){$arguments+='controls-only'}
$process=Start-Process Exe/SequenceUiCheck/Release/net10.0-windows/SequenceUiCheck.exe -ArgumentList $arguments -Wait -PassThru
Get-Content ($Output+'/sequence-ui-check.txt')
exit $process.ExitCode
