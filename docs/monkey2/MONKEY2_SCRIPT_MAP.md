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

## Engine variables as this game uses them

Values this game selects for engine-defined slots (see the engine table). One row per slot actually used.

| Variable | This game's use | Evidence | Source | Date |
|---|---|---|---|---|

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
| Script 131 (0x83) | input script of the choice screen (`VAR_VERB_SCRIPT` from Script 130 +0x0178/+0x033D): keys give digits to `var[0x06A]`; a room click finds the object under the pointer: 0x484/0x486 "Monkey Island 2" (Regular), 0x485/0x487 "Monkey 2 Lite" (Easy), each sets `var[0x06A]` = -2 | decoded, play | Script 131 +0x0000..+0x00CF | 2026-10-17 |

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| Room 108 objects 1154, 1155 | the recipe wheel halves (15 state images each, IM01..IM0F) | reading | Room 108 OBIM | 2026-10-17 |
| Room 108 objects 1156–1159 | choice boxes (1156 full game, 1157 Lite) and their text areas; class 32 cleared at Script 130 +0x031A | decoded | Script 130 +0x030E..+0x0328 | 2026-10-17 |
| Rooms 104–106 | image holders: no background, no scripts, objects at (0,0) | reading | S5 audit | 2026-10-17 |

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|
| `soundKludge` | iMUSE doCommand lists: 817 `[-1]` flushes; player commands 0x10F (enqueue command), 0x10B (part on/off), 0x10D (fade), 0x110 (clear queue), 0x100 (getParam), 0x10E (trigger), 0x10C (hook), 0x116 (part volume), 0x0D (status), 0x0B (stop all) are the most used | reading | scan of all scripts | 2026-10-17 |
| Sound 154 | the opening's song; Script 52 queues commands on its markers | decoded | Script 52 +0x0010..+0x018E | 2026-10-17 |

## Open questions

What is not decoded or not verified, and what would settle it.
