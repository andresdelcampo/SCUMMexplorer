# Run from the SCUMMexplorer repository root. Local game data is required; output is ignored by Git.
$ErrorActionPreference = 'Stop'
$taskRtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
function Run-Rtk {
    & $taskRtk proxy @args
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" }
}
$auditOutput = '.verification/correctness-20261008/out/rerun'
Run-Rtk dotnet build SCUMMexplorer.slnx --verbosity quiet
Run-Rtk dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --logger 'trx;LogFileName=correctness-rerun.trx' --results-directory .verification/correctness-20261008/results --verbosity quiet
Run-Rtk dotnet build .verification/correctness-20261008/Audit/Audit.csproj --verbosity quiet
foreach ($edition in @('ega', 'vga')) {
    $gameFolder = if ($edition -eq 'ega') { 'Games/loom ega mt32 en' } else { 'Games/loom vga en' }
    Run-Rtk dotnet run --project .verification/correctness-20261008/Audit/Audit.csproj --no-build -- $gameFolder "$auditOutput/$edition" src/SCUMMexplorer.App/loom-replays.json all
    Run-Rtk dotnet run --project .verification/correctness-20261008/Audit/Audit.csproj --no-build -- $gameFolder "$auditOutput/$edition-anomalies" - anomalies "$auditOutput/$edition/interactions.json"
}
foreach ($harness in @(
    @('.verification/advanced-scripts/ScriptsCheck/ScriptsCheck.csproj', 'Games', "$auditOutput/scripts-ui", 'report.txt'),
    @('.verification/loom-parity/ParityCheck/ParityCheck.csproj', 'Games', "$auditOutput/parity-ui", 'report.txt'),
    @('.verification/cutscene-playback/CutscenePlaybackCheck/CutscenePlaybackCheck.csproj', 'Games/loom ega mt32 en', "$auditOutput/playback-ui", 'cutscene-playback-check.txt'),
    @('.verification/correctness-20261008/UiAudit/UiAudit.csproj', 'Games', "$auditOutput/interaction-ui", 'report.txt'),
    @('.verification/room02-exit-click/Room02ExitClickCheck/Room02ExitClickCheck.csproj', 'Games/loom ega mt32 en', "$auditOutput/room02-ui", 'room02-exit-click-check.txt')
)) {
    Run-Rtk dotnet build $harness[0] --verbosity quiet
    Run-Rtk dotnet run --project $harness[0] --no-build -- $harness[1] $harness[2]
    $lines = Get-Content -Encoding UTF8 (Join-Path $harness[2] $harness[3])
    if ($lines -match '^\s*FAIL ') { throw "Harness assertions failed: $($harness[0])" }
}
Write-Output 'Audit commands completed. Review scenes/anomalies JSON: an exit code of zero is not a claim that every preview completed.'
