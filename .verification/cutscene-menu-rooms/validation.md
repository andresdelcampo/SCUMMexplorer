# The cutscene menu lists the room's cutscenes (30 September 2026)

## The user's request (H, 29 Sep)

"Play a cutscene" should list only the cutscenes relevant to the current room.

## What the user now sees

- "Play a cutscene ▾" lists only the known cutscenes that start in the room shown or whose script loads it:
  - Rooms 01, 02, 53: the opening (script 044).
  - Rooms 03, 08, 09, 54, 67: the Elders' trial (script 046). Room 11: the trial and script 052.
  - Rooms 10, 13: script 052.
  - Rooms 36, 37, 38, 58: the firewood scene (script 086).
- In any other room (or with no room shown, or outside LOOM) the menu shows one disabled line: "No known cutscene plays in Room NN. Known cutscenes: the opening (Rooms 01, 02, 53); script 052 (Rooms 10, 11, 13); script 046 (Rooms 03, 08, 09, 11, 54, 67); script 086 (Rooms 36, 37, 38, 58)." So the user can go to one of those rooms. Game Texts' "Play this cutscene" is unchanged: it still offers every known cutscene from its texts.
- Playing an entry is unchanged (the same stated start, labelled in its tooltip).

## Where the room lists come from (decoded)

The start room is the known entry's room (the object whose verb starts it). The other rooms are the `loadRoom`/`loadRoomWithEgo` operands of the cutscene's script, from `.verification/fallback-evidence/code.tsv` ([room-loads.txt](room-loads.txt)):

- script 044: Rooms 01, 02, 53 (+0x00B9 … +0x0662).
- script 052: Rooms 11 (+0x0045), 13 (+0x017D, loadRoomWithEgo after endCutscene).
- script 046: Rooms 09, 67, 54, 03, 08, 11.
- script 086: Rooms 58, 37, 38 (started from Room 37's local 200 after Room 36's doorway verb).

Rooms reached only through scripts the cutscene starts are not included. None of the global scripts they start by number (8, 17, 23, 30, 32, 33, 39, 40, 113, 114) has a `loadRoom`; scripts started through a variable (script 044's `local[0x001]`) were not followed.

## The change

`MainWindow.KnownCutscene` has a `Rooms` list and `Shows(room)`; `OpeningRooms` holds script 044's. `CutsceneMenu_Click` opens `BuildCutsceneMenu()`, which filters by `_currentRoomResource` and adds the disabled note when nothing matches (`KnownCutsceneRoomsText`). The button's tooltip says it lists the room's cutscenes.

## Evidence

- New CutsceneMenuCheck (`.verification/cutscene-menu-rooms/CutsceneMenuCheck`, `<Games\loom> <output folder>`, about 30 s). Real MainWindow, interactive mode; picks Rooms 01, 02, 53, 08, 09, 03, 10, 11, 13, 36, 37, 38, 20 and 41 in the tree and builds the menu as the button does. Every room lists exactly the expected entries; Rooms 20 and 41 show the single disabled note. Then the Room 37 entry is clicked (MenuItem.Click raised) and the firewood scene plays (status "Playing Script 086 …", playback running in Room 37 3 s later). 0 failures ([cutscene-menu-check.txt](cutscene-menu-check.txt)). No physical input.
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests 244/244 (App-only change).
- Not rerun: the playback harnesses (the entries and `PlayKnownCutscene` are unchanged; they read `KnownCutscenes` by property name), RoomAudit, Blockers, DoorwayClickCheck (no room-click or room-load change).
