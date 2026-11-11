# Picture checks: scenes judged by what they show (10 and 11 November 2026, roadmap step 2)

A picked scene was judged by "a cutscene began, rooms, lines". `ScummScenePictureCheck` (`Core/Scumm/Session`) now watches each planned scene once more as its plan plays it (`ScummCutscenePlanner.PlanSetup`) and keeps what it finds with the plan (`ScummCutsceneRunResult.Picture`, saved with the plan). A finding is a **suspect**: something for a person to look at, not a fault. Advanced shows it in the scene's note ("Picture check (an estimate ...)"); `Measure` lists and counts it.

## The four checks

| Check | A scene is a suspect when |
|---|---|
| `CostumeFromBefore` | an actor shown in the last room (not the hero) wears a costume no script set during the run, set while another room was shown, and the game dresses that actor number in two or more costumes (`ActorsWithManyCostumes`: `actorOps n, [costume, c]` with numbers, over every script). |
| `OutsideWalkBoxes` | an actor that follows the boxes stands more than 4 pixels from every walk box of the last room. |
| `MovesOnFirstFrame` | an actor moved 4 or more frames in a row while on its costume's first (init) frame. |
| `NothingChanges` | from just before the scene was started to its 50th frame (or its end, if shorter) nothing changed: room, camera, lines, object states, each shown actor's place, facing, frame and limbs. |

## Results (run 3; `*-run3.txt`), scenes that begin

| Game | Scenes | Suspects | Costume from before | Outside boxes | Moves on first frame | Nothing changes |
|---|---|---|---|---|---|---|
| Indy 3 EGA | 159 | 14 (8.8%) | 13 (8.2%) | 0 (0.0%) | 0 (0.0%) | 1 (0.6%) |
| Indy 3 256 colours | 159 | 14 (8.8%) | 13 (8.2%) | 0 (0.0%) | 0 (0.0%) | 1 (0.6%) |
| LOOM EGA English (the shared engine's scenes on LOOM's session) | 192 | 3 (1.6%) | 3 (1.6%) | 0 (0.0%) | 1 (0.5%) | 0 (0.0%) |
| Maniac Mansion classic | 45 | 0 (0.0%) | 0 (0.0%) | 0 (0.0%) | 0 (0.0%) | 0 (0.0%) |
| Maniac Mansion enhanced (Spanish) | 43 | 0 (0.0%) | 0 (0.0%) | 0 (0.0%) | 0 (0.0%) | 0 (0.0%) |
| **Total** | 598 | 31 (5.2%) | 29 (4.8%) | 0 (0.0%) | 1 (0.2%) | 2 (0.3%) |

The plans themselves are unchanged by the checks (the `SETUP`/`ATCUT`/`STILL` lines of run 3 equal run 1's).

## The suspects

- **Indy 3** (both copies the same): actor 4 in costume 6 in Rooms 1 and 82 (Room 82 is the Grail temple: another room's character standing there); actor 2 in costume 7 in Rooms 21, 33, 35 and 77; actor 7 in costume 76 in Room 40. All three costumes were set by the boot (Room 0). Room 2's package (object 68, verb 0x01) shows nothing in its first 32 frames.
- **LOOM**: actors 10 and 11 in costume 5 in Room 2, set in Room 1 (probably the gulls, and then right); in "sky", actor 3 moves 40 frames on its first frame (a flying actor may have no walk frames).
- **Maniac Mansion**: none.

## What the checks do not cover

- Versions 1 and 2 (Maniac Mansion, Zak McKracken) write the costume instruction another way, so `CostumeFromBefore` finds nothing there by construction; Maniac's actors each keep one costume, which is why the first, wider rule flagged 24% of its scenes wrongly.
- An actor or costume given through a variable is not read by `ActorsWithManyCostumes`.
- LOOM English's Normal list plays the 328 reviewed replays, not these planned scenes; the replays are not watched.
- The user's two examples (11 November): the Grail knight's head was fixed before this work; the flying sheep happened once and was not seen again. Neither is a target.

## How the checks were narrowed (runs 1 and 2)

Run 1 (the literal rules) made 63 of 396 scenes suspects (15.9%): the hero one pixel outside a box edge (25 scenes, the slope test's own edge), Maniac's fixed costumes (11) and scenes that were over before a frame played, compared with a picture taken after they had started (6). Run 2 has the 4-pixel tolerance, the several-costumes rule and the picture taken before the scene starts. Run 3 is the same through the planner.

Run: `dotnet run -c Release --project .verification/scene-setup-20261031/Measure -- <game folder>` (`MEASURE_PICTURE=0` leaves the list out). Tests run: the scene tests (`CutsceneKindRules`, `ScummBootSnapshot`, `UserReports20261107`, scene setup).

## Step 3: the shared causes (11 November 2026)

Read first, for each suspect: does the game show it too? Indy 3's boot (Script 118 +0x0045, +0x005B, +0x00B4, then +0x00E8, +0x00F4) dresses actor 4 as Marcus, actor 2 as Henry and actor 7, and puts Marcus in Room 1 and actor 7 in Room 40; Room 82's entry (+0x00CD) and Room 35's (+0x00E1) put Marcus and Henry in themselves. So 12 of Indy 3's 13 costume suspects were the game's own doing, and the check was wrong, not the scenes.

| Cause | Fix | Where |
|---|---|---|
| The costume check took an actor the game's scripts dressed and placed for a leftover. | Only an actor a scene's **setup** put in the room can be a suspect (`ScummActorSnapshot.RoomBySetup`, set while `ApplySceneSetup` places actors). | `ScummScenePictureCheck`, `ScummActors` |
| The first value that begins a scene won over the value that is the scene. The search skipped every other value of a target once the scene had begun, and two "second objects" could be kept at once (the first one was used). Indy 3 Room 82, Give Grail: object 3 (+0x0019) begins a short branch, object 10 (+0x0000) the ending. | After its passes the search tries each kept value's other values in its place and keeps one only when the scene then scores higher; a sentence has one second object or actor. Done after the passes, so a plan never shows less than before. | `ScummSceneSetupSearch.Search` |

The audit's two other expected causes left no finding in the three games: no actor placed by a setup without its costume (0 of 396 after the check was put right, apart from Indy 3 Room 33's Henry, in his own costume), and no suspect among the scenes started at their cutscene. Nothing was changed for them. The picture checks are not part of the score: the search's trial runs are not watched (one watched run per kept plan), and no suspect left would be decided by it.

### Before and after, all games (`../scene-setup-20261031/*-run75.txt`; before: `*-run3.txt` here, `*-run74before.txt` there for the other games, the committed code built in a worktree)

| Game | Scenes that begin | Searched | Plans changed | Show more | Show less | Suspects before | Suspects after |
|---|---|---|---|---|---|---|---|
| Indy 3 EGA | 159 | 64 | 5 (7.8%) | 5 | 0 | 14 (8.8%) | 2 (1.3%) |
| Indy 3 256 colours | 159 | 64 | 5 (7.8%) | 5 | 0 | 14 (8.8%) | 2 (1.3%) |
| LOOM EGA English | 192 | 19 | 1 (5.3%) | 1 | 0 | 3 (1.6%) | 1 (0.5%) |
| Maniac Mansion classic | 45 | 34 | 2 (5.9%) | 2 | 0 | 0 (0.0%) | 0 (0.0%) |
| Maniac Mansion enhanced | 43 | 32 | 2 (6.2%) | 2 | 0 | 0 (0.0%) | 0 (0.0%) |
| Monkey Island 1 floppy | 172 | 80 | 1 (1.2%) | 1 | 0 | 2 (1.2%) | 0 (0.0%) |
| Monkey Island 1 CD | 168 | 79 | 1 (1.3%) | 1 | 0 | 2 (1.2%) | 0 (0.0%) |
| Monkey Island 2 | 255 | 97 | 3 (3.1%) | 3 | 0 | 4 (1.6%) | 4 (1.6%) |
| Indy 4 | 492 | 260 | 7 (2.7%) | 7 | 0 | 42 (8.5%) | 15 (3.0%) |
| Zak McKracken classic | 145 | 81 | 0 (0.0%) | 0 | 0 | 26 (17.9%) | 26 (17.9%) |
| Zak McKracken enhanced | 151 | 83 | 0 (0.0%) | 0 | 0 | 25 (16.6%) | 25 (16.6%) |
| **Total** | 1,981 | 893 | 27 (3.0%) | 27 | 0 | 132 (6.7%) | 75 (3.8%) |

"Searched" are the scenes a fresh start does not begin; "show more" is more lines, or more rooms with no fewer lines.

The plans that changed in the three games: Indy 3 Room 82 Give Grail (18 lines and Room 83 → 99 lines and Room 94, the ending); Indy 3 Room 23's four doors (1 → 5 lines, the room entered as pseudo room 154, not 140); LOOM Room 17's entry (2 → 11 lines, on to Room 56); Maniac Mansion Room 1, Give package (2 → 4 lines) and Give contract (4 → 77 lines, Rooms 0 and 42). LOOM English's reviewed catalog as the answer key is unchanged (`loom-compare-before.txt`, `loom-compare-after.txt`: found 238, begin 238, rooms same 237, more 1, both times).

### The suspects left in the three games

- Indy 3 Room 2, the package (object 68, verb 0x01): nothing changes in 32 frames. A scene that shows nothing: step 4.
- Indy 3 Room 33's entry: Henry (actor 2, costume 7) put in pseudo room 164 by the setup. His own costume; probably right.
- LOOM Room 2, "sky": actor 3 moves 40 frames on its first frame (a flying actor without walk frames; probably right).

Zak McKracken's 17% are almost all `NothingChanges` (24 of 26) and wait for step 7.

Tests: `SceneShownTests` (Give Grail reaches Room 94 with one second object; Marcus in Room 1 is no suspect) and the full suite.
