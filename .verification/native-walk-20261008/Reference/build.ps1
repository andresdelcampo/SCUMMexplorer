param([string]$Source = '.verification/final-gaps-20261008/scummvm-v2.2.0',
      [string]$Compiler = 'C:\msys64\mingw64\bin\g++.exe')
$ErrorActionPreference = 'Stop'
$rtk = 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$actor = Get-Content -Raw -LiteralPath (Join-Path $Source 'actor.cpp')
$boxes = Get-Content -Raw -LiteralPath (Join-Path $Source 'boxes.cpp')
function Method([string]$Text, [string]$Signature) {
    $start = $Text.IndexOf($Signature)
    if ($start -lt 0) { throw "Missing native signature: $Signature" }
    $brace = $Text.IndexOf('{', $start)
    $depth = 1; $end = $brace + 1
    while ($depth -gt 0) {
        if ($Text[$end] -eq '{') { $depth++ }
        if ($Text[$end] -eq '}') { $depth-- }
        $end++
    }
    $Text.Substring($start, $end-$start)
}
$methods = @(
    (Method $actor 'int Actor::calcMovementFactor('),
    (Method $actor 'int Actor::actorWalkStep('),
    (Method $actor 'void Actor::walkActor('),
    (Method $boxes 'bool Actor::findPathTowards(')
)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'native-methods.inc'), ($methods -join "`n`n"))
$env:PATH = (Split-Path $Compiler) + ';' + $env:PATH
& $rtk proxy $Compiler -std=c++17 -O0 -static (Join-Path $PSScriptRoot 'host.cpp') -o (Join-Path $PSScriptRoot 'reference.exe')
if ($LASTEXITCODE -ne 0) { throw "Native reference compilation failed ($LASTEXITCODE)" }
foreach ($path in @((Join-Path $Source 'actor.cpp'),(Join-Path $Source 'boxes.cpp'),(Join-Path $PSScriptRoot 'native-methods.inc'))) {
    $hash = [Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes((Resolve-Path $path)))
    "{0} {1}" -f ([BitConverter]::ToString($hash).Replace('-','')), $path
}
