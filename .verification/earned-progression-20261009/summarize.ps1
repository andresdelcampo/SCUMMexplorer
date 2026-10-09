$ErrorActionPreference = 'Stop'
$taskLessons = Get-Content (Join-Path $PSScriptRoot 'out-lessons-cd/lessons.json') -Raw | ConvertFrom-Json
$taskLate = Get-Content (Join-Path $PSScriptRoot 'out-late/chains.json') -Raw | ConvertFrom-Json
$taskLevels = Get-Content (Join-Path $PSScriptRoot 'out-levels-reviewed/levels.json') -Raw | ConvertFrom-Json
$taskEarned = ($taskLessons + $taskLate) | Where-Object { $_.earned } | Sort-Object edition, slot
if (@($taskEarned).Count -ne 33) { throw 'Expected 33 unique source-earned lessons.' }
if (@($taskEarned | Group-Object edition, slot | Where-Object Count -ne 1).Count) { throw 'Duplicate lesson credit.' }
function PacketHash([string]$taskPath) {
    $taskHasher = [System.Security.Cryptography.SHA256]::Create()
    $taskStream = [System.IO.File]::OpenRead($taskPath)
    try { return [BitConverter]::ToString($taskHasher.ComputeHash($taskStream)).Replace('-', '').ToLowerInvariant() }
    finally { $taskStream.Dispose(); $taskHasher.Dispose() }
}
foreach ($taskPacket in $taskEarned) {
    if ($taskPacket.before.Learned -eq $true -or $taskPacket.after.Learned -ne $true -or
        $taskPacket.InCutscene -or -not $taskPacket.CanEdit -or
        (($taskPacket.before.Packed -band 0xDFFF) -ne ($taskPacket.after.Packed -band 0xDFFF))) {
        throw "Unqualified lesson $($taskPacket.edition)/$($taskPacket.slot)"
    }
    $taskCalls = @($taskPacket.observations | Where-Object {
        $_.Site.Script -eq 25 -and $_.Site.Stage -eq 'instruction' -and $_.Site.Offset -eq 0 -and
        $_.Values.'local[0x000]' -eq $taskPacket.slot
    })
    if ($taskCalls.Count -eq 0) { throw 'Missing observed lesson helper.' }
}
$taskQualifiedLevels = @($taskLevels | Where-Object { $_.earned })
if ($taskQualifiedLevels.Count -ne 9) { throw 'Expected nine source-executed level controls.' }
$taskSummary = [ordered]@{
    date = '2026-10-09'
    scope = 'Source callbacks and catalog starts; no Game Status edits or direct runtime writes. Not four-note input or a full original-game route.'
    lessons = @($taskEarned | ForEach-Object {
        [ordered]@{ edition = $_.edition; slot = $_.slot; before = $_.before.Packed; after = $_.after.Packed;
            root = $_.root; room = $_.CurrentRoomId; helper25Observed = $true;
            estimatedPath = @($_.observations | Where-Object { $_.PathEstimated }).Count -gt 0 }
    })
    sourceExecutedLevels = @($taskQualifiedLevels | ForEach-Object {
        [ordered]@{ edition = $_.edition; level = $_.level; notes = $_.notes; sequence = $_.sequence; room = $_.CurrentRoomId }
    })
    remainingLessons = @(3, 7, 11)
    remainingLevel = 7
    packets = @('out-lessons/lessons.json', 'out-lessons-final/lessons.json', 'out-lessons-cd/lessons.json',
        'out-chains/chains.json', 'out-late/chains.json', 'out-levels/levels.json', 'out-levels-reviewed/levels.json') | ForEach-Object {
        [ordered]@{ path = $_; sha256 = PacketHash (Join-Path $PSScriptRoot $_) }
    }
}
$taskSummary | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $PSScriptRoot 'summary.json') -Encoding utf8
Write-Output '33 lessons and nine source-executed level controls qualified; remaining guards retained.'
