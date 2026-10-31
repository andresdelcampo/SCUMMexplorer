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
