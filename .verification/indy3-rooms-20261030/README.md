# Indy 3 EGA: rooms that never settle, getActorWidth, actorFollowCamera into another room (30 October 2026)

| | |
|---|---|
| Item | [Indy 3 tracker](../../docs/indy3/SUPPORT_STATUS.md), S5 open items: "rooms 1, 6, 15 and 13 more never rest within 2400 frames; Script 23's `actorFollowCamera` of an ego in another room (ScummVM `setCameraFollows` loads that room) is not modelled" |
| Fixture | `D:\Juegos\Lucas\indy3 ega en` |
| ScummVM | `camera.cpp` `setCameraFollows`, read on GitHub master 30 October: `if (!a->isInCurrentRoom()) startScene(a->getRoom(), nullptr, 0)`, no version distinction |

## Changes

1. **`getActorWidth`** (ScummVM `o5_getActorWidth`, `_width`): the actor table keeps a width, 24 from `initActor` (`ScummActorTable.DefaultWidth`), changed by `actorOps width`; the executor answers it as an instruction and nested in an expression. Room 6's Script 56 +0x0000 stopped on it ("nested getActorWidth is unsupported").
2. **`actorFollowCamera` into another room in version 3**: `ScummProfile.CameraFollowStartsScene` was off for engine version 3 (written to keep LOOM unchanged, but only `ScummGameSession` reads it). It is now on for every version.

## Before/after (room audit, only the profile line changed)

| Room | Before | After |
|---|---|---|
| 6 | FrameLimit 2400, camera unknown (Script 73 follows Indy into Room 9) | RoomLeft to Room 9 after 96 frames |
| 18 | Settled 122 | RoomLeft after 23 frames |
| 78 | Settled 265 | RoomLeft after 166 frames |
| 80 | FrameLimit 2400 | RoomLeft after 136 frames |

All other rooms of the 88 are unchanged by this line; all 88 run without a gap. The remaining differences against the 14 October audit (Rooms 17, 23, 24, 33, 70, 72: more active images or later settling) come from earlier work, not from this change.

## Still open

- Rooms 1 and 15 never rest (crowds and ambient scripts keep changing them); Room 13 (and Room 3) start a script whose arguments depend on unknown variables (Script 131 +0x0038, Room 3 entry +0x006F). Not changed.
- The user's look at Rooms 6, 18, 78, 80, whose first-visit pictures now come from the moment the scene leaves them.

## Checks

- `ScummIndy3FirstVisitTests` (new): Room 6 leaves for Room 9 with nothing unfollowed; actor width defaults and `actorOps`.
- Indy 3 filter (69 tests) and full suite: 1831 passed, 0 failed, 5 skipped.

## Addendum, 30 October: indexed script arguments

`SubstituteLocalArguments` passed a started script's arguments by their raw text, so an indexed variable stayed unresolved: Room 13's Script 131 +0x0038 (`local[0x003+0x000]` .. `local[0x003+0x004]`, random delays for Script 132) and Room 3's entry +0x006F (`var[0x064+local[0x000]]`, twelve runs of local script 200). It now reads an indexed argument at its resolved slot when the script starts (as `getWordVararg` does). Full Indy 3 EGA audit: all 88 rooms run with nothing unfollowed; the only change is Room 3, now 13 of 28 images (the classroom's students). `ScummIndy3FirstVisitTests.AStartedScriptReceivesItsIndexedArguments`; full suite 1835 passed.

Rooms 1 and 15 still never rest: their crowds and ambient scripts keep changing them (the state at the frame limit is shown).
