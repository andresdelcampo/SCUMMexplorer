# Cutscene layout: one room size, the verb screen kept, no arrows in cutscenes, dark disabled buttons (29 September 2026)

The user reported (29 Sep) that the screen changes size constantly during cutscenes, that the scroll arrows show during
cutscenes, and that disabled buttons (the playback bar's, the sound player's) show white. All three are presentation;
no game state changed.

## What the user now sees

- In the interactive mode, the room keeps one size and position while a cutscene plays, from room to room (the
  opening's Rooms 01, 02, 53; the Elders' trial). The verb screen (rows 144–199) is always under the room, black
  when no verb is on, instead of appearing only while a script prints there.
- The room and the verb screen now touch, as in the game; the scroll arrows sit under the verb screen's note.
- No scroll arrows while the player has no control (a cutscene or the boot). Their row keeps its space in the
  320-pixel view even in rooms that fit, so the room does not grow when they go.
- The controls under the room keep a minimum height (108 px: the playback bar, a two-line note and the music row),
  so the music row appearing in a room no longer shrinks the room.
- Disabled buttons are dark with dim text (every button in the main window now uses one dark template; colours set
  on a button still apply).
- The cost: at the same window size the room is drawn smaller than before in rooms that fit the view (the verb
  screen and the arrow row are always reserved). At 1400 x 900: 772 x 309 instead of 1082 x 433.

The bottom of the room: the view shows rows 0–127, as ScummVM's `initScreens(16, 144)` gives the main screen 128 rows
between the text line (rows 0–15) and the verb screen (checked again in `scumm.cpp`, 29 Sep). Nothing overlaps the
room (the check below measures it), so no change there.

## Changes

- `MainWindow.xaml.cs`: `VerbScreenShown()` is also true in the interactive LOOM mode; `UpdateVerbStrip` draws the
  session's verb slots there with the note "Verb screen (rows 144–199), kept under the room"; `SessionMode_Changed`
  refreshes it. `UpdateRoomViewportLayout` always reserves the pan row in the 320-pixel view;
  `UpdateRoomScrollButtons` hides the arrows (`Hidden`, keeping the row) when the room cannot scroll or
  `SessionInScene`; `UpdatePlaybackControls` (every playback frame) calls it.
- `MainWindow.xaml`: an implicit dark `Button` style; `VerbStripNote` has a fixed height (30); `RoomControlsPanel`
  `MinHeight="108"`; the pan row moved under the verb strip.

## Verification

- New `CutsceneLayoutCheck` (`<Games\loom> <output folder>`, about 2 minutes): plays the opening and the first 40 s
  of script 046 in the real MainWindow on the playback clock and samples the layout every 20 ms while the player
  has no control.
  - Before (HEAD 9653eb8, [cutscene-layout-before.txt](cutscene-layout-before.txt)): 4 room sizes/positions in the
    opening and 3 in the trial; the verb strip missing in 1,738 and 980 samples; the arrows shown in 983 and 1,244
    samples. 6 failures.
  - After ([cutscene-layout-after.txt](cutscene-layout-after.txt)): one size and position (772.4 x 308.9 at y 0.5)
    in both, the verb strip always shown, no arrows, nothing overlapping the room, and disabled Pause and Skip drawn
    at mean brightness 42 and 43 (limit 110). 0 failures.
- VerbAreaCheck: 0 failures ([verb-area-check-after.txt](verb-area-check-after.txt)) after updating two checks
  that expected the old behaviour (the strip hidden by default; the room shrinking when the toggle turns on), and the
  "off again" check's pan row, which is now always reserved.
- PresentationCheck (room-viewport): no failures ([presentation-geometry-after.txt](presentation-geometry-after.txt)).
- CameraPanCheck: 0 failures ([camera-pan-check-after.txt](camera-pan-check-after.txt)).
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. No Core change, so the unit tests, RoomAudit and the blockers were
  not rerun. Not run: DoorwayClickCheck (the change does not touch room clicks, doorways or room loads),
  OpeningPlaybackCheck, CutscenePlaybackCheck, FirewoodPlaybackCheck (the layout check plays the same cutscenes).

Images: [before-opening-room02.png](before-opening-room02.png), [after-opening-room53.png](after-opening-room53.png),
[after-trial-room08.png](after-trial-room08.png), [after-playback-bar-idle.png](after-playback-bar-idle.png)
(the VisualBrush capture draws the window's dark background as white).
