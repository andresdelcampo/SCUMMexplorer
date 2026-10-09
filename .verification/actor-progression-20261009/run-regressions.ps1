$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$taskOutput = Join-Path $PSScriptRoot 'out-regressions'
$env:DOTNET_CLI_HOME = Join-Path $taskOutput 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:SCUMM_EVENTS_DUMP = Join-Path $taskOutput 'events.txt'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
& $taskRtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter 'FullyQualifiedName~LoomCdRunningSetupTests|FullyQualifiedName~LoomStoryEditionTests|FullyQualifiedName~LoomGameStatusTests|FullyQualifiedName~LoomStaffParityTests|FullyQualifiedName~LoomActorLookTests|FullyQualifiedName~LoomSceneSetupPlannerTests|FullyQualifiedName~LoomCdCutsceneTests' --verbosity minimal --logger "trx;LogFileName=actor-regressions.trx" --results-directory $taskOutput
exit $LASTEXITCODE
