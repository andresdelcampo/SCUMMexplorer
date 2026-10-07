$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cd = Get-Content (Join-Path $PSScriptRoot 'before/vga.json') -Raw | ConvertFrom-Json
$ega = Get-Content (Join-Path $PSScriptRoot 'before/ega.json') -Raw | ConvertFrom-Json
$oldNames = (Get-Content (Join-Path $repo 'src/SCUMMexplorer.Core/Formats/loom-glossary.json') -Raw | ConvertFrom-Json).entries
# Each title is reviewed against this script's own operations/text. Neutral room/actor labels deliberately
# retain numeric identities when the instructions do not establish a character or a draft's meaning.
$titles = @'
1|boot
2|cutscene start hook
3|cutscene end hook
4|yield once and return (CD stub)
5|input script
6|click handler (input loop)
7|run a sentence (verb on an object)
8|show or hide the close-up in the verb area
9|hint button (practice mode)
10|a distaff note was played
11|"That thread's too high for me."
12|"Maybe I should stand a bit closer."
13|"It might help to point at something first"
14|after-entry hook
15|before-exit hook
16|set the text line
17|give the ego a costume (by size)
18|show the distaff
19|hide the distaff verbs
20|play the draft back
21|end the draft playback
22|compare the played draft with the known ones
23|load draft N
24|check whether draft N is learned
25|mark draft N as learned
26|run the matched draft on the aimed object
27|mark a note in the verb area
28|clear the draft buffer
29|learning glow
30|play one note of a draft on an object
31|animate and sound one note
32|recolour an actor's palette
33|restore an actor's palette
34|mark draft N's last-note knowledge
35|reject a draft response
36|restore the note letters
37|play the queued sound and wait
38|stop queued and current sounds
39|return immediately (CD stub)
40|play a CD effect cue
41|spell visual at a point
42|Room 03's flying actor
43|"You cannot save a demonstration game."
44|opening cutscene
45|Room 02 walk-box watcher
46|the Elders' trial
47|Hetchel speaks after the trial
48|Room 63 to Room 62 transition
49|whirlwind scene (Rooms 02 and 11)
50|Room 03 to Room 10 transition
52|Room 10 to Room 13 cutscene
53|Room 13 scene with actors 8 and 9
54|Room 15 arrival and learning glow
56|transition into Room 20
57|room-local transition through scripts 200 and 207
58|room-local transition through scripts 200 and 59
59|transition into Room 18
60|room-local transition through scripts 201 and 61
61|Room 18 arrival through local 203
62|transition into Room 20 through script 63
63|room-local transition through scripts 201 and 207
64|"Huh! A glass bell!"
65|Room 19 spell scene
66|Goodmold conversation in Room 17
67|Goodmold conversation in Room 22
68|Master Goodmold's greeting
69|Fleece's greeting and the sheep problem
70|Room 24 sheep scene
71|"I'm dizzy!"
72|Room 65 to Room 30 transition
73|walk actor 8 across Room 24
74|dragon's arrival greeting (Room 28)
75|conversation with the hungry dragon
76|dragon leaves Room 27
77|Room 28 arrival and object classes
78|Room 65 scene and arrival in Room 30
79|Room 32 arrival and walk-box changes
80|Room 32 arrival
81|Room 30 arrival and lighting
82|arrival in Room 31 and blocked return
83|Room 30 arrival
84|Room 31 arrival
85|Rusty Nailbender's greeting
86|firewood scene
87|ghost scene (with CD close-up in Room 96)
88|Room 34 scene with actors 3, 5 and 6
89|Room 11 scene with actor 4
90|Room 42 flashes and Mandible's introduction
91|Room 43 cage scene
92|Room 43 arrival
93|Room 43 cage scene, second part
94|Room 19 scene with actors 2 and 3
95|Mandible opens the Pattern
96|Room 45 fall
97|Room 34 arrival
98|Room 34 ghost walk
99|Goodmold's aftermath and Room 20 objects
100|Bobbin speaks with his mother
101|Hetchel confronts the Dead One
102|departure to Room 66
103|"That side of the Pattern is now beyond the reach of our threads."
104|Room 51 scene with actors 1, 3 and 4
105|Hetchel's feather and the Dead One
106|ending in Room 66
107|Room 10 arrival and object classes
108|glassmakers scene
109|dragon's gold conversation
110|Room 49 arrival and object classes
111|Bishop Mandible scene in Room 86
112|"He's fast asleep."
113|ambient actors during the intro
114|ambient actors during the intro
115|return immediately (CD stub)
117|debug display of the music flag
118|animate actor 3 in Room 45
119|choose a random failed-draft reply
120|set up a game from a passcode
121|assign draft patterns for a passcode
122|the Bishop's sphere order
123|walk actor 11 across Room 16
124|keep drawing random numbers
125|split the passcode into decimal digits
'@
$review = @()
foreach ($line in $titles.Trim().Split("`n")) {
    $parts = $line.Trim().Split('|', 2); $id = [int]$parts[0]; $s = $cd | Where-Object ScriptId -eq $id
    if ($null -eq $s) { throw "Missing CD script $id" }
    $text = ($s.Instructions | ForEach-Object { $_.Operation + ' ' + $_.Operands }) -join "`n"
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
    $old = $oldNames | Where-Object { $_.kind -eq 'script' -and [int]$_.key -le $id -and [int]$_.key -eq $id } | Select-Object -First 1
    if ($id -eq 114) { $old = $oldNames | Where-Object { $_.kind -eq 'script' -and $_.key -eq '113' } }
    $evidence = if ($id -ge 47 -and $id -notin 50,52,53,54,56,57,58,59,60,61,62,63,70,72,73,77,78,79,80,81,83,84,88,89,92,94,96,97,102,104,107,110,113,114,115,117,118,120,121,123,124,125) { 'reading' } elseif ($id -in 9,20,22,26,27,29,34,35,41) { 'reading' } else { 'decoded' }
    $anchors = @($s.Instructions | Where-Object { $_.Operation -in 'loadRoom','loadRoomWithEgo','print','startObject','actorOps','chainScript' } | Select-Object -First 4)
    if ($anchors.Count -eq 0) { $anchors = @($s.Instructions | Select-Object -First 3) }
    $refs = ($anchors | ForEach-Object { '+0x{0:X4} {1}' -f $_.Offset,$_.Operation }) -join ', '
    $source = 'CD Script {0:00} in DISK01.LEC: {1}; reviewed decoded instruction stream' -f $id,$refs
    if ($old) { $source += '; compared with EGA Script {0:00} ({1}), CD differences retained' -f $id,$old.name }
    $review += [ordered]@{ id=$id; name=$parts[1]; evidence=$evidence; source=$source; fingerprint=$hash; names=@() }
}
if ($review.Count -ne $cd.Count) { throw "Review contains $($review.Count) of $($cd.Count) globals" }

function Add-Name($script, $kind, $id, $name, $evidence, $operation, $needle, $scope = $null) {
    $row = $review | Where-Object id -eq $script
    $instruction = ($cd | Where-Object ScriptId -eq $script).Instructions | Where-Object { $_.Operation -eq $operation -and $_.Operands.Contains($needle) } | Select-Object -First 1
    if ($null -eq $instruction) { throw "Unestablished $name in script $script" }
    $source = 'CD Script {0:00} +0x{1:X4} in DISK01.LEC: {2}; reviewed role' -f $script,$instruction.Offset,$operation
    $row.names += [ordered]@{ kind=$kind; id=$id; name=$name; evidence=$evidence; source=$source; scope=$scope }
}
Add-Name 1 actor 1 'Bobbin' decoded move 'var[0x001] = 0x0001'
Add-Name 6 var 0x0A3 'exit the player arrived through' decoded move 'var[0x0A3] = local[0x002]'
Add-Name 6 bit 0x003 "an object's verb 0x37 is running" decoded move 'bit[0x003] = 0x0001'
Add-Name 6 bit 0x004 'the walk and the sentence were stopped' decoded move 'bit[0x004] = 0x0000'
Add-Name 31 bit 0x007 'a note is playing' decoded move 'bit[0x007] = 0x0001'
Add-Name 2 bit 0x00E 'a cutscene is running' decoded move 'bit[0x00E] = 0x0001'
Add-Name 26 bit 0x008 'the draft response was refused or consumed' reading move 'bit[0x008] = 0x0000'
Add-Name 30 bit 0x00C 'play a note on an object' decoded move 'bit[0x00C] = 0x0001'
Add-Name 20 bit 0x010 'drafts are blocked by noise' reading notEqualZero 'bit[0x010]'
Add-Name 42 bit 0x01A "script 042's actor is on screen" decoded move 'bit[0x01A] = 0x0001'
Add-Name 117 var 0x00E 'music flag / CD cue timer' reading print 'Music Flag'
# Reused actor slots are scoped to the reviewed scene; only the boot-established ego is global.
Add-Name 42 actor 2 'flying actor of Room 03' decoded actorOps '0x02, [costume, 0x05' 'script:42'
Add-Name 44 actor 3 'fairy' reading actorOps '0x03, [costume,' 'script:44'
Add-Name 46 actor 2 'Elder (actor 2)' reading print '0x02, [center;' 'script:46'
Add-Name 46 actor 3 'Elder (actor 3)' reading print '0x03, [center;' 'script:46'
Add-Name 46 actor 4 'Elder (actor 4)' reading print '0x04, [center;' 'script:46'
Add-Name 46 actor 5 'Hetchel' reading print '0x05, [center;' 'script:46'
Add-Name 47 actor 5 'Hetchel' reading print '0x05, [center;' 'script:47'
Add-Name 69 actor 2 'Fleece' reading print 'My name is Fleece' 'script:69'
Add-Name 85 actor 3 'Rusty' reading print 'Rusty Nailbender' 'script:85'
Add-Name 86 actor 5 'Mr. Stoke' reading print '0x05, [center;' 'script:86'
Add-Name 87 actor 5 'Mr. Stoke' reading print 'One blasted stick' 'script:87'
Add-Name 90 actor 2 'Mandible' reading print 'I am Bishop Mandible' 'script:90'
Add-Name 90 actor 3 'Cob' reading print '0x03, [center;' 'script:90'
Add-Name 91 actor 2 'Mandible' reading print '0x02, [center;' 'script:91'
Add-Name 91 actor 3 'Cob' reading print '0x03, [center;' 'script:91'
Add-Name 93 actor 3 'Cob' reading print '0x03, [center;' 'script:93'
Add-Name 98 actor 3 'Rusty (ghost)' reading print "I'm NOT well. Actually, I'm dead." 'script:98'
Add-Name 99 actor 3 'Master Goodmold' reading print 'Ah, the Weaver boy' 'script:99'
Add-Name 105 actor 4 'the Dead One (El Muerto)' reading print '0x04, [center;' 'script:105'
Add-Name 108 actor 6 'Glassmaker' reading print '0x06, [center;' 'script:108'

$review | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $repo 'src/SCUMMexplorer.Core/Formats/loom-cd-glossary.json') -Encoding utf8
$lines = @('# VGA Advanced Scripts review, 7 October 2026', '',
    'Every supplied CD global is reviewed below against its decoded instructions and the EGA reference. Names describe static code, not proof that a branch runs. Character/story readings retain the reading evidence level. The runtime accepts reviewed labels only when the decoded instruction fingerprint matches; another build remains unnamed until checked.', '',
    'CD differences: 4 yields once, 39 and 115 return immediately; 40 plays CD effect cues; 90 includes Mandible''s introduction after the Room 42 flashes; 99 adds Goodmold''s aftermath before the Room 20 object draws. The EGA save/music titles are not copied onto CD stubs. CD 51/55/116 are absent; 117-125 are CD additions.', '',
    '| CD script | EGA instructions / CD instructions | Title | Evidence | Source |',
    '| --- | --- | --- | --- | --- |')
foreach ($row in $review) {
    $s = $cd | Where-Object ScriptId -eq $row.id; $old = $ega | Where-Object ScriptId -eq $row.id
    $lines += '| {0:000} | {1} / {2} | {3} | {4} | {5} |' -f $row.id,$old.Instructions.Count,$s.Instructions.Count,$row.name,$row.evidence,$row.source
}
$lines | Set-Content (Join-Path $PSScriptRoot 'REVIEW.md') -Encoding utf8
