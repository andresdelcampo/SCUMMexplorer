# Validation: loadRoom arrivals for catalog visits (28 September 2026)

Status: **done.** Builds with 0 warnings; **186/186 tests** pass (1 new: `LoomActorTests.RoomsWithoutLoadRoomWithEgoTakeALoadRoomThatPutsTheEgoIn`). Room audit: 0 gaps in 76 rooms, **0 changed renders**, report unchanged. Core only; the app did not change. WPF checks rerun ([reports](wpf/), with `.verification\wpf-checks.ps1 -OutRoot <fresh folder>`): ClickInputCheck **11/11**, DoorwayClickCheck **75/75**, Room03ActionCheck `162 4 167 38` **46 PASS**, PointerInputCheck **13/13**, ReverseHoverCheck **22/22**, HighlightCheck **19/19**, MusicRowCheck **5/5**, CameraPanCheck **11/11**, PresentationCheck **39/39**. No physical input.

## The problem

After a catalog visit, Rooms 32, 43 and 65 still left the ego unknown: no decoded `loadRoomWithEgo` brings the ego into them ([catalog arrivals](../catalog-arrivals/validation.md)). Blockers counted 14 unknown-walk-box and 6 unknown-`getActorX` stops there.

## How the game brings the ego in (decoded)

No `loadRoomWithEgo` in the release has a variable or local operand (every one is literal), so there was nothing to add from non-literal operands. These rooms are entered by a global script that runs `loadRoom` and then, after the new room's entry code (ScummVM `startScene` runs it first), puts the ego there:

| Room | Source | Steps after the load |
| --- | --- | --- |
| 65 | Script 072 +0x0024 | `setCameraAt 0xA0`; `putActorInRoom 1, 0x41`; `putActorAtObject 1, 0x33B` (object 827); `walkActorTo 1, 0xE4, 0x2C` |
| 32 | Script 079 +0x001C | `setCameraAt 0x130`; `putActor 1, 480, 80`; walks to (430, 80) and (442, 72); `putActor 1, 442, 72` |
| 32 | Script 080 +0x0026 | `setCameraAt 0x130`; `putActorAtObject 1, 0x21A` (object 538); walks to (372, 118) |
| 43 | Script 091 +0x0A50 | `setCameraAt 0xA0`; `putActor 1, 142, 93` |
| 43 | Script 092 +0x0005 | `setCameraAt 0xA0`; `putActor 1, 142, 93`; walks to (170, 108) |

Scripts 090, 093 and 094 also `loadRoom 0x2B`, but they do not put the ego there in straight-line code (dialogue returns; script 093 +0x05DA puts the ego before a conditional load), so they are not arrivals. Script 068's `loadRoom local[0]` is a dialogue return that never places the ego.

## What the session does now

- `LoomEgoArrivalIndex` also collects a literal `loadRoom R` that is followed, without a jump, a branch, another load, `chainScript` or the end of the script, by `putActorInRoom 1, R` (actor 1 or `var[0x001]`) and literal `putActor`/`putActorAtObject`/`walkActorTo` steps of that actor. Scanning stops at any other instruction that moves the actor, or one whose actor is not a literal. The camera is the last literal `setCameraAt` before the actor's last step. A `putActorAtObject` whose object is not in the room is dropped.
- **`loadRoomWithEgo` arrivals (the doorways) come first**; a `loadRoom` arrival is used only when a room has none. The previous room's own code and the seeded pick among several apply as before, and the label says "no decoded loadRoomWithEgo brings the ego in, so this is a loadRoom followed by putting the ego in the room".
- The arrival is applied after the entry hooks, as the script's continuation would be: `putActorInRoom`, then the steps (walks end at once), then the camera at the literal `setCameraAt`. `startScene` resets the camera to normal mode (ScummVM `room.cpp`), so the camera does not follow the ego. Every value is an estimate labelled "assumed arrival at Script NNN +0xNNNN after a catalog visit". The script's other effects (classes, draws, box flags, cutscene) are not applied.
- A `loadRoom` arrival names a literal actor; it is used only when `var[0x001]` holds that actor (1 after the boot).

## Results

`VisitProbe Games\loom <room> +8`, over all 76 rooms from the boot (Room 02): exactly the eight rooms with no `loadRoomWithEgo` arrival take a `loadRoom` one; every other room's arrival label is unchanged.

| Room | Arrival | After +8 frames |
| --- | --- | --- |
| 32 | Script 080 +0x0026 (seeded pick 2 of 2; alternative 079 +0x001C): object 538's walk point, then (372, 118); camera 304 | entry +0x005E `getActorX` answered (estimate) |
| 43 | Script 091 +0x0A50 (seeded pick 1 of 2; alternative 092 +0x0005): (142, 93), box 0; camera 160 | local 204 `getActorWalkBox` answered (estimate) |
| 65 | Script 072 +0x0024 (the only one): object 827, then (228, 44), box 1; camera 160 | local 200 `getActorWalkBox` answered (estimate) |
| 12, 28, 31, 49, 66 | Scripts 053, 077, 084 (pick; 082), 110, 102 | no ego stop before or after |

**Blockers** ([before](blockers-before.txt) = the click-input baseline, [after](blockers-after.txt)):

| Stop or label | Before | After |
| --- | --- | --- |
| `getActorWalkBox` reads an actor whose walk box is unknown | 14 | **0** |
| `getActorX` reads an actor whose position is unknown | 6 | **0** |
| Camera unknown | 14 | 1 |
| Catalog visit with no source-backed arrival | 41 | 33 |
| Catalog visit with a `loadRoom` arrival | – | 8 |
| `getActorWalkBox` / `getActorX` answered from a `loadRoom` arrival (estimate) | 4 / 0 | 18 / 6 |

A few sound and animation counts moved by 1–5 because scripts in these rooms now run further. No `loadRoomWithEgo` arrival count changed.

## Note on harness runs

The per-room probe loop must use only indexed room numbers: `VisitProbe` throws for a room that does not exist (for example 21 or 48), and each throw is a `dotnet.exe` crash in the Windows event log.
