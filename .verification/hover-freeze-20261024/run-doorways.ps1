# Serial doorway check (static preview, no Play Game Intro): for each game/room the Doorways option and the Advanced DOORWAYS rows. Build HoverStorm first.
param([string]$Label = 'after')
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$cases = @(@('mmansionE ega es',44,26,16), @('mmansion ega en',44,26,16), @('zakE ega en',1,2,3), @('zak ega en',1,2,3))
$lines = @()
foreach ($c in $cases) {
  foreach ($room in $c[1..3]) {
    $r = & (Join-Path $root 'run-storm.ps1') "doors-$Label" $c[0] $room 0
    $lines += "$($c[0]) room $room :: " + (($r | Where-Object { $_ -match 'options visible|DOORWAYS rows|did not load|Exception' }) -join ' | ')
  }
}
$lines | Set-Content -Encoding utf8 (Join-Path $root "doorways-$Label.txt")
$lines
