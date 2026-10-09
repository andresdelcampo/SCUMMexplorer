param([ValidateSet('lessons','seven')][string]$Kind = 'lessons')
$ErrorActionPreference = 'Stop'
function Get-TaskHash([string]$Path) {
    $taskHasher = [System.Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($taskHasher.ComputeHash([System.IO.File]::ReadAllBytes($Path))).Replace('-', '') }
    finally { $taskHasher.Dispose() }
}
$taskPackets = if ($Kind -eq 'lessons') {
    @('out-slot3-owlet','out-slot3-es','out-slot3-vga','out-slot7-approach','out-slot7-vga-geometry','out-slot11-pasture','out-slot11-vga','out-slot7-departure-control')
} else { @('out-seven-dragon-gold') }
$taskResults = foreach ($taskName in $taskPackets) {
    $taskPath = Join-Path $PSScriptRoot "$taskName/packet.json"
    $taskPacket = Get-Content -LiteralPath $taskPath -Raw | ConvertFrom-Json
    if ($null -ne $taskPacket.failure -or @($taskPacket.checks | Where-Object { $_ -like 'FAIL *' }).Count -gt 0) { throw "Failed packet: $taskName" }
    $taskTrace = @($taskPacket.trace)
    $taskBegin = @($taskTrace | Where-Object { $_ -like 'cutscene at *' }).Count
    $taskEnd = @($taskTrace | Where-Object { $_ -like 'endCutscene at *' }).Count
    if ($taskBegin -ne $taskEnd -or !$taskPacket.after.CanEdit) { throw "Unsettled packet: $taskName" }
    $taskSlot = switch ($taskPacket.route) { slot3 {3}; slot7 {7}; slot11 {11}; default {$null} }
    $taskHelpers = @($taskPacket.observations | Where-Object { $null -ne $_.Site -and $_.Site.Stage -eq 'instruction' -and $_.Site.Offset -eq 0 -and $_.Site.Script -in 25,29 } | ForEach-Object {
        [ordered]@{script=$_.Site.Script;argument=$_.Values.'local[0x000]';context=$_.Site.Context;container=$_.Site.Container;sequence=$_.Sequence;pathEstimated=$_.PathEstimated}
    })
    if ($null -ne $taskSlot) {
        $taskBefore = $taskPacket.before.Drafts[$taskSlot]; $taskAfter = $taskPacket.after.Drafts[$taskSlot]
        if ($taskBefore.Learned -eq $true -or $taskAfter.Learned -ne $true -or
            (($taskBefore.Packed -band (-bnot 0x2000)) -ne ($taskAfter.Packed -band (-bnot 0x2000))) -or
            @($taskHelpers | Where-Object { $_.script -eq 25 -and $_.argument -eq $taskSlot }).Count -eq 0) { throw "Unqualified lesson: $taskName" }
    }
    if ($taskPacket.route -eq 'seven') {
        if ($taskPacket.after.AvailableNoteCount -ne 7 -or @($taskHelpers | Where-Object { $_.script -eq 29 -and $_.argument -eq 7 }).Count -eq 0) { throw 'Seven-note helper missing' }
        $taskClosing = @($taskTrace | Where-Object { $_ -match 'startObject runs object \d+ verb 0x3B$' })
        if ($taskClosing.Count -ne 4 -or @($taskTrace | Where-Object { $_ -like '*startObject runs object 587 verb 0x4D*' }).Count -ne 1) { throw 'Earned Closing/Healing input missing' }
    }
    foreach ($taskSource in $taskPacket.sourceHashes.PSObject.Properties) {
        $taskSourcePath = Join-Path $PSScriptRoot "$taskName/$($taskSource.Name).txt"
        if ((Get-TaskHash $taskSourcePath) -ne $taskSource.Value) { throw "Changed source copy: $taskSourcePath" }
    }
    [pscustomobject][ordered]@{
        edition=$taskPacket.edition;route=$taskPacket.route;packet="$taskName/packet.json";
        sha256=(Get-TaskHash $taskPath);
        assertions=@($taskPacket.checks).Count;frames=$taskPacket.frames;scopes=$taskBegin;ended=$taskEnd;
        slot=$taskSlot;beforePacked=if($null -ne $taskSlot){$taskPacket.before.Drafts[$taskSlot].Packed}else{$null};
        afterPacked=if($null -ne $taskSlot){$taskPacket.after.Drafts[$taskSlot].Packed}else{$null};
        notesBefore=$taskPacket.before.AvailableNoteCount;notesAfter=$taskPacket.after.AvailableNoteCount;
        helpers=$taskHelpers;
        stages=@($taskPacket.records | Where-Object { $null -ne $_.status } | ForEach-Object { [ordered]@{stage=$_.stage;room=$_.snapshot.RoomId;notes=$_.status.AvailableNoteCount;frames=$_.frames} })
    }
}
$taskSummary = [ordered]@{kind=$Kind;cases=@($taskResults).Count;assertions=($taskResults | Measure-Object assertions -Sum).Sum;results=@($taskResults)}
$taskSummary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot "$Kind-routes-summary.json") -Encoding UTF8
Write-Output "$Kind`: $($taskSummary.cases) cases, $($taskSummary.assertions) recorded assertions; hashes, helper calls and settlement verified"
