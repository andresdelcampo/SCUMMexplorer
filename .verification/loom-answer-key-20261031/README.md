# LOOM English's catalog as the answer key for the shared scene engine (31 October 2026)

| | |
|---|---|
| Item | ROADMAP work item 2b, "one scene engine for every game"; REQUIREMENTS R-8.36 |
| Fixture | `D:\Juegos\Lucas\loom ega mt32 en` (the English Roland reference) |
| Answer key | `src/SCUMMexplorer.App/Loom/Ega/loom-replays.json`: 328 hand-reviewed replays (trigger, title, category, rooms reached, frames) |
| Tool | `Compare/` (`dotnet run -c Release -- <folder> <loom-replays.json> [max]`; `COMPARE_TRACE=room,object,verb` traces one picked run); runs `compare-run*.txt`, `categories.tsv` |

The shared engine (`ScummCutscenePlanner` on a `ScummGameSession`, the path Indy 3, Monkey Island and Indy 4 use) is run on LOOM English, and each catalog replay is matched to the same trigger (room, object, verb) in its scan.

## Results

| | First run | After the fixes |
|---|---|---|
| Replays whose trigger the scan lists | 238 of 328 | 238 |
| The other 90 | not found | all shared helpers: the draft reactions (verb 0x56 and others) whose only cutscene is in Script 41, reached by more than 20 triggers; the scan leaves those out by design (`SharedHelperTriggers`), they are Interact actions (R-8.2b) |
| Begin when picked | 238 (all the game's own way) | 238 |
| Rooms reached as the catalog recorded | 185 (51 more, 2 fewer) | **238** |
| Category as the catalog | 212 (static rule) | **219** (run-based rule) |

## Fixes to the shared engine

1. **The hero is put at the object on every path of a pick** (`ScummSession.PickInteraction`; not the direct-run fallback, where it cost Indy 3 three and Monkey Island 2 one scene begun the game's own way, so it was taken out again). LOOM has no sentence script, so its picks ran the verb directly with Bobbin left in Room 2, where the boot ended; LOOM's Script 3 +0x001B (`actorFollowCamera` of the hero) then started Room 2's scene (ScummVM `setCameraFollows`). All 51 "more rooms" were that extra Room 2. Versions 1 and 2 (Maniac Mansion, Zak McKracken) keep their own behaviour until they move onto the shared setup (their fidelity tests pin it).
2. **The category comes from the run** once it is known (`ScummCutscenePlanner.IsScene`): a scene when the run reaches another room or plays at least 100 frames without a line, dialogue and reactions otherwise; this is the catalog's own rule (R-8.9). Variants tried against the key: "every silent response is a scene" 199, adding "long runs are scenes" (200 to 600 frames) 205 to 208; the 100-frame threshold 219, the static rule 212. The 19 left are LOOM conventions (a silent Use is a scene) and hand judgements (Room 47's entry, 58 lines, is called a scene).

The catalog's titles are not compared: they use LOOM's verb and draft names (`LoomCutsceneVerbs`), which the generic engine does not have.

## Scene length (frames, the catalog less its 32 idle frames)

Within 5 frames 192, within 10% 9, within 50% 6, further 31: 201 of 238 (84%) play essentially the same scene. The 37 others play another branch or end differently: much shorter in the engine (Room 18's looks at 369 and 371: 84 and 154 frames against 536 and 567; Room 04's walk responses), much longer (Room 23's draft reactions at the workers: 41 lines against a short reaction). Likely the start state the catalog's replays had (Bobbin's look and staff, actors, camera); `frames.tsv` lists them.

### After the quiet-frames fix

Shared cause found by tracing Room 18's look at the bell (catalog 536 frames, engine 84): the scene is a chain of cutscenes one frame apart (Script 62, Room 20's local 207, Scripts 58 and 59), and the run stopped when the first ended. `ScummCutsceneRun` now ends a begun scene only after 32 frames with no cutscene and no line (the catalog's idle tail); versions 1 and 2 keep the first quiet frame (their fidelity tests pin it). Length within 10%: 201 → 212 of 238 (84.5% → 89.1%); rooms 100%, category 92.0% unchanged. Of the 20 still far off: Room 4's walk responses (3) are shorter because a pick teleports Bobbin (by design); Room 23's draft reactions (12) pick up about 40 lines of the workers' background talk; Rooms 30 to 32 and 51 (5) are single cases. Other games after the fix (showing nothing): Monkey Island 2 15 → 12, Indy 4 29 → 24; Monkey Island 1 one more scene with a setup.

## What retiring the catalog needs

1. The 37 length differences fixed in the shared engine (start state derived, not recorded).
2. LOOM's Interact offering the shared draft reactions ("Other draft response", verb 0x56: 61 of the 90 not listed as scenes), so nothing becomes unreachable.
3. Then the catalog shrinks to reviewed data the engine cannot derive: titles and draft names, the named sequences, Room 46's recipe.

## Next

- The named sequences (The Elders' trial, the firewood scene, Walk to Room 13, Room 46's recipe) and the catalog's start states (Bobbin's look and staff, the camera) are what the shared engine cannot produce yet; they are the reviewed data to keep.
- Run the same comparison for LOOM's other editions by matching each English replay to their scan by room, object name and verb.

## Tried and dropped: ending a scene apart from the room's background (1 November)

Room 23's draft reactions at the workers (12 of the 21 scenes more than 10% off) run about 640 frames longer than the catalog: the workers' talk (Room 23 local script 212) goes on in a cutscene of its own and keeps the run from its 32 quiet frames. Three rules for "background", each measured with `Compare` (`COMPARE_LOOM_SESSION=1`) against 217 of 238 within 10%:
1. A line from any script the scene's scan does not reach: 193; global helpers that print the scene's lines (Scripts 16 and 18) were counted as background.
2. Only the room's own local scripts the scan does not reach: 200; Rooms 8, 9, 28 and 41 lose lines of local scripts their scene starts (Room 41 object 633: 1,335 → 93 frames); Room 23 unchanged, its talk being inside a cutscene.
3. What runs before the pick (its frames' cutscenes and its lines): 213; Room 23 unchanged (the talk starts again after the pick), Room 35's draft reactions cut short (338 → 46 frames).
None kept. Telling the room's ambient talk from the scene needs the catalog's own end point or a reading of which script ends the reaction; until then Room 23's reactions stay long (their lines and rooms are right).

## 7 November: what the room does as the hero arrives plays first

Read again (`../ScriptDump room 23 201`, `202`, `212`; `../indy3-castle-20261030/SourceDump v:23:422:47`): the 40 lines were not background talk but the shepherds' **first meeting**. Room 23's entry starts local script 201 while object 422 still has class 24 (not met); 201 chains 202 once Bobbin is left of x 125 (+0x0006: ScummVM `o5_isGreater` jumps unless 125 > x), and 202 begins its cutscene and starts 212 ("Halt!", "Who goes there?" ...). In the game the walk to the shepherds (walk point x 121) always plays the meeting before any draft. The pick put Bobbin at x 121 and ran the draft at once; 201, frozen by the reaction's cutscene, then began the meeting after it.

Fix (shared, `ScummCutsceneRun.LetRoomMeetHero`): after the hero is put at the object, one frame lets the room react; a cutscene that a local script of the room begins, not one of the scene's own scripts, is played to its end first (as the walk would have), and the hero is put at the object again. A global script's cutscene (Indy 3's guards) is left to the scene. The planner's runs, the window's generic play and LOOM's play use it (not the direct way, which runs the verb where the hero stands). `Compare` has `COMPARE_TRACE_EGO=1` (the hero's place each frame of a trace); `lengths.py` lists the replays more than 10% off.

Measured (`compare-run10-loom-session.txt`, `lengths.py` against run 9 before the fix): within 10% **217 → 222 of 238 (91.2% → 93.3%)**; rooms 238, category 219, begin 238 unchanged. Room 23's Other draft response at the four shepherds and the 0x49 draft at object 423 now match. Left in Room 23: the draft 0x47 (four objects: 73 frames against 30) and the 0x49 draft (three: 141 against 123). The 0x47 catalog replay is the shorter one: its 30 frames are local script 229's cutscene alone (frames 0 to 30 in the trace), and local 232, which prints "That camouflage trick won't work on us" one frame later, was cut off by the replay's end at the first quiet frame. The engine's run is the whole reaction.

The other 9 still off: Room 4's walk responses (3) and Room 12's Use of object 322 (a walk to x -88, then Room 13: 77 frames against 126) start from the pick's teleport to the walk point (by design); Rooms 15, 18, 32, 33 and 51 are single cases.

### The 16 read: same code, another start

`COMPARE_ONE=room,object,verb` (the planner's run of one replay, replayed in a session kept for reading: its cutscene lines and what it printed) against each catalog replay's recorded boundaries. **All 16 run the catalog's cutscenes from the same instruction to the same `endCutscene` and reach its rooms**; none takes another branch. The catalog's replays ran the verb directly where Bobbin arrived ("catalog arrival assumed; direct object verb"); a pick puts him at the object first, as a click does in the game (the Use verb walks him there, CLAUDE.md). So:

| Replays | Cutscene (both) | Why the length differs |
|---|---|---|
| Room 4 objects 169, 170 (0x37), 176 (0x36); Room 12 object 322, Room 15 object 338, Room 32 object 537, Room 33 object 543 (Use) | the verb's own (+0x0000 or +0x0010 to its `endCutscene`), Room 4's local 202 | Bobbin walks on from where he stands (Room 12: to x -88); the pick starts him at the object, nearer: shorter |
| Room 18 object 374 (Look), Room 51 object 742 (Use) | Room 18 local 205 +0x0000 to +0x02FC; Script 102 +0x0000 to +0x00D8, then Room 66 | `walkActorToObject 0x01, 0x0174` (+0x002C), `walkActorTo 0x01, 160, 115` (+0x0020): from the object's walk point a longer walk (+90 and +18 frames) |
| Room 23 draft 0x47 (four shepherds) | local 229, then local 232 | the catalog ended at the frame between them (above) |
| Room 23 draft 0x49 (three shepherds) | local 210 +0x0000 to +0x024B | waits for the shepherds' idle scripts (`bit[0xD66]`, then `bit[0xD65]`, +0x000B, +0x0016), whose phase differs: +18 frames |

So nothing is left for the shared engine in the length comparison: within 10% 222 of 238, and the other 16 the same scene from another start. The next step of the work item is retiring the catalog to the reviewed data the engine cannot derive (titles and draft names, the named sequences, Room 46's recipe).
