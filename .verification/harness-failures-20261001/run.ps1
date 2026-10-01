param([string]$Output='Exe/VerificationSession-20261001/harness-failures-run1')
$ErrorActionPreference='Stop'
$rtk='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
& $rtk proxy dotnet build .verification/harness-failures-20261001/FailureProbe/FailureProbe.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
& $rtk proxy dotnet build .verification/harness-failures-20261001/ConsoleFailureProbe/ConsoleFailureProbe.csproj -c Release
if($LASTEXITCODE){exit $LASTEXITCODE}
New-Item -ItemType Directory -Path $Output | Out-Null
$results=@()
foreach($mode in @('main','worker','dispatcher','console')){
    $env:SCUMM_HARNESS_FAILURE_LOG=Join-Path (Get-Location) ($Output+'/'+$mode+'.log')
    $exe=if($mode -eq 'console'){'Exe/ConsoleFailureProbe/Release/net10.0/ConsoleFailureProbe.exe'}else{'Exe/FailureProbe/Release/net10.0-windows/FailureProbe.exe'}
    $process=Start-Process $exe -ArgumentList $mode -WindowStyle Hidden -PassThru
    $handle=$process.Handle
    if(!$process.WaitForExit(10000)){Stop-Process -Id $process.Id;throw "$mode did not exit without input"}
    if($process.ExitCode -ne 1 -or !(Test-Path $env:SCUMM_HARNESS_FAILURE_LOG)){throw "$mode failed to return 1 and write diagnostic"}
    $results+="PASS ${mode}: exits 1 within ten seconds, diagnostic saved, no user input"
}
Remove-Item Env:SCUMM_HARNESS_FAILURE_LOG
$results | Set-Content ($Output+'/validation.txt')
$results
