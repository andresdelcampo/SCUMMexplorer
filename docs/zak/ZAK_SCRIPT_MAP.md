# Zak McKracken script map

This map records **Zak McKracken-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md).

## Edition scope

Classic v1 (`Games/zak ega en`, DOS EGA, English; global scripts decoded in the v2 dialect). Tables below come from the classic copy (14 October 2026).

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form.

## Boot and hooks

Boot script number, what it sets (ego, hook scripts, input script, cutscene hooks, text speed), the order in which it starts rooms and scripts.

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|
| ScummVM runs script 1 at boot (`runBootscript`); Script 01 sets actor 1 costume 30 (+0x11) and palette 0 (+0x15) then 14 (+0x10C), actor 10 costume 31 (+0x104) palette 7 (+0x108) | the start looks | decoded | Script 01 | 2026-10-14 |

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

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|
| Palette per actor (constant `actorOps` sub 2): actor 1: 0, 13, 14; 4: 8 (Script 05 +0x1C); 8: 2, 12; 9: 3; 10: 1, 7, 9, 14; 11: 1, 9; 12: 1, 2, 12, 14. Actors 2, 3, 5, 6, 7 never get a palette (they stay 0) | actor palettes | decoded | global and room scripts | 2026-10-14 |
| Costume to actor (constant sub 4): actor 1: 1, 5, 30; 2: 6; 3, 4: 7; 5: 12, 13; 6: 14, 15; 7: 28; 8: 26; 9: 11; 10: 22-25, 29, 31-34; 11: 10, 27; 12: 18, 19, 20, 28; Room 38 entry: costume 21 on `var[0x042]` with palette 9 | who wears what | decoded | global and room scripts | 2026-10-14 |
| Costumes no constant instruction gives to anyone: 2, 3 (worn by actors 2 and 3 from the start), 4, 8, 9 (initial actors with later palettes), 16, 17, 35: their colours are labelled estimates | costume colours still estimated | decoded | scan of all scripts | 2026-10-14 |

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|

## Open questions

What is not decoded or not verified, and what would settle it.

- The colour derivation ignores a variable actor in the Room 38 entry script and control flow inside a costume/palette span; settling the 10 estimated costumes needs a running session (S4/S6). Zak classic Script 39 and a few entry scripts jump outside their resource.
