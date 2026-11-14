# Indy 3's scenes that matter, played in the window for the user's check (14 November 2026)

Roadmap step 5 for Indy 3 (user, 14 November: "play Indy 3's scenes that matter in the window (including the Grail-trial click entries in Rooms
83–85) and prepare them for my check").

## How

`run.sh` (`bash run.sh <game folder> <output root> <scene>...`): one warm-up window fills the plan cache, then each scene plays in its own real
`MainWindow` (`WindowCheck`, three at a time, watched 60 seconds, pictures at 3, 6, 12, 18, 24 and 30 seconds; the intro every 3 seconds).
Indy 3 EGA English, 23 entries: `ega/<room>-<object>-<verb>/report.txt` and pictures; contact sheets `sheet-a.png` (whip, plug, trials),
`sheet-b.png` (Grail room, endings, plane, zeppelin, Berlin), `sheet-c.png` (intro, credits, game over, Marcus, Venice, Donovan). After the fix:
`ega-fix/`, `sheet-fix.png`. Pictures and reports stay local (ignored by git).

## What was seen

| Named (user, 12 November) | Entry (Play Cutscene unless said) | Seen in the window |
|---|---|---|
| Intro | Play Game Intro | the Lucasfilm Games logo, then the credits ✔ |
| Ending | Room 82 · Give Grail; Room 82 · Entry; Room 94 · Entry | Give Grail: Elsa's fall, "It could have been ours, Indy.", the way out ("He named the DOG Indiana!") ✔. Room 82 · Entry (its setup sets `bit[0x5E2]`, the Grail chosen): "Son, I'm proud of you.", "And what did you find, Dad?", then Room 94, the sunset ✔. Room 94: the final scores and the credits ✔ **after the fix below** |
| Game over | Room 36 · Entry | "After recovering consciousness, our hero… polish Hitler's ancient artifacts", the scores ✔ |
| Pulling with the whip | Room 13 · Use brick; Room 61 · Use wooden plug | Indy whips up the castle wall; the plug pulled, the water pours ✔ |
| Breath of God, passed | Room 83 · Go to the middle (a click) | "The penitent man is humble before God…", "Kneel!", "I'm through!", on to Room 84 ✔ |
| Breath of God, failed | Room 83 · Go to the right (a click) | "Kneel!", the blades ("CENSORED"), Room 82: "Be back in a jiffy!" ✔ |
| Word of God, failed | Room 84 · Go to the middle (centre) (a click; was Go to the far left, which moved Indy) | Indy jumps from his starting stone to the "J" beside it and falls, Room 82: "Be back in a jiffy!" ✔ |
| Word of God, passed | — | no entry: it needs six clicks, I-E-H-O-V-A, one stone each |
| Path of God, the leap | Room 85 · Go to the far right (a click) | Indy crosses the hidden bridge into Room 86 ✔ |
| Path of God, the fall | — | no entry: Room 85 local script 201 drops Indy only when he stands off the bridge line as the chasm is clicked (a walk, then a click) |
| Marcus calls Indy | Room 85 · Entry | "Indy!!! Hurry up! Your father is getting worse!!", "I know Marcus! I'm hurrying!" ✔ |
| The knight | Room 86 · Entry | "I knew you would come.", "Choose." ✔ |
| The false Grail | Room 86 · Use Holy Water | "I'd better make sure this is the right one.", Indy's face ages (Room 87), "He chose… poorly.", Room 82 ✔ |
| The true Grail | — | no entry of its own: Global script 122 +0x00DA compares the held cup with `var[0x0FD]` + 887, a number Script 119 draws at random (1 to 10); the setup gives Indy cup 888, so the run says "poorly". "You have chosen wisely" needs the cup the draw names (the open "outcomes by state" item) |
| The Grail diary's inscription | Room 91 · Look inscription | "These look like inscriptions.", "…is the correct account of the Grail." ✔ |
| Boarding the zeppelin | Room 37 · Walk to airfield | the airfield, the zeppelin, "Tickets, please." ✔ |
| On the zeppelin | Room 39 · Walk to hatch | the girders, then the fistfight ✔ |
| Leaving in the plane | Room 37 · Interact: Action button (the cockpit panel) | "Hold on, Dad! Here we go!", the biplane in the sky, "We're hit, Junior!!" ✔ |
| Going to Berlin | Room 77 · Turn on motorcycle | "Get in, Dad! We're going to Iskenderun!", "No, Junior. We're going to Berlin.", the border post (Room 72) ✔ |
| Arriving in Venice | Room 15 · Entry | "Ah, Venice!", "Dr. Jones?", Elsa ✔ |
| Meeting Donovan | Room 06 · Entry | "Dr. Jones, come with us, please.", Donovan's office (Room 9) ✔ |

23 of 23 entries played (100%). Of the 18 named outcomes, 15 have an entry and play (83.3%); 3 have none (the Word of God passed, the fall, the
true Grail), each needing more than one action or a random draw.

Not a defect: every report's "last printed" history begins with "I really have to get out of these wet clothes!": the boot's intro
(global script 1 +0x051A) runs hurried before the pick; the line never reaches the screen.

## Fixed: the ending credits piled up (shared code)

Room 94's credits showed every page on top of the last (`sheet-c.png`, row 94-0-0). Room 94 local script 200 prints each page with `print 0xFE`
(drawn into the room's front buffer) and clears it with `setState 946, 0` (object 946: x 0, y 192, 320 wide, 8 high). ScummVM
(`o5_setState` → `markObjectRectAsDirty`; `redrawBGAreas` → `redrawBGStrip`, read on GitHub master 14 November) marks every strip under the object's
width dirty and repaints each from the room picture at the main screen's full height, so drawn strings in those columns are erased. We did this
for `drawObject` (`ScummMessageTable.ObjectDrawn`, Indy 4's credits) but not for `setState`. Now `ScummBoundedScriptExecutor`'s `setState` calls
`ScummMessageTable.StripsRedrawn` (every game). Checked in the window (`ega-fix/94-0-0`: the scores, then each page alone). Test:
`ScummIndy4PlayTests.AStateSetRepaintsTheObjectsColumnsAndErasesDrawnStringsInThem`.

Measured on all eleven copies (`../scene-setup-20261031/*-run80.txt` against `*-run79.txt`, `MEASURE_SCRIPTS=1`, `MEASURE_TRIALS=1`): every scene's
result line and every Normal offer the same in each copy (0 of 1,128 changed); ways the same (`ways.py 80 79`). LOOM English's answer key
identical to run 79 (`loom-compare-run80.txt`: 238 of 238 begin, 236 reach the same rooms). Full suite: 1,944 passed, 5 skipped (11 min 53 s;
a first run produced no output after 5 minutes and was stopped after 1 h 26 min, cause not found; the rerun with `--blame-hang-timeout 4m` found no test over 4 minutes).

## For you to check

Open Indy 3 (EGA), pick from Play Cutscene: Room 83 · Go to the middle and · Go to the right; Room 84 · Go to the middle (centre); Room 85 · Go to the far
right and · Entry; Room 86 · Use Holy Water and · Action Plain, shallow bronze vessel (the true cup); Room 94 · Entry (the credits); Room 77 · Turn on motorcycle; Room 37 · Walk to airfield, and Room 37's
Interact · Action button (the plane). The first pick of a scene works out its plan ("Preparing the scene…"); later picks start at once.

## Your reports of 14 November, read

| Report | What the game's code does | Done |
|---|---|---|
| Room 37: only two Go actions; the button is partly hidden, there should be no verb area | The entry ends with `roomOps setScreen, 16, 192`: no room for the verbs (ScummVM `initScreens` puts the verb screen at 192 to 200). The room's first visit leaves it (local script 200, to Room 36), and the picture "as it was when last current" took Room 36's screen (16 to 144). Interact left out every switch and the button: their only code is verb 0xFF, the default entry `getVerbEntrypoint` falls back to for any verb, and Interact took only verbs below 250 | A room its scripts leave keeps its own screen rows (`ScummRoomCapture.MainScreen`, `ScummRoomScene`): the cockpit fills the view. Verb 0xFF is offered when it is an object's only player verb (`MainWindow.GenericRoomActions.cs`): Interact lists the handle, the knob, eight switches and the button. An Interact pick uses the scan's own trigger for the object and verb when there is one, so it gets Play Cutscene's setup: the button plays the take-off ("Hold on, Dad! Here we go!", Room 70) (`reports/r37b`, `r37c`, `r37e`) |
| Room 84: choose one of the first letters on the left instead of teleporting Indy | The input script, local 202 +0x0007, makes a stone of (x − 120) / 24 and y / 24, one step from Indy's (`var[0x064]`, `var[0x065]`; he starts at column 0, row 4, local 200). The room's only walk box is the ledge, so no candidate point fell on the stones, and the setup search made the one point fall by moving Indy (`var[0x064]` = 5) | An input script that reads the pointer itself gets a 3 × 3 grid of points as candidates (`ScummCutsceneIndex.FindClickScenes`); of points that play the same, the one with the fewest setup values is offered (`ScummCutscenePlanner.NotOfferedReason`). Room 84 offers "Go to the middle (centre)": Indy jumps from his own stone to the "J" beside it and falls, no setup (`ega-fix4/84-0-516`) |
| Room 82 · Give Grail: the characters stand on the left instead of next to the knight on the right | The receiver is actor 10. The plan's state (the hero in by door 861) replays the first arrival, Room 82 local script 200, where actor 10 is the beheaded soldier's head (costume 85, rolled off to the left), not the knight; the scene then runs only the direct way. The knight (costume 84, "Knight") comes in the ending, local script 201, which the entry starts for door 862 (global script 122 sends Indy there after "You have chosen wisely") and which puts him at x 597, on the right | A pick whose second is an actor in the room stands the hero beside that actor, as the sentence script walks him to a receiver (`ScummSession.PutHeroForPick`, `PutHeroBesideActor`; the game's way, the direct way and the window). **Open:** the ending's state needs several facts of the story at once: door 862, Henry already lying in Room 82 (local 201 +0x03AF waits for actor 2 to walk past x 320, and without him it stops before the knight comes) and the Grail held (local 201 +0x04D4: then Elsa does not take it). The setup search adds one value at a time and Henry's place is not among its candidates: this is the scene order work (a scene gets the state of the scenes before it, paused since 8 November). Tried and taken back: a score that prefers the game's own way (it chose Give Grail to Henry, 18 lines), re-entering the room before a direct pick (it stood Indy beside the head) |
| Room 86: an entry with the right Grail | Each cup (objects 888 to 897) has Look and Pick up; Use and Give run its fallback (0xFF), `startScript 0x7A, [8, var[0x007], local[0x000]]`, and global script 122 +0x00E9 compares the cup with `var[0x035]` = `var[0x0FD]` + 887, a cup Script 119 draws at random | The scan reads an object's fallback beside its other verbs (a trigger only when it reaches a scene), and an object whose code hands itself to a script that compares it with a variable is an entry of its own (`ScummCutscene.IsByObject`); of such objects that play the same, one is offered. Room 86 offers "Action Plain, shallow bronze vessel" (object 896, the cup the session's seeded draw names): "I'd better make sure this is the right one.", "You have chosen wisely.", "But remember this: the Grail cannot pass beyond the Great Seal…", Room 82 (`ega-fix5/86-896-255`). The false cup stays as "Action Olive wooden cup on silver tray" and Use Holy Water |

Measured (`../scene-setup-20261031/*-run82.txt` against run 80; LOOM, Maniac Mansion and Monkey Island 1 from run 81, the same code for them):
Indy 3 (both copies) one scene more that plays, Room 84's fall from Indy's own stone, no setup (Normal 86 of 103 → 87 of 112: the grid's points
that play the same or nothing are not offered); Monkey Island 2 five grid points that begin nothing (Normal 174 of 213 → 174 of 218); Indy 4 every
plan the same, the picture check names one more suspect (290 → 289 offered); LOOM, Maniac Mansion, Monkey Island 1 and Zak McKracken: every result
the same. Ways the same everywhere (Indy 3 Game 62 → 63, the new fall).

Second batch (`../scene-setup-20261031/*-run83.txt` against run 80): no result of run 80 changed in any copy; new entries from the fallbacks and the
objects of their own: Indy 3 +11 (Normal 86 of 103 → 89 of 122, the cups and Room 84's fall), Monkey Island 2 +16 (174 of 213 → 190 of 234),
Monkey Island 1 +4 in each copy (89 → 95 offered; two fallbacks started at their cutscene in each), Zak McKracken +1 each (offered the same);
LOOM, Maniac Mansion and Indy 4 the same (Indy 4's picture check one suspect more, as in run 82).
