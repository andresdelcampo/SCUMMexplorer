param([string]$GameFolder, [string]$OutputFolder)
$previewExe = Join-Path $PSScriptRoot '../../Exe/Preview/Debug/net10.0-windows/Preview.exe'
$previewArgs = '"' + (Resolve-Path -LiteralPath $GameFolder).Path + '" "' + $OutputFolder + '"'
$previewProcess = Start-Process -FilePath $previewExe -ArgumentList $previewArgs -WindowStyle Hidden -PassThru -Wait
exit $previewProcess.ExitCode
