param([string]$OutputName = 'out-ui-before')
$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskBuild = (Join-Path $PSScriptRoot 'out-ui-build').Replace('\', '/')
$env:DOTNET_CLI_HOME = Join-Path $taskBuild 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $taskRtk proxy dotnet build .verification/actor-progression-20261009/Ui/Ui.csproj --verbosity quiet "-p:BaseOutputPath=$taskBuild/bin/" -p:UseArtifactsOutput=true "-p:ArtifactsPath=$taskBuild"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $taskRtk proxy "$taskBuild/bin/debug/LoomActorUiCheck.exe" 'Games/loom vga en' (Join-Path $PSScriptRoot $OutputName)
exit $LASTEXITCODE
