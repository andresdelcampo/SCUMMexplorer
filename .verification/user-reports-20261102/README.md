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

## Left open (first pass)

- **Indy 4 Room 14** (top-down tunnel): the catalog visit puts Indy in by an assumed walk-in from Room 94 (`loadRoomWithEgo`, object 1224), but the
  room's first state (object 165 state 0) is the mine-cart ride (local script 200, actor 9 in costume 188), which the game enters without the hero.
  Choosing an arrival that fits the room's state is the general fix; not done yet.
- **Indy 4 after Play Game Intro**: Indy walking very fast, and Room 04 showing no animations during the intro. Not reproduced headless (the intro runs
  at 6 jiffies a frame and ends in Room 4 with the player in control); needs the user's steps.

Tests: `UserReports20261102Tests` (the print continuation, the arrival flag, the room one script shows, the screens of Rooms 80 and 76, the opening's
route). Harness additions: `Measure` `MEASURE_SCREEN=<room>`, `MEASURE_INTRO=<frames>`, `MEASURE_ACTORS=<room>`; `CutsceneList <folder> groups <room|all>`;
`ScriptDump` reads any game (`entry`, `exit`, `room`, global ids).

## Second pass (2 November, after the user's retest)

The user's retest found Give Grail and Room 13 still wrong, and Indy 4's title behind a column; a crash log after switching games.

| Report | Cause | Fix (shared) |
|---|---|---|
| Give Grail still replays the arrival | The window's pick opens the scene's room first (`SelectRoomWithIntent`, `RoomOpenKind.Cutscene`), before the rule was on. And raising Room 82's `bit[0x5E2]` only skips the beheading: the rest of the arrival (local script 200: "Be back in a jiffy!", a walk into Room 83) still played | The rule now ends the arrival rather than raising its flag: after the entry of a room a picked (non-entry) scene enters, a room script holding a cutscene is ended and its cutscene closed (the game's cutscene-end hook runs; `CloseTopCutscene`, shared with the manual room's route cancelling). Kept: the scene's own scripts, scripts the setup's values were read in, and every arrival when the setup says how the room is entered (came from, door, pseudo room: Indy 4 Room 10's back door needs its arrival). The pick's opening visit gets the rule too (`_sceneOpening`) |
| Room 13's clothes change still moves to Room 73 | No decoded arrival brings Indy into Room 13, so he stays in Room 73 (where the boot ends); the game's cutscene-end hook (Script 23 +0x0022) runs `actorFollowCamera` on him, which loads his room | A picked room entry whose scripts speak as the hero (`printEgo`) brings the hero into the room when no arrival does (`WantHeroForScene`; not for rooms entered as pseudo rooms: Indy 4 Rooms 72 and 93 changed when he was) |
| The opening's tutorial lines ("Hi, I'm Indiana Jones. Welcome to my game.") inside picked scenes | Script 1 chains to Script 119 at its end; the chained script was not counted as the opening | A frame chained from the boot script counts as the opening (`ScriptFrame.ChainedFrom`); the opening's route is ended with its cutscene when it holds one |
| Indy 4: the title behind a column (Play Game Intro, Room 4) | The title is actors 5 and 6 with `ignoreBoxes`; ScummVM's `getMaskFromBox` gives no box plane 1 only in version 3 (bug #791 workaround); version 4 gives 0, version 5 box 0's mask. We gave plane 1 everywhere. `actorOps alwaysZClip`/`neverZClip` (`_forceClip`) was not modelled | `ScummActorRenderer` follows `prepareDrawActorCostume`: `_forceClip`, then the never-clip class, then the walk box's mask by version; `actorOps alwaysZClip n` / `neverZClip` set it, ignore/follow boxes clear it |
| Crash after opening Maniac Mansion Enhanced | `UpdateVerbStrip` drew the previous game's verb-screen text with no LOOM session | Guarded |

Checked in the window (`indy3vga-final-*`, `indy4-intro/`): Room 13's entry stays in Room 13 ("It's slippery out here!" … "What was that?"); Give Grail plays its ending in Room 82 ("Elsa, NO!!!"); Indy 4's title is in front of the room. Measured (`*-run50.txt` against `*-run49.txt`, Maniac/Zak `*-run31.txt`, LOOM `run32-*.txt`): the five copies and Maniac unchanged; Zak one more scene from a fresh start; LOOM back to its earlier figures (the flag rule's small gain is gone with it).

## Third pass: the hero's outfit (2 November)

Indy 3 Rooms 85 and 83 showed Indy in his college suit (the boot leaves costume 1). A room picked from the list, or its first-visit picture, now dresses
the hero in the outfit the game's own scripts give him most often (`actorOps` on actor 1 or VAR_EGO across global, room and object scripts; Indy 3:
costume 2, 14 times in the global scripts against the suit's 2), except in rooms the opening shows (the college keeps the suit). Versions 1 and 2 are
left out (their heroes are several kids). Only the catalog's previews: picked scenes keep the state their code tests (Room 13's entry plays only when
Indy is not in costume 2). Checked in the window (`r85-dressed2/`, `r83-dressed/`). The red blocks the user saw in Room 85 were the train of the
opening's logo, no longer shown since the opening's route ends on a pick.

Why rooms change size a second after they show (seen in `r83-dressed/`): while a picked room's live session is prepared, the window shows the still
picture in the room view (LOOM's view, room rows only, the panel's width); the play screen (`PlayScreenImage`, the whole 320 × 200 screen) then
replaces it, and the room takes less height. LOOM plays in the room view itself, so it never switches. Next: draw the waiting picture in the play
screen's frame.

Done: the first-visit picture is drawn as a play frame (`ScummPlayScreen.Still`: the rows the last frame had, the same width mode) and shown on the play
screen while the session is prepared, and kept there when the room becomes a still preview with no animated scripts (`size-jump/`: Room 83 ends in
the same frame as the live rooms).

## Fourth pass: the last three reports (2 November)

| Report | Cause | Fix (shared) |
|---|---|---|
| Maniac Mansion enhanced Room 44: no doorway to Room 1 | Its exit is object 258, a 16 × 128 strip along the left edge at 0, 0 (verb 0x0D: `loadRoomWithEgo 0x0101, 0x01`). The hotspot rule left out every object at 0, 0 (R-6.14, a LOOM rule for its draft previews); ScummVM's `findObject` has no such test | `RoomHotspotRules.IsHotspot` leaves out 0, 0 only when asked (`originIsOffScene`, LOOM only) |
| Indy 4 Room 14: Indy shown for a few seconds in the top-down tunnel | The catalog visit brings him in by an assumed walk-in from Room 94; the room's first state (object 165 at 0) begins a cutscene of the mine cart (local script 200, actor 9) that never refers to him | After an assumed arrival, when the room's entry begins a cutscene whose script never refers to the hero (no `printEgo`, no hero actor or VAR_EGO operand), he is taken out again (`TakeOutHeroNotInArrival`, labelled) |
| Indy 4 Play Game Intro: no sound of the window breaking | Room 4's local script 206 +0x00D0 plays it with `print 0xFD` whose text is four code-10 codes: ScummVM's `debugMessage` plays that sample from the speech bank as a sound effect (`talkSound(a, b, 1)`); we dropped text slot 2 | Slot 2 with a code-10 text records a speech-bank sound effect (`ScummSpeechEvent.IsEffect`); the app plays effects on a player of their own, beside the spoken line |

Checked in the window: Room 44 lists the doorway (`mme-44/`, `d` step); Room 14's picture has no Indy (`indy4-14-fix/`); the intro plays two effects, the window break at 78363548 (`indy4-intro-sound/`, `S` step). Indy 3 Room 83's grail scenes: "Give Grail" and "Look Cracked Seal" are object 873's and 864's verbs in Room 82, so they are in Room 82's Interact menu; they were listed under Room 83 only while the arrival bug walked Indy there. Henry being shot is Room 82's own arrival, its "Entry" scene.
