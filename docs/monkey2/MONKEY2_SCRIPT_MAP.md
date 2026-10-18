# Monkey Island 2: LeChuck's Revenge script map

This map records **Monkey Island 2: LeChuck's Revenge-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md).

## Edition scope

Facts below are from `mi2 talkie en` (DOS release with the fan "Talkie CD 0.2 beta" patch, Script 1 +0x01C5), 17 October 2026 (S4b/S5). The plain copies are not checked yet (S9).

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
| First playable state | the campfire, Room 4, after Rooms 87, 3, 103, 109, 5 (3409 frames) | play | S4b record | 2026-10-17 |
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

## Rooms

| Room | What it is, entry/exit script facts, local scripts | Evidence | Source | Date |
|---|---|---|---|---|

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
| Sound 6 | the sign breaking | decoded | Room 07 object 46 verb 9 +0x0010 | 2026-10-17 |

## Open questions

What is not decoded or not verified, and what would settle it.
