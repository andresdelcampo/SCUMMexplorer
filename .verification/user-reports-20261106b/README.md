# The user's second Indy 3 list of 6 November 2026

The user, after the tour of the same day: "it definitely looks more solid", then five reports, "think generic fixes; may be plenty more objects in many other rooms; same for all fixes". Each was reproduced in the real window and traced to a shared cause.

| Report | Cause | Fix (shared) |
|---|---|---|
| Scrolling in long rooms does not work any more | A room with no ambient script (still, 2 to 6 November: Rooms 6, 43, 62, …) had no session: only its first-visit picture, kept in the play frame so the room does not change size (`ShowWaitingPlayFrame`). The arrows need the game's camera. Live rooms scrolled | A still room is a session like any other (next row). Checked with the arrows' own mouse handlers (`m:`) and the keys (`p:`), still and live |
| Room 62 and others: no hotspots, not even doorways | The same: the overlays and lists read a session | `PrepareAmbientRoom(requireAmbient: false, picture:)`: the session stands as the first-visit picture shows the room (objects, colours, camera, every actor's pose: `ShowFirstVisitPicture`, `ScummActors.TakeLook`). Its entry code still does not run, input still does not reach the game |
| Room 93's Grail, Room 48's statues "are doorways"; "a clear definition" | Any verb of an object that could reach a room load made it a doorway | **R-6.13b: a doorway is an object you walk through**: the code for walking to it (its Walk to verb, or its default code outside any cutscene) can load a room. Everything else is a hotspot. The Walk to verb is read from the copy's own data; a table by game would be wrong (the Spanish Monkey Island CD numbers it 0x0A, the English 0x0B) |
| Room 4: the door does not show open or closed | Interact ran the verb directly, and Script 26 opens `var[0x007]`, which only the sentence script sets: it opened the object last used. Every shared door script in Indy 3, Monkey Island and Indy 4 reads it | `BindSentenceVariables`: a verb run directly gets the variables the game's own sentence script copies its verb and objects into, read from that script |
| Indy alone meets soldiers on the map and appears with his father | **The game's own scene.** Script 60 (a guard catches him): `notEqualZero bit[0x5CF]`, else chain to Script 85, which puts Henry in the room (+0x0051). `bit[0x5CF]` is set once Indy has met Henry (Room 21's entry, Script 72). So caught before meeting him, he is taken to him | No change. Checked against the scripts only, not the running game |

## The definition, measured

`Doorways/` (this folder): objects that can reach a room load, and those that are doorways now. The verb table of each copy is what the Walk to verb is read from.

| Copy | Walk to verb | Could load a room | Doorways | No longer |
|---|---|---:|---:|---:|
| Indy 3 EGA, 256 colours | 0x0A | 168 | 140 (83.3%) | 16.7%: statues, Grails, Room 18's objects, Rooms 51, 58, 77, 82 |
| Monkey Island 1 floppy, CD | 0x0A, 0x0B | 97 | 95 (97.9%) | 2.1% |
| Monkey Island 2 | 0x0B | 177 | 146 (82.5%) | 17.5%: Room 60's objects (verb 0x07, its own puzzle), Rooms 8, 9, 13, 27, 46 |
| Indy 4 | 0x0A | 200 | 156 (78.0%) | 22.0% |
| Maniac Mansion | 0x0D | 72 | 72 (100%) | 0% |
| Zak McKracken | 0x0D | 30 | 28 (93.3%) | 6.7% |
| LOOM EGA, CD | 0x38 | 75, 76 | 73, 74 (97.3%) | 2.7%: Room 4's look at the sky, Room 46's object |

Reading the first version's dropped objects found real exits among them: Monkey Island 2 Room 53's doors play a short cutscene of their own and load the room after `endCutscene`, so only a load inside a cutscene, or default code handing over to a script that holds one (Indy 3 Script 122), is left out. The rest of Monkey Island 2's and Indy 4's dropped objects were sampled, not read one by one (use and look verbs). A real exit reached only by a non-walk verb would now be a hotspot, still in Interact.

## The still rooms, measured

`Tour` with `TOUR_STILL=1` (`tour-*.txt`): each still room's session against its first-visit picture on the same rows and camera. Every room has a session, none refused.

| Copy | Still rooms | Differ at all |
|---|---:|---:|
| Indy 3 256 colours, EGA | 26, 27 | 30.8%, 29.6% |
| Monkey Island 1 floppy, CD | 22, 23 | 45.5%, 47.8% |
| Monkey Island 2 | 20 | 100% (24 pixels in most: the cursor, which the picture does not draw) |
| Indy 4 | 33 | 51.5% |

What differs beyond the cursor is what the room's ambient scripts draw from their first frame on: palette cycling (Indy 3 Room 80) and Monkey Island 1 Room 39's catacomb, which rebuilds itself at random. `Still/` compares one room's objects, actors and palette. Rooms 6 and 62 match to the pixel (test).

## Scenes, measured

`../scene-setup-20261031/*-run57.txt` against `*-run56.txt`. Binding the sentence variables moved Indy 4 only: Room 88's four canal objects no longer need starting at their cutscene (their code now knows its object). Room 76's jungle path tests `var[0x0D1]` against the active object, so its setup now sets that value to the object itself (`ScummSceneSetupSearch`). No scene moved the other way. Started at their cutscene: Indy 3 5.8%, Monkey Island 1 8.5% and 8.1%, Monkey Island 2 6.3%, Indy 4 3.1%.

## Checked in the window

`final/`, `after1/`, on `indy3 vga es`: Room 4 Open and Close (`after1/i-4-115-1.png`: open; `115=1`, then `115=0`), Room 62 with both doorways to Room 60 and its small objects (`after1/hotspots-62.png`), Room 48 (two tunnels and a door, three statue hotspots), Room 93, Room 86 (Grails as hotspots), scrolling in Rooms 1 (live), 6, 43, 86 (still).

## Tests

`UserReports20261106bTests`. The full suite on the final code: 1908 passed, none failed, 5 skipped (`full-suite-final.txt`). Harness additions: WindowCheck `p:` and `m:` (a scroll arrow held by key or mouse; the seconds are read in the machine's locale), `O:` (objects' states and the trace), `H` (hotspots and doorways, pictured Always); `Doorways/`, `Pick/`, `Still/`, `Tour`'s `TOUR_STILL=1`.
