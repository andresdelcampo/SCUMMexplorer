param([string]$OutputFolder = 'Exe/SourceProgressionSession-20261001/checks-fresh')
$ErrorActionPreference = 'Stop'
$rtkPath = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$env:SCUMM_SOUND_TEST_ROOT = Join-Path $PWD 'Games/loom'
$env:SCUMM_INDY3_EGA_TEST_ROOT = Join-Path $PWD 'Games/IndyCru/EGA'
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
& $rtkPath proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -c Release --filter 'FullyQualifiedName!~LoomObjectLookAuditTests' 2>&1 |
    Tee-Object -FilePath (Join-Path $OutputFolder 'core-tests.txt')
if ($LASTEXITCODE -ne 0) { throw "Core tests failed: $LASTEXITCODE" }
& $rtkPath proxy dotnet build SCUMMexplorer.slnx -c Release 2>&1 |
    Tee-Object -FilePath (Join-Path $OutputFolder 'release-build.txt')
if ($LASTEXITCODE -ne 0) { throw "Release build failed: $LASTEXITCODE" }
