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
- Real window (`WindowCheck/`, real `MainWindow`, private profile; the scene's Play action from the list): Room 39's hatch plays with object 586 set to state 1 and goes on to Room 41; Room 2's jar used with object 133 says "It seems to be working^" and "Hey! There's a key in here!" ([picture](window/2-71-8.png)); Room 13's brick, started at its cutscene, shows the castle wall without Indy and then loads Room 2 ([picture](window/13-164-8.png)): the at-cutscene start is a fallback of mixed quality.
- The window check found that a pick was ignored while another scene still played (the hatch's scene runs on for a minute); since 31 October a pick ends the running scene by opening the room again, as a manual room change does (R-8.5a). Maniac and Zak keep their startup rule.

## The other games (31 October, no new code)

| Game | Groups | The game's way or direct | Setup | At the cutscene | Of those, ending at once with no line |
|---|---|---|---|---|---|
| Indy 3 EGA | 153 | 92 | 12 | 49 | 10 |
| Monkey Island 1 floppy (`monkey1 ega en`) | 164 | 105 | 12 | 47 | 10 |
| Monkey Island 1 CD (`monkey1 vga en`) | 160 | 102 | 12 | 46 | 10 |
| Monkey Island 2 (`mi2 mt32 en`) | 190 | 100 | 7 | 83 | 24 |
| Indy 4 (`indy4 vga en`) | 426 | 227 | 30 | 169 | 35 |

Every group begins when picked in all five. The at-cutscene starts that end at once (their scene reads locals or state the skipped tests set) are the next quality step; runs: `*-run1.txt`.

## Interactions (31 October, R-8.38)

The Interact button (`InteractionOverview`, the object menu and the Advanced ACTIONS buttons) now lists every object of the room's data with each verb that has code, objects on screen first, the others after; before, only objects on screen were listed. A choice is played the way a picked scene is (`RunGenericRoomAction` → `PlayScummCutsceneAsync` with a one-trigger group): the room opened again when a scene still plays, the setup of §8.6 found and applied, the verb given to the sentence script with the hero put at the object, or run directly where the game refuses. It can be picked whenever a scene can (not only when the player has control).

| Window check (`WindowCheck`, real `MainWindow`) | Result |
|---|---|
| Indy 3 Room 2 "Look · junk mail" | "It's just junk mail." |
| Indy 3 Room 2 "Look · package" (its code tests `bit[0x5DF]`) | the setup sets it; "It's my father's Grail Diary!", Indy at the desk ([picture](window/i-2-68-9.png)) |
| Indy 3 Room 2 "Pick up · letters" (object 66, another state of the mail) | runs, no line (pick-ups print none) |
| Monkey Island 1 floppy, Room 28 "Walk to door" (Script 117: the door open and `bit[0x1BE]`) | "Meanwhile^", then LeChuck's ship (Room 70) |

Open: the second object of "Use X with Y" is given only where the verb's code compares it.

### Objects not in the room yet, and the hero brought to them (31 October)

`ScummSession.BringObjectIntoRoom`: for a picked interaction on an object that is not a hotspot now (`RoomHotspotRules`), the room becomes its owner again (unless the hero holds it), its untouchable class is cleared, a parent object is put in the state it needs, and a state of 0 becomes its first image when it has one. A present object is never changed (a closed door shown in state 0 stays closed). `ScummSession.PutHeroAtObject`: the hero is put in the room at the object's walk point when it is elsewhere, shown, and the camera follows it; `PickInteraction` uses it too.

| Check | Result |
|---|---|
| `ScummSceneSetupTests.APickedObjectThatIsNotInTheRoomYetIsBroughtIn` (Indy 3 Rooms 2, 13, 26, 39) | every absent object becomes a hotspot (Room 13: the trellis objects 163, 165; Room 26: 24 objects, one through its parent's state); a present one is left alone |
| Window, Indy 3 Room 13 (the castle wall, which Indy reaches only later; its entry leaves the room, so the room is a still picture), "Look · trellis" | before: no line (no hero in the room); after: Indy at the trellis, the camera on him, "I think it could hold my weight if I could get to it." ([picture](window/i-13-163-9.png)) |
| `Measure` Indy 3 again | unchanged: 153 begin, 10 started at their cutscene show nothing |

Seen on the way: Room 13's entry scene, started at its cutscene (it needs Script 52's argument, the outfit), jumps to Room 73: one of the at-cutscene starts to improve.

### LOOM's Interact (31 October)

LOOM keeps its own Interact (its verbs: Use/go, Look at, the drafts), now with the same rule: every object of the room with a LOOM verb is listed, those the pointer can pick now first, then those that are in the room only at other points of the story; a pick on one of those is brought in first (`BringObjectIntoRoom`). Picks during a scene stay closed (LOOM's playback works differently). `WindowCheck` (steps `r:`, `a`, `o`, `c:`): the overview of Rooms 2 and 17 is unchanged before and after; rooms with objects not present at the first visit: 8 (254), 10 (295), 18 (370, 371), 24 (433), 28 (465, 468, 469, 473 to 475), 34 (561, 587) of the 16 rooms looked at. Room 18 now offers "sphere" and "crystal 2": Look at the sphere plays the crystal-ball vision (Room 19, [picture](window-loom-after/c-5-0.png)); Look at crystal 2 plays in the glassmakers' room and goes on to Room 20 ([picture](window-loom-after/c-6-0.png)).
