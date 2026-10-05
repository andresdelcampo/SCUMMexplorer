# Re-runs the three harnesses of the English run that did not finish or failed without an argument problem, with a long limit and their output kept.
$root = 'D:\Documents\Personal\Code Projects\SCUMMexplorer'
$out = Join-Path $root '.verification\harness-review-20261004\out\rerun'
New-Item -ItemType Directory -Force $out | Out-Null
$env:SCUMM_SOUND_TEST_ROOT = "$root\Games\loom ega mt32 en"
foreach ($name in 'DoorwayClickCheck', 'TextGalleryCheck', 'Blockers') {
  $exe = Get-ChildItem (Join-Path $root "Exe\$name\Debug") -Recurse -Filter "$name.exe" | Select-Object -First 1
  $dir = Join-Path $out $name
  New-Item -ItemType Directory -Force $dir | Out-Null
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = Start-Process $exe.FullName -ArgumentList "`"$root\Games\loom ega mt32 en`"", "`"$dir`"" -PassThru -RedirectStandardOutput "$dir\stdout.txt" -RedirectStandardError "$dir\stderr.txt" -WorkingDirectory $root
  $done = $p.WaitForExit(900000)
  if (-not $done) { & taskkill /PID $p.Id /T /F | Out-Null }
  "$name`t$(if ($done) { $p.ExitCode } else { 'TIMEOUT-15min' })`t$([int]$sw.Elapsed.TotalSeconds)s" | Out-File "$out\summary.tsv" -Append -Encoding utf8
}
