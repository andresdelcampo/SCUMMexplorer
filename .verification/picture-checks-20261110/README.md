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
