param([string]$OutRoot, [switch]$NoBuild)
# Builds and runs the WPF harnesses into fresh folders under $OutRoot (relative to the repo root).
Set-Location "D:\Documents\Personal\Code Projects\SCUMMexplorer"
Get-Process SCUMMexplorer.App -ErrorAction SilentlyContinue | Stop-Process -Force
$checks = @(
    @{ Name = 'ClickInputCheck';    Proj = '.verification/click-input/ClickInputCheck/ClickInputCheck.csproj'; Args = @() },
    @{ Name = 'DoorwayClickCheck';  Proj = '.verification/doorway-clicks/DoorwayClickCheck/DoorwayClickCheck.csproj'; Args = @() },
    @{ Name = 'Room03ActionCheck';  Proj = '.verification/Room03ActionCheck/Room03ActionCheck.csproj'; Args = @('162','4','167','38') },
    @{ Name = 'PointerInputCheck';  Proj = '.verification/pointer-input/PointerInputCheck/PointerInputCheck.csproj'; Args = @() },
    @{ Name = 'ReverseHoverCheck';  Proj = '.verification/hotspot-highlight/ReverseHoverCheck/ReverseHoverCheck.csproj'; Args = @('3','10','17') },
    @{ Name = 'HighlightCheck';     Proj = '.verification/hotspot-highlight/HighlightCheck/HighlightCheck.csproj'; Args = @('3','10','17') },
    @{ Name = 'MusicRowCheck';      Proj = '.verification/room-music/MusicRowCheck/MusicRowCheck.csproj'; Args = @('2:none','3:none','16','+:30','17:none','16:30*') },
    @{ Name = 'VerbAreaCheck';      Proj = '.verification/verb-area/VerbAreaCheck/VerbAreaCheck.csproj'; Args = @() },
    @{ Name = 'KeyInputCheck';      Proj = '.verification/key-input/KeyInputCheck/KeyInputCheck.csproj'; Args = @() },
    @{ Name = 'PresentationCheck';  Proj = '.verification/room-viewport/PresentationCheck/PresentationCheck.csproj'; Args = @() }
)
foreach ($c in $checks) {
    if (-not $NoBuild) {
        $b = dotnet build $c.Proj 2>&1 | Select-String "error|rror\(s\)" | Select-Object -First 3
        if (($b | Out-String) -notmatch " 0 Error") { "$($c.Name): BUILD FAILED $b"; continue }
    }
    $out = Join-Path $OutRoot $c.Name
    $exe = Get-ChildItem "Exe/$($c.Name)/Debug/*/$($c.Name).exe" | Select-Object -First 1
    $rel = Resolve-Path -Relative $exe.FullName
    $argList = @('Games\loom', $out) + $c.Args
    $p = Start-Process -FilePath $rel -ArgumentList $argList -Wait -NoNewWindow -WorkingDirectory $PWD -PassThru
    $txt = Get-ChildItem $out -Filter *.txt -ErrorAction SilentlyContinue
    $pass = 0; $fail = 0
    foreach ($t in $txt) { $lines = Get-Content $t.FullName; $pass += ($lines | Where-Object { $_ -cmatch '^\s*PASS' }).Count; $fail += ($lines | Where-Object { $_ -cmatch '^\s*FAIL' }).Count }
    "$($c.Name): exit $($p.ExitCode), PASS $pass, FAIL $fail"
}
