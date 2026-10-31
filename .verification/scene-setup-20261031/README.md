# Picked scenes in every game: the scene setup, first on Indy 3 (31 October 2026)

| | |
|---|---|
| Requirement | REQUIREMENTS R-1.14, R-1.9 (exception), §8.6 R-8.35 to R-8.39 (the user's scope of 31 October: explore everything; the game's rules never forbid a pick) |
| Fixture | `D:\Juegos\Lucas\indy3 ega en` |
| Tool | `Measure/` (`dotnet run -c Release -- <game folder> [max groups] [room]`; `MEASURE_TRIALS=1` prints each candidate's trial, `MEASURE_DIRECT=1` compares with a bare direct run) |
| Runs | `indy3-ega-run1.txt` (setup, before the at-cutscene start), `indy3-ega-run2.txt` (final) |

## What changed (engine layer, `Scumm/Session`)

1. **`ScummSceneSetupSearch`**: LOOM's readiness search (`LoomCutsceneReadiness`) for every game. It reads the trigger's own scripts (the scan's labels) for what their tests compare: a bit (tried at 1); a story variable (from `var[0x040]` up, not written by the same code) compared with a constant; an object's state (`ifState`/`ifNotState`, both values, since ScummVM jumps when the test is false; `getObjectState` into a variable then compared); an object asked for its owner (given to the hero); the second object of a two-object verb (`local[0x000]` compared with an object id in the verb's code); an actor's costume or room (`getActorCostume`/`getActorRoom` then compared). Each candidate (at most 16) is tried in a fresh run, one at a time, and kept when the scene then begins or goes further (rooms, lines); two passes.
2. **`ScummSession.ApplySceneSetup`**: sets those values on a session, only where it lacks them (a bit only raised, a variable only while 0, an object already held stays held). Old v3 packs bit n into bit (n & 15) of word n >> 4; v4/v5 keep separate bit keys.
3. **`ScummSession.PickInteraction`**: a picked object verb goes to the game's own sentence script (`VAR_SENTENCE_SCRIPT`, v3 to v5) as `doSentence` would queue it, with the hero put at the object's walk point (a teleport). The sentence script sets what the game sets (Indy 3's `var[0x007]`, the current object) and passes the second object.
4. **`ScummSession.StartSceneAt`**: the last way (R-8.35): the script holding the cutscene started at its `cutscene` instruction (global, local, object verb or entry code), the tests before it skipped, locals 0.
5. **`ScummCutsceneRun.RunPicked`** tries the game's way (entry or sentence script), then the verb run directly (`ActivateObjectVerb`, which the game's own refusals such as "too dark" or "I can't reach it" do not stop); **`ScummCutscenePlanner.PlanSetup`** adds the setup search, then the at-cutscene start. The result records the way (`ScummPickedStart`).
6. **App** (`MainWindow.PlayCutscenes.cs`): the background derivation works out the plan of every scene a fresh start does not begin; a pick applies the setup to the running session (re-entering the room when something was set) and starts the scene the plan's way.

## Result (Indy 3 EGA, 153 groups)

| How it begins | Groups |
|---|---|
| The game's own way (entry, or the sentence script) | 86 |
| The verb run directly (the game refuses the sentence) | 6 |
| With a setup | 12 |
| Started at its cutscene | 49 (one of them with a setup as well) |
| Nothing | 0 |

The fresh-start count before this work was 95 begun of 153 (`indy3-cutscenes-20261017`, since moved by later engine fixes); a bare direct run today begins the same groups as the picked run except Room 73's door (Close, Walk to), which only the sentence script begins (`MEASURE_DIRECT=1`: no group a direct run begins is lost).

Examples of the setup: Room 2's jar (object 71, verb 0x08) is used with object 133 (its code tests `local[0x000]` = 0x85); Room 7's table cloth needs the plant (object 131) in state 1; Room 39's hatch in state 1 plays 18 lines through Rooms 41, 42 and 8; Room 29's vault door needs object 519 held; Room 77's motorcycle `bit[0x5CF]`.

Of the 49 started at their cutscene, most print lines or animate (up to 11 lines, some go on to other rooms); about seven end within a few frames (their cutscene depends on locals the skipped tests would have set). These are exploration actions and say so in Advanced.

## Checks

- `ScummSceneSetupTests` (new): the jar's second object, the hatch's state and rooms, every Indy 3 group begins when picked, a setup raises only what the session lacks.
- Full suite: see CHANGELOG.
- Not done: a look in the real window (Play Cutscene in Indy 3 for one setup scene and one at-cutscene scene), and the other games (Monkey Island 1, 2, Indy 4 next).
