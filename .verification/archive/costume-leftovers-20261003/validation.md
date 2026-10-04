# Costumes that do not show (3 October 2026)

Question: why do costumes 20, 21, 42, 66, 70, 74, 75, 89, 90, 91, 136 and 171-176 show no picture in the gallery?

Method: a throwaway probe (not committed) scanned all 176 costume directory entries of `Games/loom` through `LoomCostumeReader`/`LoomCostumeRenderer`, then searched every LFL for a valid costume header (length word, format 0x58, offset table inside the resource) at the indexed offset, and asked `LoomRelationshipIndexer` for any script that uses them.

Result: 159 costumes decode and draw. The 17 others fail at the header, before any drawing, because the index entry points at nothing that is a costume:

| Costume | Index entry | Why it cannot be read |
| --- | --- | --- |
| 074, 075 | 21.LFL | the file does not exist in this release |
| 020, 021, 070, 090, 091 | 10.LFL@34497, 03.LFL@15726, 15.LFL@23562, 22.LFL@22088, 16.LFL@55530 | offset lies beyond the end of the file |
| 042, 066, 172, 174, 175 | 09@30998, 15@19325, 53@30255, 19@32116, 19@33119 | the word at the offset is not a plausible length |
| 089, 136, 171, 173, 176 | 20@17805, 27@7972, 18@18619, 53@36514, 47@21463 | format byte is 0x08, 0x00, 0x00, 0x3C, 0x01 (not 0x58) |

No other LFL holds a valid costume header at those offsets. None of the 17 is named by any decoded script (248 literal costume uses, none of them these). The same situation is recorded for scripts 000/051/055/116 in LOOM_SCRIPT_MAP.md.

Conclusion: leftover index entries, not a renderer or reader bug. ScummVM reads the index the same way, so it could not load them either. Reading, not proof: the data of the real costumes at those places was probably removed when the release was built.

## Correction and fuller check (same day)

The first "no script uses them" check read only the global scripts (the relationship index does not disassemble room entry, local or object scripts). Redone over global scripts, room entry/exit scripts, room local scripts and object verbs: the 17 are still never named by a `costume`, `loadCostume` or `lockCostume` operand. Only three costume operands are non-literal (Script 17 +009B from the size variables, Room 34 local 202 +00EF `var[0x07D]`, Room 34 local 217 +002D `local[1]`), which are the ego/Rusty costumes. So the conclusion holds. (104 and 166 are also never named, but they decode.)

## The odd-looking costumes (reading, not tested against ScummVM frames)

- 101: Room 31 loads it and gives it to actor 7 (entry +001A; local 200 +000F), animation 6/7/8, at (124, 119), next to the line "¿Ese reflejo soy YO? Qué feo." One limb, six cels (the sparse dotted ones and two solid ones) played in the order 1,2,3,4,5,6,5,3,2,1. It looks like Bobbin's reflection in the water: dotted cels are what the reflection is made of, so the gallery's pose 04–07 are not decode damage. Reading from the room context.
- 99: Room 31 local 200 +002C: actors 3–6, a 2×4 pixel costume in two limbs. Four small things around the reflection scene (water sparkles or ripples, reading).
- 94: Room 34 entry +02DC/+02EC/+0312, only when `bit[0xD62]` is set: actors 7 and 8 at (511, 116) and (567, 123), thin flat cels (24×5, 58×4). Placed in Room 34's far right; looks like a ground feature, not a character.
- 63: Rooms 15–18 local scripts (e.g. Room 17 local 201): actor 2, a 2×1 pixel twinkle put at one of five fixed points for about six frames, removed, then a random wait, repeat.

## Costume background colour

Black costume pixels (palette entry 16) vanished on the near-black gallery background (11, 17, 23). The costume cards and the pose gallery now use (31, 45, 61). Rooms, the room viewport and other controls are unchanged. Shots: `shots/cards.png`, `shots/costume101.png` (scratch harness over the real MainWindow).

## Hidden in normal mode

The costume cards of entries with no costume data are removed after their thumbnail attempt fails with an invalid-data or missing-file error (other errors keep the grey tile). Harness count: 159 cards; absent: 20, 21, 42, 66, 70, 74, 75, 89, 90, 91, 136, 171-176 (0 is no costume). Advanced mode lists all costumes in the resource tree, unchanged.
