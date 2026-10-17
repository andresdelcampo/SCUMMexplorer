# Maniac Mansion script map

This map records **Maniac Mansion-specific** script roles, variable meanings, verb ids and room behaviour. Engine-defined variables and opcode rules are in the [SCUMM script reference](../engine/SCUMM_SCRIPT_MAP.md).

## Edition scope

Classic v1 (`Games/mmansion ega en`, DOS EGA, English; ScummVM v1 tables) and enhanced v2 (`Games/mmansionE ega es`). Tables below come from the **classic v1** English copy unless a row says otherwise (14 October 2026).

## Row format (every table below)

Each row: **item** | **meaning** | **evidence** (`decoded` / `engine` / `reading` / `play`, R-2.3) | **source** (`Script NN +0xOFFS`, `Room NN local script NN`, `Room NN object NN verb 0xHH`) | **date**. A row without a source is not allowed. Unnamed things keep their raw form.

## Boot and hooks

Boot script number, what it sets (ego, hook scripts, input script, cutscene hooks, text speed), the order in which it starts rooms and scripts.

| Item | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

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
| Classic 01 | The selected-party preview settles with visible images 56, 57, 62, 63, 64; state flags other than intrinsic visibility do not paint their images. | engine | Classic Room 01 entry; indexed states and reached session, `ScummManiacClassicFirstVisitTests`; [S5 record](../../.verification/maniacclassic-objects-20261017/README.md) | 2026-10-17 |
| Classic 02 | The bounded preview settles with no active object image; objects 141 and 167 retain state 1, which is not the intrinsic visibility bit. | engine | Classic Room 02 entry and reached session; `ScummManiacClassicFirstVisitTests` | 2026-10-17 |
| Classic 18 | Six 8-by-8 coin-slot rectangles already contain 58 black pixels each in the room background. The stored-image gap detector reports them even when the entire object composite equals the background; no filling is applied. | decoded | Classic Room 18 background; object records 242–247 at x=192/248/312/368/424/488, y=88; S5 audit and regression | 2026-10-17 |
| Classic 30 | The entry tests object 176's intrinsic bit; the selected-party catalog preview reaches the 2400-frame settling bound. | engine | Classic Room 30 entry +0x001B; [source comparison](../../.verification/maniacclassic-objects-20261017/final/source-comparison.md) | 2026-10-17 |
| Classic 44 | First playable room from the reviewed Syd/Bernard party clicks; object states 258=0 and 278=0, no active object image. | engine | Classic Room 44 reached through boot/input scripts; `ScummManiacClassicFirstVisitTests` | 2026-10-17 |
| Enhanced 01, 02, 44 | Reviewed selected-party previews: Room 01 shows images 56, 57, 62, 63, 64; Rooms 02 and 44 show no active object image. These are edition-local test results, not reused classic state writes. | engine | Enhanced fixture's entry/index/boot scripts; `ScummManiacEnhancedFirstVisitTests`; [S5 record](../../.verification/maniacenhanced-objects-20261017/README.md) | 2026-10-17 |
| Enhanced 30, 45 | Bounded previews reach the 2400-frame limit; all other 51 indexed rooms settle under the selected-party recipe. | engine | Enhanced room entries and reached child scripts; [source comparisons](../../.verification/maniacenhanced-objects-20261017/source-comparisons.md) | 2026-10-17 |

## Verbs and the input chain

| Verb / script | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Objects and classes

| Object / class | Meaning | Evidence | Source | Date |
|---|---|---|---|---|

## Actors and costumes

| Actor / costume | Meaning (look, palette, talk colour, who wears what) | Evidence | Source | Date |
|---|---|---|---|---|
| `actorOps` sub 1 sound, 2 palette (v1: index = actor number), 3 name, 4 costume, 5 talk colour | how scripts set an actor's look | engine | ScummVM `script_v2.cpp` `o2_actorOps` | 2026-10-14 |
| The only costume instruction in the global and room scripts sets costume 0 on actor `var[0x060]` | an actor is hidden | decoded | Script 61 +0x010E | 2026-10-14 |
| Each actor's start costume is its own number; colours are `v1MMActorPalatte1/2[actor]` | costume to colour rule | engine | ScummVM `costume.cpp` | 2026-10-14 |
| Global scripts 0 and 1 begin with actor-name/sound instructions on actors 1 to 8 | likely data tables; they decode only a handful of bytes | reading | Scripts 00, 01 | 2026-10-14 |

## Sounds and music

| Sound | Meaning (device parts, who starts it) | Evidence | Source | Date |
|---|---|---|---|---|

## Open questions

What is not decoded or not verified, and what would settle it.
