# Validation: messages (M1a) and the game's font (M1b), 28 September 2026

Status: **done in Core.** The app does not draw messages yet (M1c). `dotnet build SCUMMexplorer.slnx`: 0 warnings. **205/205 tests** pass: 198 before, plus 4 message tests and 3 charset tests. The old "message wait continues next frame" case was replaced by one where no message is shown.

## What changes on the screen

Nothing in the app yet: M1c draws the layer. What Core now produces, shown by [MessageRender](MessageRender/Program.cs) (the room at the camera, the verb screen left black, the message layer from `LoomMessageRenderer` over it, 3× scale):

- [room02-156-amanecer.png](room02-156-amanecer.png): Room 02 object 156 verb 0x36 run directly. "Amanecer." is drawn in charset 0, white with a black shadow (colour 0x8F), centred on x 160 at y 112, for 105 jiffies.
- [room02-156-doubleclick.png](room02-156-doubleclick.png): the same line through `Click(700, 40, doubleClick: true)` (scripts 005–007). Script 007 +0x001E also draws the object's name, "cielo", in the verb screen at (272, 181), under the close-up slot (`print 0xFE`, text code 6).
- [room02-154-hoja.png](room02-154-hoja.png): object 154 verb 0x36: a cutscene, actor 5 walks, 8 + 2 frames, then "Ultima hoja del año." (160 jiffies).

The logs next to each PNG give the message, its position, colour, delay and source.

## Evidence

- **Position.** "Amanecer." names no position. v3 keeps each print's position, height and colour as the text slot's defaults (ScummVM `decodeParseStringTextString`). The last print before it is the opening cutscene's empty `print 0x01, [center; position 0x00A0, var[0x0A6]; text ""]` (script 044 +0x06AA). Script 016 does the same on room entries with a height of 10. So (160, 112) and line height 10 are reached state, not a guess. After a catalog visit they are labelled estimates, because the prints on the route did not run.
- **Colour.** Actor 1's `talkColor` is 0x8F (script 017 +0x009B). For LOOM v3, `CharsetRendererV3::setColor` uses 0x80 for a shadow and the low 4 bits for the EGA colour.
- **Delay.** 60 jiffies (v3 has no `VAR_DEFAULT_TALK_DELAY`) plus `VAR_CHARINC` (5, script 001 +0x0012) per character. Each frame subtracts `VAR_TIMER_NEXT` (6) before the scripts run, and `stopTalk` runs in the frame that reaches 0. So "Amanecer." (105 jiffies) is on screen for 17 frames and gone in the 18th (test `TheSkysDescriptionIsATalkMessageWithTheGamesDelay`).
- **`waitFor message`** now breaks while `VAR_HAVE_MSG` is set. A synthetic "Hi" (70 jiffies) holds its script for 12 frames.
- **Charset.** `99.LFL` holds 240 glyphs, font height 8, a width table and 8 bytes per glyph. `getStringWidth` has no extra pixel in v3. The renderer ports `printChar`/`drawBits1`: shadow, outline, right-edge clipping and room/verb screen clipping.

## Side effects checked

- **Blockers** ([blockers-after.txt](blockers-after.txt)) against `../room-owned-loads/blockers-after.txt`: **5839 → 4979**. Every "presentation operation not applied: print" and "waiting for the message … continues on the next frame" label is gone (−860 in total). No new reasons appeared. Four "reached loadRoom after a frame advance" results fall outside Blockers' 8-frame window, because cutscene messages now last their delay.
- **Cutscene length.** With timed messages, the Elders' trial (script 046, Room 08's door) runs 2,120 frames, beyond the old continuation cap of 1000. `ContinueUntilRoomChanges` now defaults to `CutsceneFrameLimit` = 20,000 frames. Without that change, the trial stopped mid-way in Room 54 with input off, and later doorways were refused.
- **RoomAudit:** 0 gaps in 76 rooms; renders unchanged (`compare-png.ps1`: 0 changed). The report changes only in Room 02's pending scripts (6 → 4).
- **DoorwayClickCheck** (the room click path is timing-dependent): **75/75, 29 through the game's click and 46 through the labelled fallback**, the same as M1e, line for line ([doorway-clicks.txt](doorway-clicks.txt); run against the committed asynchronous room selector, 959e848).
  - Run only on the rooms whose results had changed (08, 09, 10, 15, 16, 17, 22, 23): **23/23**, 14 through the game's click ([doorway-clicks-changed-rooms.txt](doorway-clicks-changed-rooms.txt)). Room 08 object 254 plays the whole trial and ends in Room 09.
  - The first full run, with the old 1000-frame cap, is kept as [doorway-clicks-m1a.txt](doorway-clicks-m1a.txt): 74/75 (16 through the game's click). The trial stopped in Room 54 with input off, and later rooms refused the click.
- Other WPF harnesses do not touch messages and were not run. The app draws no text yet.

## Not modelled (labelled when met)

`overhead` text (0 sites), text codes 5 and 7 (a verb's name, a string), actors' names for code 6, codes 9–14 inside talk text, talk animations, and `putActor`/`putActorInRoom` stopping the talk of an actor who leaves the room.
