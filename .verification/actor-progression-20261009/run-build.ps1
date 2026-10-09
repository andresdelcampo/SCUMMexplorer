$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskBuild = (Join-Path $PSScriptRoot 'out-solution-build').Replace('\', '/')
$env:DOTNET_CLI_HOME = Join-Path $taskBuild 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $taskBuild | Out-Null
$ErrorActionPreference = 'Continue' # Windows PowerShell maps native stderr to ErrorRecord.
& $taskRtk proxy dotnet build SCUMMexplorer.slnx -c Release --verbosity quiet "-p:BaseOutputPath=$taskBuild/bin/" -p:UseArtifactsOutput=true "-p:ArtifactsPath=$taskBuild" 2>&1 | Tee-Object -FilePath (Join-Path $taskBuild 'build.log')
exit $LASTEXITCODE
