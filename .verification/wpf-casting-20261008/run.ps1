param([ValidatePattern('^out-[a-zA-Z0-9-]+$')][string]$OutputName = 'out-reproduction')
$ErrorActionPreference = 'Stop'
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskBuild = (Join-Path $PSScriptRoot 'out-build').Replace('\', '/')
$taskOutput = Join-Path $PSScriptRoot $OutputName
$env:DOTNET_CLI_HOME = Join-Path $taskBuild 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:SCUMM_HARNESS_FAILURE_LOG = Join-Path $taskOutput 'failures.log'
Set-Location -LiteralPath $taskRepository
& $taskRtk proxy dotnet build .verification/wpf-casting-20261008/Check/Check.csproj --verbosity quiet "-p:BaseOutputPath=$taskBuild/bin/" -p:UseArtifactsOutput=true "-p:ArtifactsPath=$taskBuild"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $taskRtk proxy "$taskBuild/bin/debug/LoomWpfCastingCheck.exe" Games $taskOutput
exit $LASTEXITCODE
