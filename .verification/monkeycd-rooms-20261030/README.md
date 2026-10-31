# Monkey Island CD: Rooms 88, 89, 99 and the lookout frame (30 October 2026)

| | |
|---|---|
| Items | The S5 leftovers ([monkeycd-objects-20261017](../monkeycd-objects-20261017/README.md)): Room 88 "execution reached an undecoded byte at +0x0000", Room 89 "saveRestoreVerbs subopcode 0x00", Room 99 "Object 1000 is absent from the global state table"; and "the lookout shows for one frame at start" ([checkpoint 20261017](../../docs/archive/checkpoints/20261017-S7-S9-S4b-MonkeyCd.md)) |
| Fixture | `D:\Juegos\Lucas\monkey1 vga en` (Monkey Island CD, English) |
| ScummVM | source read on GitHub master, 30 October: `room.cpp` `setupRoomSubBlocks`, `object.cpp` `getState`, `drawObject` |

## Findings and changes

| Room | Finding | Change |
|---|---|---|
| 88 | Its `ENCD` and `EXCD` blocks are empty (8 header bytes each; `BlockDump` shows the next bytes are the `NLSC` header). ScummVM keeps the data pointer of an empty block, so `runEntryScript` would read "NLSC…" as opcodes; that is not game code. | `ScummV5DataSource.RoomScript`: an empty `ENCD`/`EXCD` is read as no script. Room 88 settles with nothing unfollowed. |
| 89 | Local script 200 (random palette flashes of colours 11, 0xE5, 0xE2) now decodes in full; the `saveRestoreVerbs` stop was closed by later reader work. | none; pinned by a test |
| 99 | The index (`DOBJ`) has 1000 objects (0–999); Room 99 holds the inventory icons 955–1045. ScummVM's `getState` and `drawObject` `assertRange(0, obj, _numGlobalObjects - 1)`: the game never enters this room, and Script 9 only takes its images (`verbOps assignObject`). "Cannot run" is the right outcome; inventing states for 1000–1045 would break R-1.9. | The refusal now says why (outside the object table, ScummVM refuses it, an image-holder room) instead of "absent from the global state table". |
| Lookout frame | `StartFrames` (real `MainWindow`, private profile) picks Room 33 as a catalog click: from the first visible frame only Room 33 is shown (25 s). Fixed earlier by the catalog preparation of 19–20 October (the room is prepared offscreen before it is shown). | none |

## Checks

- `MonkeyCdRoomAudit` on Rooms 88, 89, 99: 88 and 89 Settled, nothing unfollowed; 99 refused with the new reason.
- `ScummMonkeyCdFirstVisitTests`: `RoomsWithAnEmptyEntryOrAPaletteLoopRunWithNothingUnfollowed` (88, 89), `TheInventoryIconRoomIsNotARoomTheGameEnters` (99).
- Filter `ScummMonkeyCd|V5|Monkey2|Indy4|RoomObjectSelection`: 195 passed.
- `StartFrames monkey1 vga en 33`: PASS, only Room 33 shown.
