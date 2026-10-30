# Indy 4: Rooms 76, 77, 87, the first-state images and Room 4's magenta line (30 October 2026)

| | |
|---|---|
| Items | [CURRENT_WORK](../../docs/CURRENT_WORK.md) / [tracker](../../docs/indy4/SUPPORT_STATUS.md): "Rooms 76, 77, 87 have an unfollowed camera-after-walk item"; "the play view still draws each object's first-state image"; "a magenta line crosses Room 4's picture after the boot" |
| Fixture | `D:\Juegos\Lucas\indy4 vga en` |
| Tools | `../indy4-objects-20261017/RoomAudit`, `../indy3-castle-20261030/SourceDump` (`visit:`, `box:`, `v:` modes added), `../monkeycd-rooms-20261030/StartFrames` |

## Findings

| Item | Finding | Change |
|---|---|---|
| Rooms 77, 87 | Run with nothing unfollowed today (closed by earlier work). | none |
| Room 76 | The catalog visit's assumed arrival is Room 77's door (`loadRoomWithEgo 0x0419, 76, walkTo=(735, 126)`, Room 77 object 1052 verb 0x0A +0x0023), but the entry reads `var[0x0E0]` (previous room) as the boot's room and puts the ego at object 0x040B (+0x005F..+0x0063), box 13. Boxes 0–15 have no matrix route to box 17 (`box:76:735:126:1049`). The at-once walk then gave up ("where the walk stops is not modelled"), the ego and camera became unknown, and local script 214 (the parrot's "Bwaaaaak!" when `var[0x002]` < 450) could not be followed. ScummVM `walkActor`: `getNextBox` < 0 sets the destination box to the current one and the last leg, so the walk ends where it started with the stand frame (our stepped walk already did this). | `ScummActorTable.WalkAtOnce` ends a walk with no route where it started (stand frame, look note) instead of forgetting the actor. Room 76 now settles with the ego at (50, 114), box 13, camera 160 labelled as the arrival's estimate. |
| First-state images | Stale: `ScummPlayScreen` picks v5 object images by state (`ReadObjectImages(roomId, snapshot.ObjectStates)`). | none |
| Room 4 magenta line | Not seen in the real window after the boot ([window](window/room-04.png)); likely closed by the 19 October credits fix. | none |

Not addressed: the inconsistency between a catalog visit's assumed arrival and the previous-room variable the entry reads; it is the existing labelled estimate of catalog visits.

## Checks

- Room audit 76, 77, 87: all settle or run to their limit with nothing unfollowed.
- `ScummIndy4FirstVisitTests.AnArrivalWalkWithNoRouteEndsWhereItStarts`.
- Full suite: 1831 passed, 0 failed, 5 skipped.
