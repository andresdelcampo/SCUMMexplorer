# Monkey Island 2: LeChuck's Revenge script map

This map records **Monkey Island 2: LeChuck's Revenge-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md).

## Edition scope

Facts below are from `mi2 talkie en` (DOS release with the fan "Talkie CD 0.2 beta" patch, Script 1 +0x01C5), 17 October 2026 (S4b/S5). S9 (18 October 2026) checked `mi2 mt32 en`, `mi2 sb16 en`, `mi2 classic es` (floppy releases) and `mi2 talkie es`; rows marked *floppy* or *talkie patch* are the genuine differences found ([record](../../.verification/monkey2-editions-20261018/README.md)). Offsets of the floppy rows are `mi2 mt32 en`'s.

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form.

## Boot and hooks

Boot script number, what it sets (ego, hook scripts, input script, cutscene hooks, text speed), the order in which it starts rooms and scripts.

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Boot parameter -7873 | treated as 0 (`local[0]` = 0) | decoded | Script 1 +0x0004 | 2026-10-17 |
| `monkey2.cfg` | `roomOps loadString 0x1B`; first character's bits 0 and 2 set `bit[0x24B]`, `bit[0x24C]`; a missing file leaves "E`@@@@@@@" (both 1) | decoded, play | Script 1 +0x001D..+0x006A | 2026-10-17 |
| iMUSE volume | `soundKludge [6, vol]` (96 when the config byte is 0) when `VAR_SOUNDCARD` ≤ 3 | decoded | Script 1 +0x006A..+0x0089 | 2026-10-17 |
| Easy / Regular | boot parameter ≥ 20000 or 10001: Easy (`bit[0x237]` = 1, `var[0x062]` = 1, string 0x1A "Easy Mode"); else Regular until the choice | decoded | Script 1 +0x017B..+0x01C5 | 2026-10-17 |
| Choice screen | Script 130 (from Script 1 +0x09F5), Room 108; waits at +0x034A for `var[0x06A]` = -2 | decoded, play | Script 130 +0x0336..+0x034A | 2026-10-17 |
| Copy protection | Script 128 skips to +0x0248 when `local[0]` = 0 (the recipe screen, "LAME-O COPY PROTECTION", runs otherwise); Script 130 +0x005F likewise | decoded, play | Script 128 +0x0000, Script 130 +0x005F | 2026-10-17 |
| Copy protection (*floppy*) | no skip for boot parameter 0: Script 128 prints "PRESS ENTER TO BEGIN LAME-O COPY PROTECTION." (input script 132 from +0x000E, waits for `var[0x06A]` ≠ 0) and draws the recipe (objects 0x482/0x483 with random states); Script 130 builds the expected answer from strings 0x2A/0x2C into `var[0x206]` (+0x0134), sets input script 131 (+0x0157), reads four digits (place value 1000 down, +0x015C..+0x0210; key 8 restarts), compares with `var[0x1EA]` (+0x0255), and after three wrong answers `systemOps quit` (+0x0279); then the choice screen as in the talkie (+0x02A0..+0x0300) | decoded, play | Script 128 +0x0000..+0x00EE, Script 130 +0x0021..+0x0300 (`mi2 mt32 en`; the same in `mi2 sb16 en`, `mi2 classic es`) | 2026-10-18 |
| Input script 131 keys (*floppy*) | key '0'..'9' sets `var[0x06A]` = digit, key 8 sets 11; ',' with `var[0x027]` ≥ 1 sets -100 (a skip the boot never takes) | decoded | Script 131 +0x0000..+0x003F | 2026-10-18 |
| First playable state | the campfire, Room 4, after Rooms 87, 3, 103, 109, 5 (3409 frames; 2841 since S7's beat; floppies 3657 to 3699 with the protection) | play | S4b record, S9 record | 2026-10-17 |
| After the campfire | Room 4 local script 204 walks Guybrush off, arms jump hook 1 of sound 108 (+0x016A), waits for the song to end (+0x01A0..+0x01BA), chains to Script 93 (`chainScript 0x5D`), which brings him to Woodtick (Room 7, (696, 53)) | decoded, play | Room 04 local script 204, S6 record | 2026-10-17 |

## Engine variables as this game uses them

Values this game selects for engine-defined slots (see the engine table). One row per slot actually used.

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|
| `VAR_VERB_SCRIPT` | Script 4 in play; Script 14 in a dialogue; Room 7 local script 206 while Largo holds Guybrush | decoded, play | S6 route | 2026-10-17 |
| `VAR_SOUNDRESULT` (`var[0x038]`) | answer of `soundKludge`; Room 4 local script 204 polls sound 108's status through it | decoded, play | Room 04 local script 204 +0x01AD | 2026-10-17 |

## Game variables and bits

| Name | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Global scripts

| Script | Role | Evidence | Source | Date |
|---|---|---|---|---|
| Script 76 | LeChuck catches Guybrush with the voodoo key (Room 74, Part 3): `cutscene` +0x0000, starts sound 176 +0x0019, waits for the song's beat 9 (`soundKludge [0x0100, 0xB0, 7]`, +0x0090..+0x00DF) before LeChuck walks in, then a dialogue (waits for `var[0x0AF]`, +0x05D2..+0x05DC) | decoded, play | Script 76 | 2026-10-18 |
| Script 101 | Captain Dread's scenes (Room 22 entry); its first print is only a string control `\xFF\x07` | decoded | Script 101 +0x00BE | 2026-10-18 |

## Rooms

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|
| Room 7 entry | starts local script 202 when `var[0x066]` = 0x0B (back from Room 11's window) and local script 203 (Largo at the bridge) when `bit[0x168]` is set; 203 clears the bit, waits for Guybrush at x ≤ 0x2E1, gives control, and begins its cutscene (sound 151) only when he walks west of x 0x29E | decoded | Room 07 entry script, local script 203 +0x0000..+0x007C | 2026-10-18 |
| Room 9 object 73 (barkeeper) | verb 0x0A: the Bloody Lip dialogue (local scripts 213, 212, 203, global script 70; sound 152 from Script 151 +0x0028) | decoded, play | Room 09 object 73 verb 0x0A | 2026-10-18 |
| Room 13 (Largo's room at the inn) | local script 211 waits 1800 jiffies (+0x0004), then a cutscene (+0x0010): Largo comes in ("Who let you into my room?!!"), starts sound 182 (+0x0072); Guybrush ends in Room 12 | decoded, play | Room 13 local scripts 211, 213 | 2026-10-18 |
| Room 74 object 755 (voodoo key to jail) | verb 0x09 starts Script 76 | decoded, play | Room 74 object 755 verb 0x09 +0x0000 | 2026-10-18 |
| Room 7 object 46 (sign) verb 0x08 (*talkie patch*) | the patch wraps the second line in `cutscene` with an override (+0x002A..+0x0070); the floppies print both lines in one `printEgo` (`\xFF\x03` between) with no cutscene. The patch does the same to 70 other multi-line descriptions (71 more cutscene groups than the floppies, 67 of them begin from a fresh start) | decoded, play | Room 07 object 46 verb 0x08 (`mi2 talkie en` +0x0000..+0x0071; `mi2 mt32 en` +0x0000..+0x0043) | 2026-10-18 |
| Room 45 (*talkie patch*) | the patched entry names object 471 (+0x003F) and holds one more object image (1 against 0); none active at the first visit in either | decoded | Room 45 entry script (78 instructions; floppy 75) | 2026-10-18 |
| Room 50 (*talkie patch*) | the patched room scripts (1089 instructions; floppy 930) keep a script running, so the first visit does not settle in 2400 frames (floppy settles in about 470); same active object (574). Close coffin (object 574 verb 0x03) begins its cutscene from a fresh start only in the talkie | decoded, play | room audit, cutscene runs (S9 record) | 2026-10-18 |
| Room 85 object 792 verb 0x07 (*floppy*) | Use ocean has a cutscene trigger in the floppies (local script 208) that the patched talkie lacks | decoded | cutscene scan (S9 record) | 2026-10-18 |
| Room 80 | the bone-song sheet: the entry prints the verses from string 0x2E and waits for a click (`var[0x06A]`, +0x01F1..+0x01F7) | decoded | Room 80 entry script | 2026-10-18 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Verbs 2–10, 0x0B | 2 Open, 3 Close, 4 Give, 5 Push, 6 Pull, 7 Use, 8 Look at, 9 Pick up, 0x0A Talk to, 0x0B Walk to (the default); prepositions 0x81–0x84 "in", "with", "on", "to" | decoded, play | Script 22 verb set-up, S6 route | 2026-10-17 |
| Verbs 0xC8–0xCF, 0xD0/0xD1 | inventory slots (image verbs, empty image Room 94 object 956) and scroll arrows (957); Script 9 gives each held object's icon from Room 94 (its verb 0x5B names it, e.g. the shovel's 911) | decoded, play | Script 4 +0x0455..+0x04B4, Script 9, Room 07 object 45 verb 0x5B | 2026-10-17 |
| Verb 0x64 | the sentence line, charset 1, centred at (160, 145) | play | S6 route | 2026-10-17 |
| Verbs 0x78–0x80 | dialogue choices at x 0, y 145 + 8 per row; names carry the talkie cue (code 10) and end with code 9 | decoded, play | Room 07 local script 204 +0x00BE.. | 2026-10-17 |
| Script 4 | input script: area 4 keys (cheats, settings), a right click takes the default verb from `var[0x09C]` (+0x0005..+0x0016), inventory clicks, `doSentence` (+0x05C2), walk (+0x05EC) | decoded, play | Script 4 | 2026-10-17 |
| Script 14 | dialogue input script (Largo at the bridge) | play | S6 route | 2026-10-17 |
| Room 7 local script 206 | input script of the second Largo list: prints the chosen verb (code 5) and puts it in `var[0x0AF]` | decoded, play | Room 07 local script 206 +0x0024..+0x0056 | 2026-10-17 |
| Script 131 (0x83) | input script of the choice screen (`VAR_VERB_SCRIPT` from Script 130 +0x0178/+0x033D): keys give digits to `var[0x06A]`; a room click finds the object under the pointer: 0x484/0x486 "Monkey Island 2" (Regular), 0x485/0x487 "Monkey 2 Lite" (Easy), each sets `var[0x06A]` = -2 | decoded, play | Script 131 +0x0000..+0x00CF | 2026-10-17 |

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Room 108 objects 1154, 1155 | the recipe wheel halves (15 state images each, IM01..IM0F) | reading | Room 108 OBIM | 2026-10-17 |
| Room 108 objects 1156–1159 | choice boxes (1156 full game, 1157 Lite) and their text areas; class 32 cleared at Script 130 +0x031A | decoded | Script 130 +0x030E..+0x0328 | 2026-10-17 |
| Rooms 104–106 | image holders: no background, no scripts, objects at (0,0) | reading | S5 audit | 2026-10-17 |
| Room 7 object 46 (sign) | verb 8: "`Welcome to Woodtick`", cutscene with override (+0x0028..+0x002F) skips "`No Trezer huntin zone`"; verb 9: `pickupObject 0x2D` (the shovel, 45) and "Oops… I think I broke the sign"; verb 0x5A: default verb 8 | decoded, play | Room 07 object 46 | 2026-10-17 |
| Room 7 object 41 (dinky door) | verb 0x0B: `loadRoomWithEgo 0x40, 8` → Room 8 (the map shop), Guybrush at (51, 112) | decoded, play | Room 07 object 41 verb 0x0B +0x0023 | 2026-10-17 |
| Room 7 objects 49–56 (riches) | Guybrush's starting inventory; Largo takes them at the bridge | play | Room 07 local script 204 | 2026-10-17 |
| Room 51 object 0x24D | entry script puts it in state 3 (IM03 differs from IM01): the play-screen state-image test | decoded, play | Room 51 entry script +0x0007 | 2026-10-17 |

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|
| `soundKludge` | iMUSE doCommand lists: 817 `[-1]` flushes; player commands 0x10F (enqueue command), 0x10B (part on/off), 0x10D (fade), 0x110 (clear queue), 0x100 (getParam), 0x10E (trigger), 0x10C (hook), 0x116 (part volume), 0x0D (status), 0x0B (stop all) are the most used | reading | scan of all scripts | 2026-10-17 |
| Sound 154 | the opening's song; Script 52 queues commands on its markers | decoded | Script 52 +0x0010..+0x018E | 2026-10-17 |
| Sound 108 | the campfire song (92 s of track 0, looping on its hook-0 marker); hook-1 markers every 2.8 s jump to track 1 (beat 4, tick 400), whose rest ends it: after Room 4 local script 204 arms hook 1 at 92.6 s the song ends at 98.97 s (session clock, estimate) | reading, play | sound 108 ADL/ROL parts; Room 04 local script 204 +0x016A | 2026-10-17 |
| Sound 107 | Woodtick music (AdLib rendition 233.6 s before its loop point) | play | Room 04 entry script +0x006D | 2026-10-17 |
| Sound 151 | Largo's theme (AdLib 190.5 s) | play | Room 07 local script 203 +0x0075 | 2026-10-17 |
| `soundKludge [0x0100, s, 7]` | `Player::getParam` 7, the song's beat (tick / 480 + 1); 8 the tick in the beat; 110 sites (Scripts 47, 48, 52, 58, 76, 119, 120, 122, 146, 164, 165, 199; Rooms 5, 17, 20, 40, 56, 60, 79, 86); 7 and 8 answered from the song's AdLib timeline since S7 (estimate) | decoded, play | `CutsceneList find` | 2026-10-18 |
| Sound 176 | LeChuck's arrival (AdLib 185 s) | play | Script 76 +0x0019 | 2026-10-18 |
| Sound 182 | Largo in his room (AdLib 200.7 s) | play | Room 13 local script 211 +0x0072 | 2026-10-18 |
| Sound 6 | the sign breaking | decoded | Room 07 object 46 verb 9 +0x0010 | 2026-10-17 |

## Open questions

What is not decoded or not verified, and what would settle it.
