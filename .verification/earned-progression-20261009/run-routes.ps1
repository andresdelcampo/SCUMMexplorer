param([string]$Route = 'dump', [string]$Edition = 'ega-en', [string]$OutputName = 'out-routes-source')
$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskBuild = (Join-Path $PSScriptRoot 'out-routes-build').Replace('\', '/')
$env:DOTNET_CLI_HOME = Join-Path $taskBuild 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $taskRtk proxy dotnet build .verification/earned-progression-20261009/Routes/Routes.csproj --verbosity quiet "-p:BaseOutputPath=$taskBuild/bin/" -p:UseArtifactsOutput=true "-p:ArtifactsPath=$taskBuild"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Route -eq 'build') { exit 0 }
& $taskRtk proxy "$taskBuild/bin/debug/LoomEarnedRoutes.exe" Games (Join-Path $PSScriptRoot $OutputName) $Route $Edition
exit $LASTEXITCODE
