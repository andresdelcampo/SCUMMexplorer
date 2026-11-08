# Maniac Mansion, scenes ready (8 November 2026)

User, 8 November: "for scenes prioritize Maniac Mansion, then Indy 3 -for now. Don't go beyond until I can see the results ... unless it is a very simple change / generic that can be used in all games almost for free", then "I was expecting to check when at least a game was fully ready". Ready here means: every scene in Maniac Mansion's list plays in the real window, the game's own way where it can, from the state its code needs, with nothing on screen that the game would not show.

## How it was checked

`WindowCheck` plays every listed scene in its own window (`r:1` first: a room opened before a scene gives the assumed party, Syd and Bernard, as the app does; then the scene, watched 45 seconds, pictures at 4.5, 9 and 18 seconds). The script is `run-all.sh` (`bash run-all.sh <game folder> <output folder>`): a warm-up launch fills the plan cache (`warm/`), then each scene runs with a copy of it (`<room>-<object>-<verb>/report.txt` and pictures; `sheet-03.png`, `sheet-12.png` contact sheets). Runs: `classic/` (27 scenes, the list before the fixes), `classic-2/`, `classic-3/` and `enhanced-1/` (39 scenes each, after them). Headless: `Measure` runs 70 to 73 (`../scene-setup-20261031/`).

## What was wrong, and the fixes (all shared code)

| Found in the window | Cause | Fix |
|---|---|---|
| The microwave, picked early, did nothing | The first scene-order step had set the engine's second-object variable (`var[0x00A]`) | Only story variables come with earlier code (committed earlier, c1bfabd6) |
| The contract and the stereo ended with the microwave's death line ("Radioactive steam!") | Their real scenes (the Green Tentacle with the contract, Script 110; hearing a tape, Script 107) take control with `cursorCommand` (interface hidden), not `cutscene`; the scan found only the death script's cutscene (Script 61) behind them, and started it at its first branch | `ScummSceneMarks`: in versions 1 and 2 a `cursorCommand` that hides the whole interface starts a scene and one that shows it ends it (ScummVM `setUserState`); the scan, the setup search and the session's "scene begun" count use it |
| Giving things (contract, badge, film, package, cassette, hamster) played nothing or the wrong branch | The receiver of a Give is an actor (`var[0x00A]` = 14, the Green Tentacle), read as an object to hold; tests of it in the global scripts a verb starts were not read | `ScummSetupKind.SecondActor`: the actor stood next to the hero and passed as object B; tests of `var[0x00A]` in any script the verb reaches count |
| "Walk to stairs" showed nothing | Its scene (the delivery, Script 111) waits five minutes; the window hurried the scene's scripts before opening the room, and the room's own cutscene ended the hurry | The window hurries them again after the pick, as the headless run does |
| The wall safe (and other scenes that begin with an input screen) showed nothing | Its keypad (Script 44) was answered in the frame it opened | Each answer waits `InputShownFrames` (15) frames: the keypad shows, then the code is typed |
| A scene that begins but says nothing got no setup | Only scenes a fresh start does not begin were searched | A scene a fresh start begins without a line is searched too; a value is kept only when it plays more |

Not wrong, kept: Rooms 5, 8, 9, 15, 22 and 23 are dark. The boot script darkens them (Script 1 +0x02DE to +0x02F7: `setBitVar 0x0B00, room, 1`; Script 55 sets the light on entry), so a kid shows as a grey outline there until the light is switched on: the game's first-visit state. The piano first says "I have to turn off the Victrola first", turns it off by a sentence and plays (Script 100), the game's own branch when the Victrola plays. The microwave's steam is its own death scene (Room 7 object 85 +0x0073: radioactive water inside).

## Result

Maniac Mansion lists 39 scenes per copy (was 27: the scenes that hide the interface are found now). Every one begins; 33 of 39 (84.6%, was 77.8%) begin the game's own way, 6 are started at their cutscene (the keypad door of Room 4, Room 10's door, Room 12's entry, the wires, the developer tray, Room 30's ending), each playing its own moment. Both copies behave alike scene for scene (`classic-3/`, `enhanced-1/`; final build `classic-4/`, `enhanced-2/`). Three windows at once run slower than the game's own time: the stairs' delivery and Room 30's ending text then come after the 45 seconds watched, and were checked alone (both play: Script 87's delivery in Rooms 26 and 38; the explosion, its two lines and "EL JUEGO HA TERMINADO"). The pictures and reports stay local (ignored by git); this README, `run-all.sh` and the full suite's result are the record.

Full suite: 1,919 passed, one failed (the room-list titles test read "manuscript" as the word "script"; it now matches whole words and passes), 5 skipped.

Zak McKracken gains the same: 79 and 82 scenes (was 54), all begin. Indy 3, Monkey Island 1 and 2, Indy 4: unchanged ways (started at their cutscene 5.0%); the "says nothing" search gave more scenes state of their place in the story.

## For you to check

Open Maniac Mansion, open any room first (for example Room 1; the party is assumed), then pick scenes from Play Cutscene. Suggested: Room 01 Give contract (the Green Tentacle), Room 20 Turn on mondo stereo (the demo tape), Room 05 Give cassette tape, Room 04 Give badge, Room 09 Open wall safe (the keypad), Room 12 Walk to stairs (the delivery), Room 17 Use piano, Room 07 Open microwave oven.
