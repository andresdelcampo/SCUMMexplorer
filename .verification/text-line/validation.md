# Text where the game shows it: the text line above the room (29 September 2026)

The user's report (29 Sep): text could show where the game shows it, over the room or on the text line above it
(rows 0–15), not in the verb area. Checked against ScummVM and the decoded prints before changing anything.

## What the evidence says

- ScummVM v3 has three virtual screens (`initScreens(16, 144)`, `scumm.cpp`; `gfx.cpp`): the text line (rows 0–15),
  the room (16–143) and the verb screen (144–199). `displayDialog` puts a message at its slot's position
  (`_charset->_top = _string[0].ypos + _screenTop`; `_screenTop` is 0 before v7, and v3 has no overhead talk), and
  `CharsetRendererV3::printChar` draws each glyph into the screen that holds `_top` (`findVirtScreen`). The text and
  verb screens have one buffer, so a glyph is clipped at that screen's bottom (`drawBits1`: `y + drawTop < dest.h`).
- The Elders' trial prints at y 152 (script 046 +0x002F: `print 0x01, [center; position 0x00A0, 0x0098; ...]`; its
  later lines keep that position), so its lines are in the verb screen in the game too: the black band just under
  the picture. Most rooms' entries call script 016 with 0x10 (y 16: the room's first row). The opening's fairy text
  is at y 48 and Bobbin's cliff lines at y 152 (session probe).
- Script 016's argument is below 16 in two rooms: Room 08 (8) and Room 41 (0x0C). No `print` names a literal y below 16
  (the 439 `position` subops). So Room 41's lines printed with the default position (Bobbin's look lines, for example
  object 632 verb 0x36 "Es dificil enterarse de algo, con tanto jaleo.") start on the text line, and the app did
  not draw rows 0–15 at all: those lines were missing.
- Per ScummVM's clipping, a line at y 12 shows only its top 4 rows on the text line; the next line (y 22) is in the
  room. The app now draws it the same way. Reading: this looks odd in the game; worth a look in ScummVM's Room 41.

## What the user now sees

- In the interactive mode, the game's text line is kept above the room (16 rows at the room's scale, black), as the
  verb screen is kept below it, and text printed there shows in the game's font (Room 41's lines).
- The text line, the room and the verb screen now touch with no gap or rule, so they read as the game's one screen,
  and text at y 152 reads as the band under the picture (as in the game) rather than a separate panel.
- The room is drawn a little smaller at the same window size (16 more rows share the height): at 1400 x 900 the
  Room 41 view is 710.6 x 284.2.
- Unchanged: where each message is placed (game behaviour from the decoded prints). No Core change.
- Not drawn on the text line when charset 0 cannot be read (the UI-font fallback covers the room and verb screen only).

## Changes

- `MainWindow.xaml`: `TextLinePanel` (black border) with `TextLineMessageOverlay` above `RoomViewport`;
  the verb strip's border is named `VerbStripBorder`.
- `MainWindow.xaml.cs`: `TextLineShown()` (interactive LOOM mode with a room); `UpdateRoomViewportLayout` counts 16
  more rows, sizes the strip and removes the gap and rule above the verb screen when the text line is shown;
  `UpdateMessageLayer` draws rows 0–15 of the message layer there (`MessageTextLineBitmap`).

## Verification

- New TextLineCheck (`.verification/text-line/TextLineCheck`, `<Games\loom> <output folder>`, about 20 s): Room 41 in
  the interactive mode, object 632's verb 0x36 run directly. Result ([text-line-check.txt](text-line-check.txt)):
  0 failures. The strip sits directly above the room and is 16 rows at its scale; the verb screen starts where the
  room ends; the line at y 12 lights 509 pixels of the strip; the room keeps its size and position when the text
  appears. Pictures: [before](room41-before.png), [with the text](room41-look-632-text-line.png).
- CutsceneLayoutCheck: 0 failures ([cutscene-layout-check.txt](cutscene-layout-check.txt)); one size through the
  opening and the trial.
- VerbAreaCheck: 1 failure at first ("off again": its expected scale did not count the text line's 16 rows);
  the check now counts them when the strip is shown: 0 failures ([verb-area-check.txt](verb-area-check.txt)).
- PresentationCheck and CameraPanCheck: 0 failures.
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 230/230.
- Skipped (no change to clicks, room loads or playback): DoorwayClickCheck, the audit, Blockers, the playback checks.

## Follow-up: whole first lines (29 September)

The user's report: "when I select the sword, I only see a line of text that seems to be a continuation of another".
The user also confirmed the cutscene sound (AdLib music; the sword's effect is the PC speaker rendition, as labelled)
and that the conversations in the verb area (y 152, the game's position) look right.

### What the evidence says

- The sword is Room 41's object 634; verb 0x36 prints `print 0x01, [center; text, "Cuando termine de darle golpes,
  menuda\xFF\x01espada va a tener."]` with no position, so it takes text slot 0's default: y 12 (Room 41's entry,
  `startScript 0x10, [0x000C]`). Line 1 is at y 12 (text line), line 2 at y 20 (the room's row 4).
- ScummVM draws each glyph in the screen that holds `_top` and clips at that screen's bottom
  (`CharsetRendererV3::printChar`, `CharsetRendererPC::drawBits1`: `y + drawTop < dest.h`; the text screen is 16
  rows, `initScreens(16, 144)`). No LOOM-specific adjustment exists in `string.cpp`, `charset.cpp` or `gfx.cpp`. So
  in ScummVM line 1 keeps its top 4 rows, which the app copied: the user saw only line 2.
- Reading (not verified in the original interpreter): the designers set y 12 for the forge and would not ship
  half-hidden first lines, so the original likely drew line 1 whole across the boundary. The user chose to draw it
  whole.

### What the user now sees

- In Room 41 (and Room 08, the other room whose default y is below 16), a line that starts on the text line is drawn
  whole: its lower rows cover the room's top rows. The sword now reads "Cuando termine de darle golpes, menuda" /
  "espada va a tener." ([picture](room41-look-634-sword.png)).
- Unchanged: the room and verb screens still clip at their own bottoms, as in ScummVM; message positions.

### Changes

- `LoomCharset.cs` (`LoomMessageRenderer.PrintChar`): a glyph whose top is in the text screen is clipped at the
  room screen's bottom (row 143) instead of row 15. Labelled a reading in the code.
- `LoomCharsetTests`: the text-screen case now expects the line to reach past row 15.
- TextLineCheck: a new sword step (object 634 verb 0x36 run directly, an exploration action).

### Verification

- TextLineCheck: 0 failures ([text-line-check.txt](text-line-check.txt)); the sword's first line lights 742 pixels in
  the room's rows 0–3, where line 2 cannot reach.
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 230/230.
- Skipped: the audit (room gaps do not depend on messages), DoorwayClickCheck, Blockers, the playback and layout
  checks (no change to clicks, room loads, playback or layout).
