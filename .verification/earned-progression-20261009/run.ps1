param([string]$OutputName = 'out-lessons-cd', [string]$PriorName = 'out-lessons-final')
$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskBuild = (Join-Path $PSScriptRoot 'out-build').Replace('\', '/')
$env:DOTNET_CLI_HOME = Join-Path $taskBuild 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $taskRtk proxy dotnet build .verification/earned-progression-20261009/Check/Check.csproj --verbosity quiet "-p:BaseOutputPath=$taskBuild/bin/" -p:UseArtifactsOutput=true "-p:ArtifactsPath=$taskBuild"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskPrior = Join-Path $PSScriptRoot "$PriorName/lessons.json"
if (Test-Path -LiteralPath $taskPrior) {
    & $taskRtk proxy "$taskBuild/bin/debug/LoomEarnedProgressionCheck.exe" Games (Join-Path $PSScriptRoot $OutputName) $taskPrior
} else {
    & $taskRtk proxy "$taskBuild/bin/debug/LoomEarnedProgressionCheck.exe" Games (Join-Path $PSScriptRoot $OutputName)
}
exit $LASTEXITCODE
