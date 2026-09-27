# Validation: camera limits from `roomOps roomScroll` (27 September 2026)

Status: **done.** Builds with 0 warnings; **172/172 tests** pass (1 new: `LoomActorTests.RoomScrollLocksRoom04sCameraToTheHalfItsEntryPicks`). Room audit: 0 gaps, **0 changed renders**. DoorwayClickCheck **75/75** ([report](doorway-clicks.txt)); Room03ActionCheck `162 4 167 38` **46 PASS** ([captures](room03-action/)); CameraPanCheck **11/11** in Rooms 02, 03 and 04 ([report](pan-check/camera-pan-check.txt)).

## ScummVM behaviour (`script_v5.cpp`, `room.cpp`, `vars.cpp`, master)

- v3 uses the base variable numbering: `VAR_CAMERA_MIN_X` = 17 (`var[0x011]`), `VAR_CAMERA_MAX_X` = 18 (`var[0x012]`).
- `startScene` sets them to half a screen from the room's edges (160 and width − 160).
- `o5_roomOps` case 1 (`SO_ROOM_SCROLL`; in v3 the two operands come before the sub-opcode) keeps each limit in [160, width − 160] and stores both.
- `setCameraAt` and `moveCamera` clamp the camera to the minimum and then to the maximum; `moveCamera` moves it back inside 8 pixels a frame.

## What the session does

- Room entry sets `var[0x011]`/`var[0x012]` and `CameraRange` to [160, width − 160]. `RoomCameraBounds` keeps those widest limits.
- `roomScroll a, b` clamps both into the room's bounds, sets the two variables and narrows `CameraRange`. If the limits cross, the maximum wins, as in ScummVM's clamp order. If the camera is outside the new limits, it moves inside at once (labelled timing).
- Every later camera command (`setCameraAt`, `panCameraTo`, `loadRoomWithEgo`, `actorFollowCamera`, walk following) clamps to the narrowed limits.

## Room 04 (decoded)

Outside a cutscene, the entry (+0x0013–0x003F) starts local 204 or 205 by the previous room. `var[0x070]` holds that room; the before-exit hook, script 015 +0x001A, stores `var[0x004]` there.

| Previous room | Local | roomScroll | Camera x | Limits after clamping |
| --- | --- | --- | --- | --- |
| 64 (`var[0x070]` = 0x40) | 204 | 152, 160 | 160 | 160..160, the left half only |
| others (except 70, which reads `var[0x0AC]`) | 205 | 480, 488 | 480 | 480..480, the right half only |

The game shows one half of Room 04 at a time; its objects 169/170 (verb 0x37) pan between them. The app's view opens on the half the entry picks, and panning to the other half is still possible there as exploration.

## Blockers

Compared with [the previous task](blockers-before.txt) ([after](blockers-after.txt)): no stop changed. Nine "presentation operation not applied: roomOps" notes are gone (roomScroll is applied now; other `roomOps` forms remain presentation). Two visits now carry "the camera moves 8 pixels a frame into its new limits and reaches x = … at once".
