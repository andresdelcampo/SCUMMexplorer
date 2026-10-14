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
