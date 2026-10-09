param([string]$OutputName = 'out-before', [string]$GameFolder = 'Games/loom vga en')
$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskBuild = (Join-Path $PSScriptRoot 'out-build').Replace('\', '/')
$env:DOTNET_CLI_HOME = Join-Path $taskBuild 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
& $taskRtk proxy dotnet build .verification/room09-speakers-20261009/Ui/Ui.csproj --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false "-p:BaseOutputPath=$taskBuild/bin/" -p:UseArtifactsOutput=true "-p:ArtifactsPath=$taskBuild"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $taskRtk proxy "$taskBuild/bin/debug/LoomRoom09SpeakerCheck.exe" $GameFolder (Join-Path $PSScriptRoot $OutputName)
exit $LASTEXITCODE
