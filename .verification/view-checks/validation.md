# CameraPanCheck and VerbAreaCheck fixed (28 September 2026)

These two WPF checks had failed since the UI refresh (92c2e3c). One failure was an app bug; the other was a stale check.

## What the user now sees

- **The first room opened starts at the game's camera.** Before, the first room after the game opened (Room 02 after the boot, camera x 800) started at the left edge, x 0. The camera was applied before the room view could scroll, and was then dropped.
  - Now `ApplyPendingCamera` keeps the camera until the view can scroll, and `RoomViewport_ScrollChanged` applies it once the view's extent is known. Room 02 opens at source x 640, which is camera x 800 − 160.
  - This is presentation of the session's camera (`var[0x002]`); no game state changed.
- **VerbAreaCheck "off again" was stale.** It compared the room's height with the height it had at first open. The controls under the room grow after the clicks (action note, status), so the space the room gets shrinks, and the room correctly uses less of it.
  - Since M1c, the verb screen also stays under the room while scripts have text there. In this route that is script 018's distaff note letters and "cielo", which the click on object 156 drew. See [verb-area/room-02-verb-area-off-again.png](verb-area/room-02-verb-area-off-again.png).
  - The check now tests what should hold:
    - the verb slots' images are gone;
    - the verb screen shows only the game's text, with its note;
    - the room's scale is the layout's fit for the space it has.

## Finding (ScummVM `gfx.cpp`, reading)

Only v7 and later resize the main virtual screen to the room (`initBGBuffers`). In LOOM v3 the room screen stays rows 16–143, which is 128 rows. The room images are 144 rows (Room 02's header: 960 × 144), so their last 16 rows never appear in the game. The app shows all 144; in Room 02 those rows are black. This was not changed, and it is recorded in the script map as a follow-up.

## Verification

- `dotnet build SCUMMexplorer.slnx`: 0 errors, 0 compiler warnings. Tests: 206/206.
- CameraPanCheck (Rooms 02 and 04): **0 failures**, was 2. Report: [camera-pan/camera-pan-check.txt](camera-pan/camera-pan-check.txt).
- VerbAreaCheck: **0 failures**, was 1. Report: [verb-area/verb-area-check.txt](verb-area/verb-area-check.txt).
- DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10 (the camera change runs during room loading): **13/13** ([doorway-clicks-targeted.txt](doorway-clicks-targeted.txt)).
  - The results match the full run in `../messages-app/doorway-clicks.txt`.
  - One line differs only in `var[0x071]` (0xA7 against 0xFC): the targeted run reaches Room 08 by a different route. The outcome is the same, Room 07 through the game's click.
- Not rerun: the full DoorwayClickCheck (the doorway path did not change), WindowCheck, KeyInputCheck, Room03ActionCheck and MessageAppCheck. They do not test the camera or the verb strip.
