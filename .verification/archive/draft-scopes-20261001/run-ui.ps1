param([string]$Output='Exe/DraftScopeSession-20261001/ui-1',[switch]$Room24Only)
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/archive/draft-scopes-20261001/ScopeUiCheck/ScopeUiCheck.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
$root=(Get-Location).Path
$arguments=@('"'+$root+'\Games\loom"','"'+$root+'\'+$Output+'"')
if($Room24Only){$arguments+='room24-only'}
$process=Start-Process Exe/ScopeUiCheck/Release/net10.0-windows/ScopeUiCheck.exe -ArgumentList $arguments -Wait -PassThru
Get-Content ($Output+'/scope-ui-check.txt');exit $process.ExitCode
