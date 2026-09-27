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

## Optional entry pan (presentation)

The user asked for the camera move to be playable, and optional. A new checkbox, **"Pan to the camera on entry"** (off by default), sits in the room view's options. When it is on, a room entered in the session opens its 320-px view at the left edge and slides to the camera: 8 source pixels per 75 ms tick (8 px is ScummVM `moveCamera`'s step; the tick is not the game's timing). A manual pan (buttons, keys, hold) or a new room stops it. The tooltip says what it is: a presentation effect. In the game, a room load, `loadRoomWithEgo` and `actorFollowCamera` put the camera there at once; only script pans and walks scroll. The full-panorama view is unaffected.

[`CameraPanCheck`](CameraPanCheck/Program.cs) (real MainWindow, synthetic calls): **6/6 PASS** ([report](pan-check/camera-pan-check.txt)).
- Pan off: Room 02 opens at source x 640 (camera 800), and Room 03 (camera 160) at 0.
- Pan on: Room 02 starts at 0, moves in 80 steps of at most 8 source px, and ends at 640.
- A manual pan stops it.

Captures: [off](pan-check/room-02-pan-off.png), [midway](pan-check/room-02-pan-midway.png) (over the sea), [end](pan-check/room-02-pan-end.png).

ScrollViewer applies an offset only on its next layout pass, so the last step never landed until the tick forced one (`RoomViewport.UpdateLayout()`). The harness found this: it ended at 632 instead of 640.

**PresentationCheck** (room viewport geometry): **39/39 PASS** ([report](presentation-check/geometry-check.txt)). Its pan-button checks assumed Room 04 opens at the left edge. Now the view opens at the camera, which Room 04's entry puts on its right half, so the harness scrolls to the left edge before those checks. No other assertion changed.

## Finding: Room 04 locks the camera to one half (decoded)

Room 04's entry (+0x0013–0x003F) runs only outside a cutscene (`bit[0xD6E]` = 0). It starts local 204 or 205, depending on the previous room (`var[0x070]`) and `var[0x0AC]`:

- Local 204 does `roomOps roomScroll 152, 160` and `setCameraAt 160`: the left half.
- Local 205 does `roomOps roomScroll 480, 488` and `setCameraAt 480`: the right half.

So a catalog visit from Room 02 opens on the right half (camera 480), and so does the Room 03 route. `roomScroll` (ScummVM `o5_roomOps` case 1: `VAR_CAMERA_MIN_X`/`MAX_X`) is not modelled yet. The session's camera range stays [160, width − 160], so a later pan or follow could leave the half the game allows.
