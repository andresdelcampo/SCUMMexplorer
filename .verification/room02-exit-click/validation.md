# Room 02's exit click: no doorway fallback after a change of scene (29 September 2026)

## What the user now sees

- After the boot, a single click on Room 02's exit (the path on the left, object 153) walks Bobbin right along the rock. At box 4, script 045's change of scene puts the smaller Bobbin (costume 2) on the path at (798, 118) and walks him to (756, 107). **The view then stays in Room 02** and waits, as the game does (ScummVM, seen 28 Sep). The status line says "Click sent to the game: the scene changed; click again to go on". Before this change, the app's doorway fallback opened Room 03 at once, so the user saw Bobbin turn small and then the scene jump. ([room02-exit-first-click-app.png](room02-exit-first-click-app.png))
- A second click on the same point walks the smaller Bobbin down the path, with the camera following, until box 10's change of scene puts him at (609, 110) in costume 3 and he waits again. ([room02-exit-second-click-app.png](room02-exit-second-click-app.png))
- A third click reaches the exit's walk point. Script 007 runs verb 0x38, its cutscene pans over the valley, holds for its `delay`, and Room 03 opens, all through the game's click with no fallback.
- The same holds in Rooms 15, 17 and 24, whose local 200 has the same change-of-scene watcher (script map, "Other rooms' changes of scene"): a click on a far exit first stops at the change of scene.
- A click that the game refuses with a cutscene still leads on through the labelled fallback. For example, Room 37's left exit (object 601, verb 0x37) prints "Mejor no vuelvo allí vestido de esta manera." without moving Bobbin, then the doorway's direct verb opens Room 36.

## The rule (no room-specific code)

When the game's click on a doorway reaches no room load, the app runs the doorway's direct verb (an exploration action, labelled), **unless the click changed the scene**: a cutscene began (`beginCutscene` reached, `LoomExplorationSession.CutscenesBegun`) **and** a script put the ego at a point (`putActor`, `putActorAtObject` or a room entry of the ego; `LoomActorTable.EgoPuts`, not counting `putActor()` re-placing it where it is). The app reads both counts before the input and when its playback ends (`MainWindow.FinishPlayback`).

- A cutscene alone is not enough. The first full DoorwayClickCheck run with "a cutscene began" as the rule failed Room 37 object 601: its verb 0x37 begins a cutscene on every click and never moves Bobbin, so the doorway could not be left (5 clicks, no room).
- A `putActor` alone is not enough either: every room entry puts the ego, and a load reached by the click is handled before this test.
- "Run the fallback only when the walk arrived and verb 0x38 was refused" was not used. Room 10 object 295 is untouchable (the click finds no object), and Room 04 object 177 has no verb 0x38 (script 007 finds none). Both would lose their navigation.

## Evidence

- Unit tests: 227/227 (`SCUMM_SOUND_TEST_ROOT=Games\loom`, `SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA`). `LoomClickInputTests.AClickOnRoom02sExitWalksTheEgoAndRunsTheSentence` now also checks that the first click begins one cutscene and puts the ego.
- New harness `Room02ExitClickCheck` ([room02-exit-click-check.txt](room02-exit-click-check.txt)), on the real playback clock (10 frames a second), **0 failures**: the first click stays in Room 02 with the smaller Bobbin at (756, 107), no fallback ran and the status says to click again (11.7 s of playback, the change of scene at 4.8 s). The second click stops at (609, 110). The third enters Room 03 through the game's click.
- DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10 ([doorway-clicks-5-rooms.txt](doorway-clicks-5-rooms.txt)): **13/13**, 11 through the game's click (10 before), 2 through the fallback (Room 04 object 177: no verb 0x38; Room 10 object 295: untouchable). The harness now clicks the same point again, up to 5 clicks, when the game held a click with a change of scene, and says so: Room 02 object 153 takes **3 clicks**.
- DoorwayClickCheck on all doorways ([doorway-clicks-all.txt](doorway-clicks-all.txt)): **75/75**, 37 through the game's click and 38 through the fallback. Eight doorways needed 2 or 3 clicks: Room 15 object 339, Room 17 objects 353 and 356, Room 22 object 401, Room 23 object 420, Room 24 object 430, Room 34 object 557 and Room 36 object 599. Some of them then fell back on a later click (for example Room 15 object 339: its verb 0x38 runs but loads nothing). In the full run Room 02 is opened after Room 01, so it is a catalog visit with Bobbin at the assumed arrival (568, 114); from there the click changes no scene and the fallback opens Room 03 (a debug run of Rooms 01 and 02 confirmed the start point). The last full run on record (M1c/M1d, 28 Sep) had 29 through the game's click and 46 through the fallback; routes and `var[0x071]` differ between runs, so the split is not strictly comparable.
- ActorDrawCheck ([actor-draw-check.txt](actor-draw-check.txt)): 0 failures (its clicks go on the floor first, so it is unchanged).
- Not rerun: RoomAudit and `compare-png.ps1` (no rendering or session state changed: two counters only), Blockers (no executor change), MessageAppCheck, ClickInputCheck, CameraPanCheck and VerbAreaCheck (they do not click doorways).

## Commands

```powershell
dotnet build SCUMMexplorer.slnx
dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build
dotnet build .verification\room02-exit-click\Room02ExitClickCheck\Room02ExitClickCheck.csproj -o <scratch>\rec
Start-Process <scratch>\rec\Room02ExitClickCheck.exe -ArgumentList '<repo>\Games\loom','<out folder>' -WorkingDirectory <repo> -Wait
dotnet build .verification\doorway-clicks\DoorwayClickCheck\DoorwayClickCheck.csproj -o <scratch>\dcc
Start-Process <scratch>\dcc\DoorwayClickCheck.exe -ArgumentList '<repo>\Games\loom','<out.txt>','2','3','4','8','10' -WorkingDirectory <repo> -Wait
```
