# Preserve only this session's generated files under the already ignored Exe tree.
# No construction-review candidates, game data, worktrees or unrelated files are touched.
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$storeRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'Exe/VerificationSession-20260930'))
$relativePaths = @(
  '.verification/score-preservation-20260930/audit-build',
  '.verification/score-preservation-20260930/build-after',
  '.verification/score-preservation-20260930/build',
  '.verification/score-preservation-20260930/verb-build',
  '.verification/mode-roundtrip-20260930/build-after',
  '.verification/mode-roundtrip-20260930/build-before',
  '.verification/mode-roundtrip-20260930/build-final',
  '.verification/mode-roundtrip-20260930/doorway-build-final',
  '.verification/mode-roundtrip-20260930/doorway-build',
  '.verification/audio-modes-20260930/build-final',
  '.verification/audio-modes-20260930/build',
  '.verification/cutscene-busy-20260930/build-final',
  '.verification/cutscene-busy-20260930/build',
  '.verification/arrival-music-20260930/audit-build',
  '.verification/arrival-music-20260930/build',
  '.verification/arrival-music-20260930/doorway-build',
  '.verification/arrival-music-20260930/busy-build',
  '.verification/room36-exit-20260930/detail-build',
  '.verification/room36-exit-20260930/probe-build',
  '.verification/room02-mountain-mask-20260930/build',
  '.verification/audio-mode-analysis-20260930-player_ad.cpp',
  '.verification/audio-mode-analysis-20260930-player_ad.h',
  '.verification/cutscene-discovery-20260930/candidates.json',
  '.verification/audio-modes-20260930/commit-message.md',
  '.verification/cutscene-busy-20260930/commit-message.md',
  '.verification/cutscene-discovery-20260930/commit-message.md',
  '.verification/arrival-music-20260930/commit-message.md',
  '.verification/room36-exit-20260930/commit-message.md',
  '.verification/room02-mountain-mask-20260930/commit-message.md'
)
foreach ($relative in $relativePaths) {
    $source = [IO.Path]::GetFullPath((Join-Path $repoRoot $relative))
    $destination = [IO.Path]::GetFullPath((Join-Path $storeRoot $relative))
    if (!$source.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        !$destination.StartsWith($storeRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        !$storeRoot.StartsWith($repoRoot + '\Exe\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside intended workspace' }
    if (!(Test-Path -LiteralPath $source)) { continue }
    $tracked = & 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy git -C $repoRoot ls-files -- $relative
    if ($tracked) { throw "Refusing to move tracked files: $relative" }
    if ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing reparse point: $source" }
    if (Test-Path -LiteralPath $destination) { throw "Destination already exists: $destination" }
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Move-Item -LiteralPath $source -Destination $destination
    "Preserved $relative under Exe/VerificationSession-20260930"
}
