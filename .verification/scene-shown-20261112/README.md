# Clearing the scene lists (12 November 2026, roadmap step 4, R-8.42)

Two changes, both generic.

## Blank prints are not lines

Some games print a talk line of blanks every frame while a script waits: Indy 4 Script 139 +0x006D, Monkey Island 1 Script 24 +0x0069, Monkey Island 2 Script 23 +0x0075 (`Measure` with `MEASURE_LINES=room,object,verb` lists what a scene printed). A scene run counted each as a line and could not end while one was up, so such scenes ran to the 6,000-frame limit with thousands of "lines" (Indy 4 Room 88's exits: 5,982), and those counts went into the setup search's score.

- `ScummMessage.ShowsText`: a message shows text when one of its lines has a character other than a blank.
- `ScummCutsceneRun`: `Lines` counts messages that show text; a talk line of blanks counts as quiet when deciding that a scene is over.

Effect (`../scene-setup-20261031/*-run76.txt` against `*-run75.txt`): Indy 4's lines over its 520 entries 267,161 → 15,974, Monkey Island 1 32,106 → 2,522 (floppy), Monkey Island 2 16,363 → 4,044; Indy 3, LOOM, Maniac Mansion and Zak McKracken lose only a few (687 → 669 in Indy 3). Two plans changed: Monkey Island 1 Room 27's loose board (now ends after 311 frames in Rooms 25 and 6, not at the limit) and Zak McKracken Room 17's door (290 frames, Rooms 18 and 0). No other plan changed its way, rooms or kind.

## Normal leaves out what shows nothing or shows wrong

`ScummCutscenePlanner.NotOfferedReason(trigger)` (Core, every game): once a scene's plan is known, Normal mode does not offer it when

- no start begins it;
- it is started at its cutscene and shows no line, no other room and no animation;
- the picture checks name a suspect (`ScummScenePictureCheck`).

Until its plan is known a scene is offered as before (a pick works its plan out on the spot); the plans are derived in the background and kept between launches (`ScenePlanCache`).

A room's Play Cutscene list (the play games' `PlayCutsceneItems`, LOOM's scanned editions' `CdCutsceneItems`) leaves such a scene out in Normal; Advanced keeps it with " · not in Normal" after the title and the reason first in its note. The whole-game picker (Advanced only) keeps every entry. LOOM English's Normal lists play its reviewed catalog and are unchanged.

Entries that are not scenes were already out of the room lists: since 9 November a room lists only what its run shows to be a cutscene (it changes room or animates); refusals and reactions are Interact's. With blank prints no longer counted, Indy 4 Room 88's canal entries print one line each and stay reactions (Interact's); its three hallway exits walk to Rooms 86 and 87 and stay listed.

## Results, scenes in the room lists (`*-run76.txt`, `normal:` lines)

| Game | Scenes | Offered in Normal | Not offered |
|---|---|---|---|
| Indy 3 EGA | 81 | 79 (97.5%) | 2 (2.5%) |
| Indy 3 256 colours | 81 | 79 (97.5%) | 2 (2.5%) |
| LOOM EGA English (the shared engine's scenes; its Normal lists play the catalog) | 169 | 168 (99.4%) | 1 (0.6%) |
| Maniac Mansion classic | 42 | 42 (100.0%) | 0 (0.0%) |
| Maniac Mansion enhanced | 40 | 40 (100.0%) | 0 (0.0%) |
| Monkey Island 1 floppy | 87 | 87 (100.0%) | 0 (0.0%) |
| Monkey Island 1 CD | 87 | 87 (100.0%) | 0 (0.0%) |
| Monkey Island 2 | 176 | 172 (97.7%) | 4 (2.3%) |
| Indy 4 | 304 | 289 (95.1%) | 15 (4.9%) |
| Zak McKracken classic | 128 | 105 (82.0%) | 23 (18.0%) |
| Zak McKracken enhanced | 132 | 109 (82.6%) | 23 (17.4%) |
| **Total** | 1,327 | 1,259 (94.9%) | 68 (5.1%) |

Run 76 was measured while the at-cutscene rule did not yet ask for "no animation"; it named three scenes that animate (Indy 4 Room 57's statue head and Room 65's closet door, Zak McKracken Room 8's object 205 in both copies), which are offered now. The Indy 4 and Zak rows above count them as offered; the other rows are as measured.

Not offered in the three games:

- Indy 3 (both copies): Room 2's package (nothing changes in 32 frames); Room 33's entry (Henry placed by the setup, in his own costume; probably right, and now out of Normal until a reviewed start or a closer check says so).
- LOOM: Room 2's "sky" (actor 3 moves 40 frames on its first frame); the same in the Spanish copy, whose Normal list is the scanned one (window check below).
- Maniac Mansion: none.

## Window checks

- `window-indy3/report.txt` (`WindowCheck "indy3 ega en" w:90 r:2 l la r:33 l la`): Room 2's Normal list is empty and its Advanced list has "Room 02 · Open package · not in Normal"; Room 33's Normal list has Open door and Walk to door, Advanced adds "Room 33 · Entry · not in Normal".
- `window-loom-es/report.txt` (`WindowCheck "loom ega es" w:150 RL:2`): Room 2's list has six entries, without "sky" (planned, a picture suspect).

`WindowCheck` has `la` (the room's list as Advanced shows it) beside `l`.

## Left for step 4

(b) Reviewed starts for the scenes the user names as ones that matter (R-8.42): waiting for the user's list per game.

Tests: `SceneShownTests` (a print of blanks is no line; Indy 3 Room 2's package is not offered once planned, Room 1's door is) and the full suite.
