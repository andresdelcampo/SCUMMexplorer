# LOOM's room screen is 144 rows (29 September 2026)

## The user's report

"The bottom of the room graphics is still cut. Maybe the verb area overwrites part of it? Example, the egg is completely hidden in room 09." (Asked which issue to take next, the user reported this instead.)

## What the user now sees

- LOOM rooms show all 144 rows of their image, from the top of the game's screen down to the verb screen, with no text line above. The artwork in rows 128–143 is back (67 of 76 rooms have some there), including Room 18's doorway to Room 22 (object 368, box (168, 128) 80×16), which the pointer can reach again.
- Room 09's egg (actor 5, costume 0x34 frame 0x0C at (230, 132), Room 09 locals 218/219) is drawn in rows 123–134 (probe of the costume's cels). With the 128-row crop, only its top 5 rows showed; now it shows whole.
- Messages are drawn over the room at their screen position, as in the game: screen row y is room row y. The separate 16-row text line above the room is gone. Room 41's two-line messages at y 12 draw whole over the room's top rows. This is ScummVM behaviour now, not a labelled reading.
- Room text that the 28 September change had put 16 rows higher (for example "Amanecer." at y 112) is back at its screen y.
- The view's scale is fitted to 144 + 56 rows, so the room is slightly smaller in the same window.
- Indy3 keeps the 128-row view (ScummVM's default; its scripts were not checked for `setScreen`).

## Why (decoded + ScummVM)

- Decoded: script 001 +0x0000, the first instruction of LOOM's boot, is `roomOps setScreen, 0x0000, 0x0090`.
- ScummVM `o5_roomOps` (script_v5.cpp): for v3 (not PC Engine) the two parameters come before the sub-opcode, and case 3 (`SO_ROOM_SCREEN`) calls `initScreens(a, b)`, here `initScreens(0, 144)`.
- `initScreens(b, h)` (gfx.cpp): the text virtual screen gets `b` = 0 rows and the main screen starts at row 0 with `h − b` = 144 rows; the verb screen is rows 144–199. `redrawBGStrip` draws `_virtscr[kMainVirtScreen].h` = 144 rows of the room.
- The earlier model (29 Sep crop, 28 Sep text offset, the text line) used ScummVM's default `initScreens(16, 144)` from `resetScumm` and missed the boot's call. The unexplained reading in `LoomMessageRenderer.PrintChar` (a line at y 12 running on past row 15) is explained by this: there is no text screen to clip it.
- The 28 September note that Room 02's actor 4 (costume 141 at (433, 129)) is "in the 16 bottom rows the game does not show" was an inference from the 128-row model, not an observation. It is drawn again.

## The change

- `LoomMessageRenderer.RoomScreenTop` is 0 (was 16); `PrintChar` picks the room or the verb screen and clips at its bottom (the text-line exception is removed).
- `MainWindow`: `RoomScreenRows` is 144 for LOOM (128 otherwise); the text line (`TextLinePanel`, `TextLineMessageOverlay`, `MessageTextLineBitmap`, `TextLineShown`) is removed; `OneGameScreen()` keeps the gap-free room and verb screens in the interactive LOOM mode; `MessageRoomBitmap` maps screen rows 0–143 to room rows 0–143.
- Harnesses updated: TextLineCheck (now checks the 144-row view and text over the room's top rows), VerbAreaCheck and PresentationCheck (144 rows, no text line), MessageRender (comment).

## Evidence

- Build: 0 warnings. Tests 234/234 (`LoomCharsetTests.RoomAndVerbScreensClipTextAtTheirBottom` updated).
- TextLineCheck: 0 failures ([text-line-check.txt](text-line-check.txt)): the view is 319.8 px = 144 rows at its scale; the sword's lines light 1,183 and 368 pixels in rows 12–27; the look line at y 12 lights 1,292 pixels in rows 0–23; the room keeps its size when text appears. [room41-look-634-sword.png](room41-look-634-sword.png).
- VerbAreaCheck: 0 failures ([verb-area-check.txt](verb-area-check.txt)).
- PresentationCheck: 0 failures ([presentation-check.txt](presentation-check.txt)): the viewport shows 144 rows.
- Egg probe (temporary test, deleted): costume 0x34, frame 0x0C, directions 0–2: limbs 0 and 1 in rows 123–134, limb 2 in rows 101–127.
- Not rerun: RoomAudit and the room PNGs (room images are unchanged; the crop was in the app only), DoorwayClickCheck (clicks are given as room points; the room-to-screen mapping for clicks was already row for row), CutsceneLayoutCheck, CameraPanCheck, the playback checks, Blockers (no session change).
- Not compared with the running ScummVM.
