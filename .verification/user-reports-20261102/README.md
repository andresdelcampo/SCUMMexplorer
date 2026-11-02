# The user's reports of 2 November 2026

The user found these after a quick browse of the Indy 3 256-colour and Indy 4 copies, and asked for generic fixes ("pursuing all these one by one will
be a never ending story"). Each one was reproduced in the real window (`../scene-setup-20261031/WindowCheck`, `r:<room>` opens a room as the list does
and saves `room-<n>.png`), traced to an engine rule, and fixed in shared code for every game.

| Report | Cause | Fix (shared) |
|---|---|---|
| Indy 3 Room 80: the propeller's colour cycling also changes the verbs; Room 76 (logo) shows verbs | A picked room that does not settle into play is a still preview: none of its entry code runs, so its `roomOps setScreen` (Room 80: 8, 200, the whole screen) did not either, and the room stayed cut at row 144 above the verbs. Room 76 is shown only by the boot (Script 1 +0x01C2: setScreen 16, 152, before any verb exists) | A still preview takes its entry's first `setScreen` with constant rows (`TakeEntryScreen`). A room that only the boot script loads gets the screen and verbs it had when the opening showed it (`_bootShown`, `TakeBootShownState`; live and still previews) |
| Indy 3 Rooms 75 and 69: overlapping text, text in the wrong place | (1) `print` with no position goes on where the last one ended: ScummVM `drawString` ends with `_string[a].xpos = _charset->_str.right`, and `decodeParseString` keeps it as the default for versions 1 to 3 only ("needed for Indy3 (Grail Diary)"). We kept the old start. (2) The v3 text renderer clipped text at LOOM's screen (room rows 0–143), so a line at row 140 in a full-screen room was cut in half | (1) `ScummMessages.DrawString` moves the slot's x to the drawn text's right edge for versions 1–3; versions 4+ no longer carry a print's position into the next one. (2) `ScummV3MessageRenderer.Render` clips at the session's screen rows (`IScummTextCharset.RenderMessages(..., mainScreen)`); Maniac's and Zak's 16–151 screen also had this |
| Indy 3 Room 74: no airplane instructions | The page is blank until Script 90 (the plane's manual, Room 17 object 240) loads the room and prints the eight steps; Script 90 is the only script that loads Room 74 | A room that one global script (not the boot) alone loads, and that script loads no other room, is shown by starting that script (`ScummSession.ShownOnlyBy`, the app's catalog visit) |
| Indy 3 Room 83: Give Grail shows the bouncing head that stays in place through the ending | "Give Grail" (Room 82 object 873) enters Room 82, whose entry starts local script 200: a cutscene that beheads a soldier unless `bit[0x5E2]` is set (+0x0005). The game sets that bit (Script 122 +0x0233) when Indy comes back with the Grail; the setup never did, so the arrival played again (the user's reading) | While a picked scene that is not a room's arrival is set up (`ScummSession.ArrivalScenesSeen`, set in `ScummCutsceneRun.Run` and the app's pick), a room entered has its arrival cutscenes counted as seen: in a script the entry starts, a flag tested just inside its cutscene (and raised somewhere in the game), or a flag the script tests and then sets itself just before its cutscene begins, is raised first. A test that loops back (a wait for the flag: Monkey Island 1 Room 80's local script 202) or one before any cutscene that leads to another story state (Monkey Island 2 Room 50's `bit[0x05B]`) is left alone; the first version took those too and moved four Monkey Island scenes to their cutscene. Noted in the uncertainties |
| Indy 3 Room 13: the clothes-changing scene changes room | Script 1 (the boot) goes on after it hands over control: "I really have to get out of these wet clothes!" (+0x040D), a walk, `loadRoom 73` and the closet (`doSentence`). It kept running inside any room or scene picked after the boot, so Room 13's scenes ended in Room 73 | A picked room or scene (`VisitRoom`, `EnterRoomAgain`, a manual preview) ends the opening's leftover route (`EndOpeningRoute`; not while it holds a cutscene) |
| Indy 3: Play Game Intro stays disabled after the intro | The button waits for the game's cutscene to end, but the per-frame refresh updated only the scene controls when a cutscene began or ended | `RefreshPlayControlsOnFrame` also refreshes the intro button then |

Checked in the window after the fixes (`indy3vga-after3/`, `indy3vga-after2/`, `indy3vga-74/`): Room 80 fills the screen with no verbs; Room 76 shows the
logo alone; Room 75's right page reads "If ye would enter, / follow the first / on the right."; Room 69's lines are whole; Room 74 shows the eight
instructions; Give Grail plays with no head on the floor and no beheading.

## Measured

All scenes again after the fixes (`../scene-setup-20261031/*-run49.txt` against `*-run48.txt`; Maniac and Zak `*-run30.txt` against `*-run29.txt`;
LOOM `../loom-editions-20261031/run31-*.txt` against `run30-*.txt`, `EDITIONS_LOOM_SESSION=1`): the five copies and Maniac/Zak unchanged (no scene moved
either way); LOOM's shared engine plays a few more of its scenes its own way (EGA English 91.7% → 92.2%, CD 91.9% → 92.4%; at the cutscene unchanged).

## Left open

- **Indy 4 Room 14** (top-down tunnel): the catalog visit puts Indy in by an assumed walk-in from Room 94 (`loadRoomWithEgo`, object 1224), but the
  room's first state (object 165 state 0) is the mine-cart ride (local script 200, actor 9 in costume 188), which the game enters without the hero.
  Choosing an arrival that fits the room's state is the general fix; not done yet.
- **Indy 4 after Play Game Intro**: Indy walking very fast, and Room 04 showing no animations during the intro. Not reproduced headless (the intro runs
  at 6 jiffies a frame and ends in Room 4 with the player in control); needs the user's steps.

Tests: `UserReports20261102Tests` (the print continuation, the arrival flag, the room one script shows, the screens of Rooms 80 and 76, the opening's
route). Harness additions: `Measure` `MEASURE_SCREEN=<room>`, `MEASURE_INTRO=<frames>`, `MEASURE_ACTORS=<room>`; `CutsceneList <folder> groups <room|all>`;
`ScriptDump` reads any game (`entry`, `exit`, `room`, global ids).
