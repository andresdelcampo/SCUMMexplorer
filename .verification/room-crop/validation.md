# The room view shows the game's 128 rows (29 September 2026)

## What the user now sees

- The room view is 128 rows high, as in the game, instead of the image's 144. The bottom band of each room goes: in Room 02 the lowest rocks and the sea's edge under row 128, and the 8 black rows below them. ([room02-128-rows-app.png](room02-128-rows-app.png): after the first exit click. Compare with [../room02-exit-click/room02-exit-first-click-app.png](../room02-exit-click/room02-exit-first-click-app.png), taken before the crop.)
- Room 02's actor 4 (costume 141 at (433, 129)) is no longer drawn: it stands in rows the game never shows. This matches the ScummVM comparison of 28 September (visible in the app, not in the game).
- With the same window, the room is drawn a little larger, because 128 rows share the height instead of 144.

## Why (ScummVM)

- `ScummEngine::resetScumm` calls `initScreens(16, 144)` for every version from 1 to 6 outside the C64 and NES ports, so the main virtual screen is rows 16–143 of the 200-row screen: **128 rows**. Only v7+ and HE 71+ use the full height (`scumm.cpp`, fetched 29 Sep).
- `redrawBGStrip` draws each room strip with the main virtual screen's height (`_gdi->drawBitmap(…, _virtscr[kMainVirtScreen].h, …)`, `gfx.cpp`), so rows 128–143 of a 144-row room image are never drawn. Actors are drawn into the same 128-row screen.

## Checking the other rooms first

The Known issue said Room 02's last 16 rows were black. That is only partly true: Room 02 has artwork down to about row 136 ([room02-image-row128-line.png](room02-image-row128-line.png): the image at 2×, with a red line at row 128). Of the 76 room renders in `.verification/room-audit` (all 144 rows high), **67** have non-black pixels in rows 128–143 (for example Room 16: 4,451 of 4,992 sampled pixels). The other 9 are black there: Rooms 61, 63, 65, 69 and 70, and the 8-pixel-wide Rooms 82–85. So the original artwork does extend below the game's screen, and the crop hides artwork the engine never draws. It does not hide anything the game shows.

- Doorway boxes: 75 doorways (the full DoorwayClickCheck list). 32 extend below row 128 and keep a visible part. One lies entirely below: **Room 18 object 368** (box (168, 128) 80×16, leads to Room 22). The game's pointer cannot reach it either. In the app, Room 22 stays reachable from Room 18 through the room's "Open Room 22" doorway button. DoorwayClickCheck gives the point to `HandleRoomPoint` directly, so its report does not change.

## The change

`MainWindow.UpdateRoomViewportLayout`: the viewport's height is min(room height, 128) × scale (`ShownRoomRows`, `RoomScreenRows = 128`), and the scale is fitted to those rows. The scene surface inside keeps the whole image, so the room picture, actor layer, message layer, walk-box and doorway overlays keep the room's coordinates. The viewport has no vertical scrolling, so it clips rows 128–143. The crop applies in the explore and the inspection mode alike, since the engine never draws those rows.

## Evidence

- Build: 0 warnings. Tests 227/227 (no Core change).
- PresentationCheck ([presentation-check.txt](presentation-check.txt)): 0 failures. Its "hidden horizontal scrollbar adds no viewport height" check now expects the viewport to show 128 of the 144 rows: 344.9 / 344.9 at scale 2.69 (the first run, before the update, failed as expected with 344.9 / 388.0 = 128 / 144).
- CameraPanCheck on Rooms 02, 03 and 04 ([camera-pan-check.txt](camera-pan-check.txt)): 0 failures.
- VerbAreaCheck ([verb-area-check.txt](verb-area-check.txt)): 1 failure, **the same as at HEAD before the crop** (a temporary worktree at 157dbf3 gave the same failure twice): "off again: the verb screen stays for the game's text [… cielo]". The verb screen stays when the strip is turned off, because a text is still there. This check had not been run since M3a; see the plan's Known issues. The crop's first run also failed "hover on object 156" once. Two reruns passed it, so that failure was intermittent.
- Room02ExitClickCheck ([room02-exit-click-check.txt](room02-exit-click-check.txt)): 0 failures after the crop.
- Not rerun: RoomAudit and `compare-png.ps1` (Core rendering is unchanged; the crop is in the app's view only), DoorwayClickCheck (clicks are given as room points, unchanged), MessageAppCheck and ClickInputCheck (no change to messages or clicks), Blockers.
