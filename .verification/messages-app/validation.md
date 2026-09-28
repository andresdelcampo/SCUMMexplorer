# M1c/M1d: messages on screen, clicks that play forward (28 September 2026)

## What the user now sees

In the explore mode, a click plays forward. The app advances the session on a clock at the game's frame rate (`VAR_TIMER_NEXT` = 6 jiffies, 10 frames a second) and redraws every frame, so text appears and goes as it does in the game.

- A double click on the sky in Room 02 shows "Amanecer." in the game's font (charset 0, white with a black shadow, colour 0x8F), centred over the room, for about two seconds. It also shows "cielo", the object's name, in the verb screen under the room: script 007 +0x001E draws it with `print 0xFE` and text code 6. See [room02-156-amanecer-app.png](room02-156-amanecer-app.png), then [room02-156-after-talk-app.png](room02-156-after-talk-app.png) once the talk has ended. "cielo" stays, because a drawn string lasts until the room is redrawn.
- A click on talk text ends it on the next frame. This is ScummVM's '.' talk-stop key (`_talkDelay = 0`), labelled as assumed input in the status line.
- While the verb strip is off, the verb screen (rows 144–199) appears under the room only when a script has printed text there. Its note says so.
- If charset 0 cannot be read, a UI font draws the messages, and the objects note says it is not the game's font. This path was not exercised: charset 0 reads in this data.

## Game behaviour vs presentation

- **Game behaviour (ScummVM):** the message table from M1a (`print` → slots, `actorTalk`, `displayDialog`, the talk delay counted down by the frame's jiffies) and the charset drawing from M1b. Messages come only from prints the session reached.
- **Screen rows (ScummVM `gfx.cpp`, `initScreens(16, 144)`):** the text screen is rows 0–15, the room screen rows 16–143 and the verb screen rows 144–199. `printChar` draws at `_top - vs->topline`, so screen row y is room row y − 16, and x is relative to the camera's left edge (camera x − 160, clamped).
  - M1b's MessageRender placed room text without the 16-row offset. Its PNGs in `../messages/` therefore show text 16 rows too low. The tool is now corrected; its PNGs were not regenerated.
  - `LoomMessageRenderer` also clipped as if the room screen started at row 0. It now clips each screen at its own bottom. New test: `EachVirtualScreenClipsTextAtItsOwnBottom`.
- **Playback rule:** it continues while a talk message is on (`_haveMsg != 0`), or while scripts are pending, for up to 32 frames outside a cutscene. That is the old exit continuation's limit. Cutscene frames do not count, up to 20,000.
  - A room load reached while playing opens that room, and playback continues there. The clock waits while the view loads the room, so the view's `VisitRoom` cannot pull the session back.
  - Opening another room, Continue, a session reset and leaving the explore mode all stop it.
  - A click the game refuses while a scene plays (input off) is not sent and does not stop the scene.
  - Pointer hover frames are not added while the clock runs.
- **Doorways:** the direct-verb fallback runs when playback ends without a room load. For input the game refused, the frames run at once, so the fallback is not delayed. This is the same rule as before.
- **Timing (estimate):** "Amanecer." lasted 1,977 ms in the check. The game's value is 17 frames on screen and gone in the 18th, 1.7–1.8 s. The difference is WPF `DispatcherTimer` jitter; the frame count is the game's.

## Verification

- `dotnet build SCUMMexplorer.slnx`: 0 errors, 0 compiler warnings (only MSB3026 copy retries). A running `SCUMMexplorer.App` (not stopped) locked the App's output exe, so the harnesses were built into the scratchpad.
- Tests: **206/206** (205 plus the screen-clipping test).
- [MessageAppCheck](MessageAppCheck/Program.cs), new: real `MainWindow`, `HandleRoomPoint(700, 40, doubleClick: true)` and the text-skip handler, with the real clock. **0 failures.** Report: [message-app-check.txt](message-app-check.txt). It checks:
  - the text's top row (room row 96) and camera (x 800, text at room columns 770–829);
  - the talk duration, the stop, "cielo" in the verb screen;
  - the skip, and that a click away from the text does not skip.
- DoorwayClickCheck, all 75 doorways: **75/75** (29 through the game's click). The report is **identical line for line** to `../messages/doorway-clicks.txt` (M1a/M1e): [doorway-clicks.txt](doorway-clicks.txt).
  - This harness, Room03ActionCheck, KeyInputCheck and VerbAreaCheck set the private `_playInstantly` field, so a playback's frames run at once, as the synchronous continuation did.
- ClickInputCheck: **0 failures** ([click-input/click-input-check.txt](click-input/click-input-check.txt)). It uses the real clock. The double-click check now waits for the playback to end, because verb 0x36 runs on a later frame.
- Not rerun:
  - RoomAudit: no room or object render changed; the Core change is the message clipping only.
  - The other WPF harnesses (window, camera pan, verb area, key input, Room 03 actions): they do not test messages. CameraPanCheck and VerbAreaCheck are the plan's next task.
