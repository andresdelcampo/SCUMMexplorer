param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Output) { throw 'Use a fresh output folder.' }
if (Get-Process -Name SCUMMexplorer.App -ErrorAction SilentlyContinue) { throw 'Close the App before running this Core-only probe.' }
$routes = Get-Content .verification/archive/room41-certainty-20261002/room41.json -Raw | ConvertFrom-Json
$selected = @($routes | Where-Object id -eq 'room41-raw4-click')
if ($selected.Count -ne 1) { throw 'Exactly one unchanged raw4 recipe required.' }
New-Item -ItemType Directory -Path $Output | Out-Null
Copy-Item -LiteralPath Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json -Destination "$Output/protected-before.json"
[IO.File]::WriteAllText((Join-Path $PWD "$Output/raw4-only.json"), (ConvertTo-Json -InputObject $selected -Depth 50))
& rtk proxy dotnet run --project .verification/archive/room41-certainty-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom "$Output/raw4-only.json" "$Output/raw4-1" *> "$Output/raw4-run.txt"
if ($LASTEXITCODE) { throw 'Raw4 probe failed. Preserve its log.' }
Get-Content "$Output/raw4-run.txt"
& rtk proxy 'C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -X utf8 .verification/archive/room41-route-20261002/analyze.py $Output
if ($LASTEXITCODE) { throw 'Raw4 analysis/protection checks failed.' }
