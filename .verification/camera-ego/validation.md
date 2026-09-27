# Validation: camera after loadRoomWithEgo and actorFollowCamera (27 September 2026)

Status: **done.** Builds with 0 warnings; **171/171 tests** pass (1 new: `LoomActorTests.TheCameraFollowsTheEgoAfterTheBootAndLoadRoomWithEgo`). Room audit: 0 gaps in 76 rooms, **0 changed renders**. DoorwayClickCheck: **75/75** ([report](doorway-clicks.txt)). Room03ActionCheck `162 4 167 38`: **46 PASS** ([captures](room03-action/)).

## ScummVM behaviour (checked against `script_v5.cpp` and `camera.cpp`, master)

- `o5_loadRoomWithEgo` (v3/v4): after `startScene` and the ego's placement, it sets `camera._cur.x = camera._dest.x = ego x` ("based on disassembly"). Then it calls `setCameraFollows(ego)` and, if x ≠ −1, `startWalkActor`.
- `setCameraFollows` (`o5_actorFollowCamera` → `actorFollowCamera`): follow mode. If the actor is outside the triggers (`t = x/8 − _screenStartStrip` below 10 or above 30, where `_screenStartStrip = camera/8 − 20`), it calls `setCameraAt(actor x)`. If the actor is in another room, it would load that room.
- `moveCamera` in follow mode: once the actor passes a trigger, the camera moves 8 px a frame until it reaches the actor's strip. `cameraMoved` keeps it half a screen from the room's edges. `VAR_CAMERA_POS_X` takes `camera._cur.x` on the next loop.

## What the session does

- **loadRoomWithEgo:** after the ego is placed, `var[0x002]` = the ego's x, clamped to [160, width − 160] as before. It is an estimate when the ego's position is one, and unknown when the ego or its position is unknown.
- **The ego's walk:** it ends at once (an estimate). If the end is outside the triggers, the camera takes the end x (clamped); otherwise it stays. Either way, the camera is labelled an estimate: "Camera x (var[0x002]) = N is an estimate: it follows the ego's walk from …".
- **actorFollowCamera** (executor; it was "presentation operation not applied"): the same trigger rule, with the actor table's position. The camera is an estimate after a seeded pick or an estimated actor position. It is unknown (labelled) when the actor's number or position is unknown, or when the actor is in another room.
- The camera's own 8-pixel strip rounding (`camera._cur.x &= 0xFFF8`) and pan timing are not modelled.
- **App:** a room entered in the session opens its 320-px view centred on `var[0x002]`, not at the left edge. The full-panorama view is unchanged. "Advanced room details" lists the camera x and whether it is an estimate. `LoomExplorationSession.CameraX`/`CameraEstimated` expose it.

## Results

`VisitProbe Games\loom 3/162/38 17` (the probe now prints the camera after each step):

| Step | Camera x |
| --- | --- |
| Boot (Room 02) | **800**, not an estimate. Script 001 +0x0233 `actorFollowCamera` on the ego at (828, 60); 828 is past the right trigger, and 800 is Room 02's limit (960 − 160). |
| Room 03 from the catalog | 160 (`startScene`) |
| Object 162 → Room 04 | **480**, estimate. Local 200 +0x0008 puts the ego at object 167's walk point, then walks it to (580, 132). Room 04 is 640 wide. |
| Room 17 from the catalog | 160 |

Scripts that read `var[0x002]` (from `code.tsv`): script 044 +0x0361, Room 02 object 153 verb 0x38, **Room 04 local 201 +0x0060 and objects 169/170 verb 0x37**, Room 24 local 204, Room 34 local 200. Room 04's readers are on the Room 03 → 04 route, so they now see the camera the arrival leaves (an estimate).

**Blockers** ([before](blockers-before.txt), [after](blockers-after.txt)): no stop was added or removed. The 149 "presentation operation not applied: actorFollowCamera" notes (the boot's and script 003's, the cutscene end hook) are now camera results. 72 read the ego as unknown after a catalog visit, so the camera is labelled unknown. There are also 38 "within the triggers" estimates, 22 walk-end estimates and 1 unknown walk end.

**Viewport captures** ([Room 02 after the boot](viewport/step-01-room-02.png), [Room 04 after object 162](room03-action/coordinate-action.png)): Room 02 opens on the cliff and the tree, where the ego stands; Room 04 opens on its right-hand section (scrollbar at the right end, right arrow disabled), where the ego arrives from Room 03. Catalog visits still open at the left edge ([Room 03](viewport/step-02-room-03.png)).
