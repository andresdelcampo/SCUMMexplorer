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

## Your reports of 8 November (enhanced, Spanish), read

| Report | What the game's code does | Done |
|---|---|---|
| Room 04, push the loose brick: a sound, no picture change | Global script 137 opens the outer door, object 159 at x 472, off the right edge of the view, and script 56 closes it three seconds later (one kid holds the brick while another leaves). The picture changes, out of sight. Not the Indy 3 door bug (a state that did not draw, fixed 6 November) | Nothing changed; an option is offered (show an off-screen change) |
| Room 06 lists "Turn on the stereo (Room 20)", which plays only in Room 20 (and now neither room lists it) | The stereo's code can reach the death script (61), whose branches load other rooms; the list counted those possible loads | A room lists a scene only where its run goes, once it has run (`ScummCutscenePlanner.PlaysIn`, `HasRun`); the code's possible loads only before that. Room 20 offers it under Interact ("equipo de música · Encender"), so its Play Cutscene leaves it out (R-8.2b, user 31 October) |
| Room 08, turning on the circuit breakers does not light the room | Room 8 is dark from the boot until its own light switch is on (Script 1 `setBitVar 0x0B00, 8, 1`); the breakers cut and restore the power of rooms that are lit. Checked: switch on, breakers off (dark), breakers on (`var[0x00C]` 0 → 11, lit again) | Nothing to change |
| Room 24, the mummy in front of the curtain | Versions 1 and 2 keep each object's mask with its picture; we read only the rooms' masks, so a closed curtain hid nothing | Object masks read for version 2 (`GdiV2::prepareDrawBitmap`: the mask follows the picture) and version 1 (`GdiV1::drawStripV1Mask`: the third block of the object's cells names mask characters, inverted); every Maniac Mansion and Zak McKracken object |
| Room 41 has nothing to do | The publisher's office: the kids never enter it; its scene (Script 106, the publisher reads the mailed manuscript) is started from Room 1's exit script (Script 105), and the scan reads room entries and object verbs, not exit scripts | Open: listing scenes started by exit scripts (all games) |
| After an ending, rooms with nothing to do | After Room 30's explosion and the Edsel's launch, Rooms 1 and 9 keep their Interact choices and the kid (checked); the countdown's "Tiempo restante:" stays on screen | Open: which ending left rooms empty |
| Room 46, the Edsel launches with the garage door closed (the door is Room 16's, the launch's start) | The Edsel's Use tests only the trunk, but the Edsel (object 340, part of the background) lies behind the door's slats (objects 144 to 149): the player reaches it only after the door is opened (Script 14) | Scene order: a picked object under drawn, touchable objects gets what the one piece of code that takes them all away sets (`ScummSceneSetupSearch.UncoverState`); the launch starts with the door open. Also used by 1 Monkey Island 2 and 5 Indy 4 scenes (run 74), no way changed |

Checked in the window: Room 24 in both copies (Ted hidden behind the closed curtain); Rooms 7, 17, 20, 26, 9 unchanged (Room 20's kid stands behind the speaker, which is nearer the viewer). Tests: `UserReports20261107Tests.ARoomListsOnlyTheScenesWhoseRunGoesThere`, `OldObjectsCarryTheirDepthMasks`, `APickedObjectUnderOthersGetsWhatUncoversIt`. Full suite 1,924 passed (then the Edsel's test, passed). The Edsel checked in the window (`edsel/`: the door up, the Edsel in view, then the launch).
