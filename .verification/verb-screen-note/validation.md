# The verb screen's note row removed (user report D, 30 September 2026)

## What the user sees now

Under the room, the interactive LOOM mode shows only the game's verb screen (the 56 rows 144–199: the distaff, the close-up, the game's text). The line of explanatory text that sat under it ("Verb screen (rows 144–199), kept under the room: …", "Verb area: presentation of the session's verb slots …") is gone. The room and the verb screen are larger by the 36 pixels that row used to keep (Room 02 in the harness window: room 544 x 245 px, verb screen 544 x 95 px).

The explanation is still there: it is the verb screen's tooltip. Its first line is the old note's summary; the lines after it are what the old note's tooltip held (each drawn verb's image source and estimate, or the game's text on the verb screen with its source). Over a verb that is on, the verb's own tooltip ("a click goes to the game's input script as assumed input") shows instead, as before.

## Change

- `MainWindow.xaml`: the `VerbStripNote` text block under `VerbStripBorder` is removed.
- `MainWindow.xaml.cs`: `VerbStripNoteHeight` (36 px kept in `UpdateRoomViewportLayout`) is removed; `UpdateVerbStrip` sets the text through the new `SetVerbScreenNote(summary, details)`, which puts it on `VerbStripBorder.ToolTip`.
- `.verification/verb-area/VerbAreaCheck`: reads the summary from the tooltip's first line, checks that no `VerbStripNote` element exists, and computes the expected room scale without the 36 px.

No Core code changed; the room images, the session and the audit are unaffected.

## Checks

- `dotnet build SCUMMexplorer.slnx`: 0 errors.
- VerbAreaCheck: 0 failures ([verb-area-check.txt](verb-area-check.txt), PNGs in this folder). The "room fills its space" check now uses the layout without the note row.
- CutsceneLayoutCheck: 0 failures ([cutscene-layout-check.txt](cutscene-layout-check.txt)): the room keeps one size and position through the opening and 40 s of the trial (768 x 346 px in its window).
- Not run (unchanged by this task): unit tests (no Core change), PresentationCheck (does not read the note), DoorwayClickCheck, the playback checks.
