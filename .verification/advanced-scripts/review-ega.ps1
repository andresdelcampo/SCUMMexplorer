$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$inputFolder = Join-Path $PSScriptRoot 'before-ega/review-input'
$oldNames = (Get-Content (Join-Path $repo 'src/SCUMMexplorer.Core/Loom/Text/loom-glossary.json') -Raw | ConvertFrom-Json).entries
$cd = Get-Content (Join-Path $repo 'src/SCUMMexplorer.Core/Loom/Cd/loom-cd-glossary.json') -Raw | ConvertFrom-Json
$titles = @{}
foreach ($row in $cd) { $titles[[int]$row.id] = $row.name }
# Existing EGA titles retain their wording. This is a reverse review, not an ID-based CD import.
foreach ($row in $oldNames | Where-Object kind -eq 'script') {
    $to = if ($row.to) { [int]$row.to } else { [int]$row.key }
    foreach ($id in [int]$row.key..$to) { $titles[$id] = $row.name }
}
# These EGA streams/text differ materially from the CD. Titles below describe the EGA instructions.
$titles[43] = 'cannot save a game in demo mode'
$titles[51] = 'Room 11 actor procession and arrival in Room 13'
$titles[55] = 'glass bell scene and transition into Room 20'
$titles[64] = 'the little glass bell reply'
$titles[70] = 'sheep scene in Rooms 24 and 27'
$titles[71] = 'the dizzy reply'
$titles[88] = "Rusty's ghost and Hetchel's return (Room 34)"
$titles[90] = "Room 42 flashes and Mandible's introduction"
$titles[99] = "Goodmold's aftermath and Room 20 objects"
$titles[103] = 'the far side of the Pattern is out of reach'
$titles[106] = 'ending sequence from Room 66'
$titles[111] = 'Room 86 spell visual and return to Room 31'
$titles[112] = 'Rusty is still napping'
$titles[115] = 'save-screen input hook'
$titles[116] = 'animate actor 11 at random argument positions'

$review = @()
foreach ($edition in @(@{Prefix='ega';Name='English EGA'}, @{Prefix='ega-es';Name='Spanish EGA'})) {
    $scripts = Get-Content (Join-Path $inputFolder ($edition.Prefix+'.json')) -Raw | ConvertFrom-Json
    $rows = @()
    foreach ($s in $scripts | Where-Object { $_.ScriptId -gt 0 -and $_.Issues.Count -eq 0 -and !$_.DecodeNote }) {
        $id = [int]$s.ScriptId
        if (!$titles.ContainsKey($id)) { throw "Unreviewed EGA title $id" }
        $old = $oldNames | Where-Object { $_.kind -eq 'script' -and [int]$_.key -eq $id } | Select-Object -First 1
        $evidence = if ($id -in 90,99) { 'reading' } elseif ($old) { $old.evidence } elseif ($id -in 26,34,35,47,64,65,66,67,68,69,70,71,74,75,76,85,88,95,100,101,103,104,105,106,109,112) { 'reading' } else { 'decoded' }
        $anchors = @($s.Instructions | Where-Object { $_.Operation -in 'loadRoom','putActorInRoom','loadRoomWithEgo','print','chainScript','actorOps','and','or','startSound' } | Select-Object -First 5)
        $speaker = switch ($id) { 69 {'Fleece'} 85 {'Rusty'} 90 {'Mandible'} 99 {'Goodmold'} 100 {'Cygna'} default {$null} }
        if ($speaker) {
            $identity = @($s.Instructions | Where-Object { $_.Operation -eq 'print' -and $_.Operands.Contains($speaker) } | Select-Object -First 2)
            if ($identity.Count -eq 0) { throw "No EGA identity anchor for $speaker" }
            $anchors = @($identity) + @($anchors | Select-Object -First 3)
        }
        if ($anchors.Count -eq 0) { $anchors = @($s.Instructions | Select-Object -First 3) }
        $refs = ($anchors | ForEach-Object { '+0x{0:X4} {1}' -f $_.Offset,$_.Operation }) -join ', '
        $source = '{0} Script {1:00} in {2:00}.LFL: {3}; reviewed EGA instruction stream' -f $edition.Name,$id,$s.ContainerFileNumber,$refs
        if ($cd | Where-Object id -eq $id) { $source += '; compared with CD Script {0:00}, EGA differences retained' -f $id }
        $text = ($s.Instructions | ForEach-Object { $_.Operation + ' ' + $_.Operands }) -join "`n"
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
        $rows += [ordered]@{id=$id;name=$titles[$id];evidence=$evidence;source=$source;fingerprint=$hash;edition=$edition.Name;names=@()}
    }
    function Add-Name($script, $kind, $pattern, $name, $operation, $evidence = 'decoded', $scope = $null, $count = 1, $extra = '') {
        $s = $scripts | Where-Object ScriptId -eq $script
        $row = $rows | Where-Object id -eq $script
        if (!$row) { throw "No verified $script" }
        $found = @($s.Instructions | Where-Object Operation -eq $operation | ForEach-Object {
            $match = [regex]::Match($_.Operands, $pattern)
            if ($match.Success) { @{Instruction=$_;Id=[Convert]::ToInt32($match.Groups['id'].Value,16)} }
        })
        if ($found.Count -eq 0 -or @($found.Id | Select-Object -Unique).Count -ne 1) { throw "Unestablished/ambiguous $name in $($edition.Name) $script" }
        $first = $found[0]; $i = $first.Instruction
        $source = '{0} Script {1:00} +0x{2:X4} in {3:00}.LFL: {4}; {5}{6}' -f $edition.Name,$script,$i.Offset,$s.ContainerFileNumber,$operation,$name,$extra
        $entry = [ordered]@{kind=$kind;id=$first.Id;name=$name;evidence=$evidence;source=$source;scope=$scope}
        if ($count -gt 1) { $entry.to = $first.Id+$count-1 }
        $row.names += $entry
    }
    $v = 'var\[0x(?<id>[0-9A-F]+)\]'
    $b = 'bit\[0x(?<id>[0-9A-F]+)\]'
    Add-Name 6 var ('^local\[0x002\], '+$v+',') 'pointer x' findObject
    Add-Name 6 var ('^local\[0x002\], var\[0x[0-9A-F]+\], '+$v+'$') 'pointer y' findObject
    Add-Name 23 var ('^'+$v+' = var\[0x032\+local\[0x000\]\]$') 'current draft response word' move
    Add-Name 28 var ('^'+$v+', count=4,') 'draft buffer / displayed note letters' setVarRange decoded $null 4
    foreach ($part in @(@{Mask='2000';Name='the current draft is learned'},@{Mask='1000';Name='the current draft can be reversed'},@{Mask='4000';Name='the current draft has last-note knowledge'})) {
        $code = ($scripts | Where-Object ScriptId -eq 23).Instructions
        $and = $code | Where-Object { $_.Operation -eq 'and' -and $_.Operands -eq ('local[0x001] &= 0x'+$part.Mask) }
        $next = $code | Where-Object Offset -eq ($and.Offset+5)
        if ($next.Operation -ne 'move' -or $next.Operands -notmatch '^bit\[0x[0-9A-F]+\] = local\[0x001\]$') { throw "No mask-to-bit pair $($part.Name)" }
        $destination = [regex]::Match($next.Operands, '^bit\[0x(?<id>[0-9A-F]+)\]').Groups['id'].Value
        Add-Name 23 bit ('^bit\[0x(?<id>'+$destination+')\] = local\[0x001\]$') $part.Name move decoded $null 1 ('; mask 0x'+$part.Mask)
    }
    Add-Name 30 bit ('^'+$b+' = 0x0001$') 'play a note on an object' move
    $refused = ($scripts | Where-Object ScriptId -eq 35).Instructions | Where-Object { $_.Operation -eq 'move' -and $_.Operands -match '^bit\[0x[0-9A-F]+\] = 0x0001$' } | Select-Object -First 1
    $destination = [regex]::Match($refused.Operands, '^bit\[0x(?<id>[0-9A-F]+)\]').Groups['id'].Value
    Add-Name 35 bit ('^bit\[0x(?<id>'+$destination+')\] = 0x0001$') 'the draft response was refused or consumed' move reading $null 1 '; Script 26 clears and checks it after running the object response'
    Add-Name 37 var ('^'+$v+'$') 'queued or last-started sound' startSound
    Add-Name 37 var ('^'+$v+' = var\[0x0A3\]$') 'sound currently playing' move
    foreach ($part in @(@{Parameter='initAnimation';Name='ego initial animation'},@{Parameter='walkAnimation';Name='ego walk animation'},@{Parameter='standAnimation';Name='ego standing animation'},@{Parameter='talkAnimation';Name='ego talk-start animation'})) {
        Add-Name 17 var ($part.Parameter+', '+$v+'(?:;|,|\])') $part.Name actorOps
    }
    Add-Name 17 var ('talkAnimation, var\[0x[0-9A-F]+\], '+$v+'(?:;|\])') 'ego talk-stop animation' actorOps
    foreach ($part in @(@{Local='003';Axis='x'},@{Local='004';Axis='y'})) {
        $pattern = '^local\[0x'+$part.Local+'\] = var\[0x(?<id>[0-9A-F]+)\+local\[0x002\]\]$'
        Add-Name 42 var $pattern ('flying actor '+$part.Axis+' positions') move decoded $null 5 '; Script 44 initializes five entries'
        $added = ($rows | Where-Object id -eq 42).names | Select-Object -Last 1
        if (!(($scripts | Where-Object ScriptId -eq 44).Instructions | Where-Object { $_.Operation -eq 'setVarRange' -and $_.Operands.StartsWith(('var[0x{0:X3}], count=5,' -f $added.id)) })) { throw 'Flying array extent not established' }
    }
    # Actor slots are reused, so add only roles supported inside the reviewed scene.
    foreach ($part in @(
        @{Script=46;Actor=2;Name='Elder (actor 2)'},@{Script=46;Actor=3;Name='Elder (actor 3)'},@{Script=46;Actor=4;Name='Elder (actor 4)'},@{Script=46;Actor=5;Name='Hetchel'},
        @{Script=47;Actor=5;Name='Hetchel'},@{Script=66;Actor=3;Name='Master Goodmold'},@{Script=67;Actor=6;Name='Master Goodmold'},
        @{Script=69;Actor=2;Name='Fleece'},@{Script=74;Actor=2;Name='dragon'},@{Script=75;Actor=2;Name='dragon'},@{Script=85;Actor=3;Name='Rusty'},
        @{Script=86;Actor=5;Name='Mr. Stoke'},@{Script=87;Actor=5;Name='Mr. Stoke'},@{Script=88;Actor=3;Name='Rusty (ghost)'},@{Script=88;Actor=11;Name='Hetchel'},
        @{Script=90;Actor=2;Name='Mandible'},@{Script=90;Actor=3;Name='Cob'},@{Script=99;Actor=3;Name='Master Goodmold'},
        @{Script=91;Actor=2;Name='Mandible'},@{Script=91;Actor=3;Name='Cob'},@{Script=93;Actor=3;Name='Cob'},@{Script=95;Actor=2;Name='Mandible'},
        @{Script=98;Actor=3;Name='Rusty (ghost)'},@{Script=100;Actor=3;Name="Cygna (Bobbin's mother)"},
        @{Script=101;Actor=5;Name='Hetchel'},@{Script=101;Actor=4;Name='the Dead One (El Muerto)'},@{Script=103;Actor=3;Name="Cygna (Bobbin's mother)"},
        @{Script=104;Actor=3;Name="Cygna (Bobbin's mother)"},@{Script=104;Actor=4;Name='the Dead One (El Muerto)'},
        @{Script=105;Actor=4;Name='the Dead One (El Muerto)'},@{Script=106;Actor=3;Name="Cygna (Bobbin's mother)"},@{Script=106;Actor=4;Name='the Dead One (El Muerto)'},
        @{Script=108;Actor=6;Name='Glassmaker'},@{Script=109;Actor=2;Name='dragon'}
    )) {
        $extra = switch ($part.Script) {
            66 {'; Script 68 receives actor 3 and identifies Goodmold'}
            67 {'; Script 68 receives actor 6 and identifies Goodmold'}
            88 {'; Rusty dialogue / Hetchel bird costume 0x34 as in Script 47'}
            {$_ -in 103,104,106} {'; speaker continuation of Script 100, which identifies Cygna'}
            default {''}
        }
        Add-Name $part.Script actor ('^0x(?<id>'+('{0:X2}' -f $part.Actor)+'), \[') $part.Name print reading ('script:'+$part.Script) 1 $extra
    }
    $review += $rows
}
$review | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $repo 'src/SCUMMexplorer.Core/Loom/Ega/loom-ega-reviewed-glossary.json') -Encoding utf8
$lines = @('# EGA reverse review, 8 October 2026','',
    'English and Spanish EGA globals were compared with the completed VGA review. Titles and roles below are accepted only for their reviewed EGA instruction fingerprints. Numeric scene descriptions remain where the story role is unestablished. Static code is not proof of execution. Existing EGA titles retain their wording, except 90/99 now describe their dialogue as well; all valid globals have their own EGA source references.','',
    'Script 00 remains unresolved: the English decode has invalid control-flow targets; the Spanish resource length is invalid. Spanish 51, 55 and 116 also point outside their containers and get no imported label. English 51, 55, 115 and 116 are reviewed from their own code. EGA 4/39/40/115 keep their EGA save/music behavior. Contrary to the earlier VGA comparison notes, both EGA and CD 90/99 contain the Bishop introduction and Goodmold aftermath; the old EGA titles described only part of them.','',
    '| EGA script | English / Spanish instructions | EGA title | Evidence | English source |',
    '| --- | --- | --- | --- | --- |')
$en = Get-Content (Join-Path $inputFolder 'ega.json') -Raw | ConvertFrom-Json
$es = Get-Content (Join-Path $inputFolder 'ega-es.json') -Raw | ConvertFrom-Json
foreach ($row in $review | Select-Object -First 116) {
    $s = $en | Where-Object ScriptId -eq $row.id; $other = $es | Where-Object ScriptId -eq $row.id
    $count = if ($other) { $other.Instructions.Count } else { 'invalid resource' }
    $lines += '| {0:000} | {1} / {2} | {3} | {4} | {5} |' -f $row.id,$s.Instructions.Count,$count,$row.name,$row.evidence,$row.source
}
$lines | Set-Content (Join-Path $PSScriptRoot 'REVIEW-EGA.md') -Encoding utf8
