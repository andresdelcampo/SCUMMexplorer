param([string]$Mode = 'levels', [string]$OutputName = 'out-levels-final', [string]$PriorName = 'out-levels')
$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskBuild = (Join-Path $PSScriptRoot 'out-level-build').Replace('\', '/')
$env:DOTNET_CLI_HOME = Join-Path $taskBuild 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $taskRtk proxy dotnet build .verification/earned-progression-20261009/Check/Check.csproj --verbosity quiet "-p:BaseOutputPath=$taskBuild/bin/" -p:UseArtifactsOutput=true "-p:ArtifactsPath=$taskBuild"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Mode -eq 'build') { exit 0 }
& $taskRtk proxy "$taskBuild/bin/debug/LoomEarnedProgressionCheck.exe" Games (Join-Path $PSScriptRoot $OutputName) (Join-Path $PSScriptRoot "$PriorName/levels.json") $Mode
exit $LASTEXITCODE
