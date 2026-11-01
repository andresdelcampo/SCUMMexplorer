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

### Fewer scenes started at their cutscene (31 October, later)

Two changes to the setup search, both generic:
1. Object code that tests the state of a variable it does not set (`ifState var[0x007], 1` in Indy 3, `getObjectState var[0x000], var[0x007]` in Monkey Island 2: "this object", which the sentence script sets) now tries the picked object's own states. The second object of a two-object verb is offered even when no room holds it (an inventory-only item).
2. The setup is applied again after the room is visited (`ScummCutsceneRun`, and the app's pick): a room's entry can reset what the setup set (Indy 3 Room 23's entry closes its doors). Only what the session lacks is set.

| Game | With a setup | Started at their cutscene | Of those, showing nothing |
|---|---|---|---|
| Indy 3 EGA | 12 → 23 | 49 → 38 | 10 → 4 |
| Monkey Island 1 floppy | 12 → 18 | 47 → 41 | 10 → 6 |
| Monkey Island 1 CD | 12 → 18 | 46 → 40 | 10 → 6 |
| Monkey Island 2 | 7 → 17 | 83 → 73 | 24 → 15 |
| Indy 4 | 30 → 38 | 169 → 161 | 35 → 30 |

Every group still begins. `ScummSceneSetupTests.ADoorThatTestsItsOwnStateIsOpenedForItsClose`. Window: Indy 3 Room 23's "Push loose brick" and "Close door" play without breaking the room (short animations without lines; the picture shows Indy by the door afterwards). Runs: `*-run3.txt`, `indy3-ega-run5.txt`.

Then: when no single value makes a scene begin, all candidates are tried together (one value per target) before its cutscene is started directly. Indy 4: 38 → 42 with a setup, 30 → 29 showing nothing; Monkey Island 2 unchanged. What is left (Indy 3 4, Monkey Island 1 6, Monkey Island 2 15, Indy 4 29) has no common pattern: script arguments, values tested in other rooms' scripts, actor placements. Full suite 1842 passed. Runs: `*-run4.txt`.

### No duplicates between Interact and the room's cutscene list (31 October, R-8.2b)

A room's Play Cutscene list (LOOM and the shared session) leaves out every scene whose trigger is an action of this room's Interact (same object and verb, read from the entry id: `scumm-r-o-v`, `cd-r-o-v`, LOOM's `Room NN object O Verb 0xVV`); the Advanced whole-game picker still lists all. Interact offers a verb whenever its first instruction decodes (Indy 3's package Open, whose code has a jump target outside the resource, was missing). `WindowCheck` step `l`: LOOM Room 2 keeps the intro and the two "Other draft response" entries (Interact does not offer verb 0x56); Room 18 keeps its draft responses and the scenes played from Room 20; Indy 3 Room 2's list becomes empty, its package now offers Open in Interact.

### Class tests, inventory-only items, and the trigger's own code (31 October, later)

Three generic additions to the setup search: (1) `ifClassOfIs` candidates (an object's class set or cleared, both tried; ScummVM o5_ifClassOfIs: a class with bit 0x80 must be set, one without must be clear), applied by the new `ScummSetupKind.ObjectClass`; (2) an item held that no room holds (inventory-only) is offered without a room; (3) the trigger's own code is always read first, as the scan may list only the scripts it reaches (Indy 4's bread chains to local 208 after its own owner tests). Call-site arguments for a scene started at its cutscene were also added (no measurable change).

| Game | Scenes | Its own way | Started at its cutscene | Shows nothing |
|---|---|---|---|---|
| Indy 3 | 153 | 75.2% → 80.4% | 19.6% | 2.6% |
| Monkey Island 1 floppy | 164 | 75.6% → 84.8% | 15.2% | 3.7% → 3.0% |
| Monkey Island 1 CD | 160 | 75.6% → 85.6% | 14.4% | 3.8% → 2.5% |
| Monkey Island 2 | 190 | 61.6% → 81.6% | 18.4% | 6.3% → 2.1% |
| Indy 4 | 426 | 63.1% → 77.5% | 22.5% | 5.6% → 3.8% |
| **Total** | 1,093 | 68.3% → **80.9%** | 19.1% | 4.8% → **3.0%** |

LOOM English's answer key unchanged (rooms 100%, category 92.0%, length 89.1%). Full suite 1842 passed. Runs: `*-run10.txt`.

Then: both sides of a comparison are tried (an inequality also tries a value on its other side: Indy 4's closet door needs object 870's state to be non-zero). Indy 4 77.5% → 77.7% played its own way; the others unchanged. A cap of 24 candidates instead of 16 was tried and dropped: Indy 4 78.2%, but its derivation took 223 s against about 180. What is left showing nothing (Indy 3 4, Monkey Island 1 5 and 4, Monkey Island 2 4, Indy 4 16: 3.0% of all scenes) is a long tail of single cases, mostly room entries with many tested values (Indy 4 Rooms 10, 27, 72: 15 to 36 candidates). Runs: `*-run11.txt`, `*-run12.txt`.

## Arriving from another room; variables set to their tested value (31 October, later)

Indy 4 Room 10's entry branches on `var[0x0E0]` (+0x0058 to +0x01A1), the room the hero came from: every game's exit hook copies the room being left there (`move var[X] = VAR_ROOM`: Indy 4 Script 23 +0x0014; Monkey Island 1 and 2, Indy 3 Script 7). Two changes:

1. A **Variable** setup value is now set whenever the session's value differs (it was set only while 0, which a booted session never has for most story variables).
2. A new setup kind, **came from** (`ScummSetupKind.CameFrom`): a test of a variable a global script fills from `VAR_ROOM` against a room number is "the hero came from that room"; the setup visits that room first, so the game's own exit hook sets the variable as the scene's room is entered. In the app a room entry's setup now re-enters the room after it is applied (before, the room was opened first and its entry never saw the setup; the headless runs always applied it first).

Measured (`*-run14.txt` against `*-run13.txt`): own way (with or without setup) / started at the cutscene.

| Game | Scenes | Own way | At cutscene |
|---|---|---|---|
| Indy 3 | 153 | 80.4% → 80.4% | 19.6% → 19.6% |
| Monkey Island 1 floppy | 164 | 84.8% → 86.0% | 15.2% → 14.0% |
| Monkey Island 1 CD | 160 | 85.6% → 86.9% | 14.4% → 13.1% |
| Monkey Island 2 | 190 | 81.6% → 83.7% | 18.4% → 16.3% |
| Indy 4 | 426 | 77.9% → 81.2% | 22.1% → 18.8% |
| **Total** | 1,093 | **81.1% → 83.1%** | **18.9% → 16.9%** |

LOOM unchanged (`../loom-editions-20261031/run4-*.txt`: EGA 3.2%, CD 2.7% at the cutscene; answer key 238 of 238). Real window (`WindowCheck 10,0,0`, Indy 4): Room 10's entry, picked from the list, opens on the theatre street and its cutscene begins ([picture](window-camefrom/10-0-0.png)); before the app change it showed no cutscene. Test: `ScummSceneSetupTests.ARoomEntryThatTestsWhereTheHeroCameFromIsEnteredFromThatRoom`.

## "This object" held (31 October, later)

Indy 3 Room 17's books: Look (verb 0x09) asks who owns "this object" (+0x0000 `getObjectOwner var[0x000], var[0x007]`, the variable the sentence script sets) and shows the book's text only once the hero holds it; lying in the room, the code sends him to fetch it. The setup search now gives the picked object to the hero for such a test, as it already tried the object's own states. Measured (`*-run15.txt`): Indy 3 own way 80.4% → 82.4%, at the cutscene 19.6% → 17.6% (three books); the other games unchanged; all five copies 83.1% → 83.3% own way, 16.9% → 16.7% at the cutscene. Pull (verb 0x07) shares the code, which reaches the cutscene only when the verb argument (`local[0x001]`, Script 2 +0x0335 `startObject … [objB, verb]`) is 9, so in the game Pull never shows it; those stay started at the cutscene. Real window (`WindowCheck 17,241,9`): "It's that book by Hitler, `Mein Kampf`.", then "It looks like a first edition." Test: `ScummSceneSetupTests.ABookLookedAtIsHeldFirstWhenItsCodeAsksWhoOwnsThisObject`.

## A class test of "this object" (31 October, later)

`ifClassOfIs var[0x007], [0x0088]` (Indy 4 Room 40's clothesline, verb 0x09 +0x0000): the class list was cut from the first bracket of the instruction, the one in `var[0x007]`, so every class test of "this object" read as class 7. It now takes the list after the object. Measured (`*-run16.txt`): at the cutscene Indy 3 17.6% → 15.7%, Indy 4 18.8% → 17.6%, the others unchanged; all five copies own way 83.3% → 84.1%, at the cutscene 16.7% → 15.9% (Indy 3 84.3%, Monkey Island 1 86.0% and 86.9%, Monkey Island 2 83.7%, Indy 4 82.4% own way). LOOM unchanged (`../loom-editions-20261031/run5-loom-ega-mt32-en.txt`). Test: `ScummSceneSetupTests.AClassTestOfThisObjectReadsTheClassListNotTheVariable`.

## Call arguments, actors held in variables, VAR_ROOM (1 November, one batch)

Three readings of the trigger's code, measured together (`*-run18.txt`, Indy 3 256 colours `indy3-vga-en-run17.txt`):

1. **A called script's locals are its caller's literal arguments** (Indy 4 Room 7's cat figurine: `chainScript 206, [0x0074, 0x0009]`; local script 206 +0x0007 tests the class of `local[0x000]`, object 116). The trigger's own code now counts as a caller too.
2. **An actor held in a variable** (`getActorRoom var[0x000], var[0x0B6]`: Sophia in Indy 4) is a setup target, read when the setup is applied.
3. **A comparison with VAR_ROOM** is one with the scene's room ("is she here").

| Game | Scenes | Own way | At cutscene |
|---|---|---|---|
| Indy 3 (EGA; 256 colours the same) | 153 | 84.3% | 15.7% |
| Monkey Island 1 floppy | 164 | 86.0% | 14.0% |
| Monkey Island 1 CD | 160 | 86.9% | 13.1% |
| Monkey Island 2 | 190 | 83.7% → 84.2% | 16.3% → 15.8% |
| Indy 4 | 426 | 82.4% → 83.1% | 17.6% → 16.9% |
| **Total** | 1,093 | **84.1% → 84.4%** | **15.9% → 15.6%** |

Tried and taken out again: an actor variable a fresh session has not set yet falling back to the value the boot script gives it (Indy 4's ladder, Room 48 object 583, still starts at its cutscene). Test: `ScummSceneSetupTests.AChainedScriptsLocalsAreTheArgumentsItsCallerGives`.

## Which side of a test leads to the cutscene; how the room is entered; who holds what (1 November, second batch)

Changes to the setup search (`ScummSceneSetupSearch`) and the session (`ScummSession`), measured together (`*-run23.txt`, LOOM `../loom-editions-20261031/run9-*.txt`; `*-run19.txt` to `*-run22.txt` are the steps):

1. **The side of each test that leads on to the scene** (`ScummSceneSetupSearch.Paths.cs`): the script holding the scene's first `cutscene`, and each script that calls it, is read as a graph of its jumps; a test from which only falling through, or only its branch, reaches the cutscene (or the call) is a guard. ScummVM's `jumpRelative(cond)` jumps when the condition is false (o5 `isLess` and the like compare the constant with the variable: `b < a`), so the value on the scene's side is known. Values on that side are tried first and then together; values on the other side last, and never in the combined try. Indy 4 Room 27 begins with "came from Room 75" and `var[0x0EE]` = 3 ([picture](window-guards/27-0-0.png), the real window: Indy and Sophia outside, its cutscene running).
2. **Scripts on the way to the cutscene**: the scan lists the scripts a scene reaches; the scripts the trigger's code starts (literal numbers, three calls deep) that lead to the cutscene's script are read too (Monkey Island 2 Room 56's entry starts local 200, whose bits decide whether local 207 runs: with `bit[0x0D2]` it plays 22 lines, still started at its cutscene; it showed nothing).
3. **How the room is entered**: `VAR_WALKTO_OBJ` (variable 38) is now the door object while `loadRoomWithEgo` enters a room, 0 afterwards, as ScummVM's `o5_loadRoomWithEgo` sets it (versions 3 to 5). New setup values: the room entered under a **pseudo room number** (`EnteredAs`, a test of VAR_ROOM against 0x80 and up), the hero **coming in by an object** (`EnteredBy`, a test of `VAR_WALKTO_OBJ`; object scenes also get the doors their room's entry tests), and the room entered **without the hero** (the hero in Room 0: Monkey Island 2 Room 72). Indy 3 Room 21's corridor scene needs the door 311, which no literal arrival uses: the setup brings the hero in by it. Indy 3 Room 82's entry sets the grail chamber up by the door: its two object scenes lost their fresh start with the port and get it back through the door value.
4. **Who holds what**: an owner test against another actor gives the object to that actor (`OwnedBy`, only on the way to the cutscene: Indy 3 Room 82's cracked seal, the Grail held by Elsa).
5. **Readings**: a variable read again no longer keeps its earlier meaning (Indy 4 Room 44: "object 529 state 44" became "actor 2 in Room 44"); a walk box read inside an expression; a variable indexed by a literal (`bit[0x057+0x007]` is `bit[0x05E]`, Indy 4 Room 67's rope ladder).

| Game | Scenes | Own way | At cutscene | Showing nothing (no line, no other room) |
|---|---|---|---|---|
| Indy 3 (EGA; 256 colours the same) | 153 | 84.3% → 88.9% | 15.7% → 11.1% | 9 → 4 |
| Monkey Island 1 floppy | 164 | 86.0% → 87.2% | 14.0% → 12.8% | 6 → 5 |
| Monkey Island 1 CD | 160 | 86.9% → 88.1% | 13.1% → 11.9% | 4 → 3 |
| Monkey Island 2 | 190 | 84.2% → 88.9% | 15.8% → 11.1% | 6 → 3 |
| Indy 4 | 426 | 83.1% → 90.6% | 16.9% → 9.4% | 19 → 11 |
| **Total** | 1,093 | **84.4% → 89.2%** | **15.6% → 10.8%** | 44 → 26 |

LOOM on the shared engine: at the cutscene EGA English 3.2% → 0.9%, CD 2.7% → 0.4%; the English answer key unchanged (`../loom-answer-key-20261031/compare-run7-loom-session.txt`: 238 of 238 begin, same rooms, 219 same category). What is left showing nothing: walk-offs that wait on the hero's walk box and position (Indy 4 Room 44's local script 203: box 1 left of x 84, an exit), VAR_ROOM tested against another room (Monkey Island 2's dog, Room 53, plays only in Room 8), music-synchronised waits. Tests (`ScummSceneSetupTests`): `AVariableReadAgainIsTestedForWhatItHoldsNow`, `ARoomEntryThatTestsAPseudoRoomNumberIsEnteredUnderIt`, `ACorridorSceneIsEnteredByTheDoorItsEntryAsksFor`, `AnObjectAnotherActorMustHoldIsGivenToThatActor`, `TheScriptsOnTheWayToTheCutsceneAreReadForTheirTests`; Indy 4 Room 10 now comes from Room 12 (tested on the way to its cutscene) where it came from Room 15. Tools: `summarise.py <run> <previous run>` in this folder; `MEASURE_SCRIPTS=1` prints a trigger's scripts and its cutscene; SourceDump `arr:<room>` lists the decoded arrivals into a room.

## Re-entering a room, standing the hero where a script waits, and the rest of a scene started at its cutscene (1 November, third batch)

Measured together (`*-run26.txt`, LOOM `../loom-editions-20261031/run12-*.txt`; `*-run24.txt` and `*-run25.txt` are the steps):

1. **Came from the room itself or a pseudo room**: an entry that tests the room just left against its own number (Indy 4 Rooms 33, 35, 55: the hero went out and came back) is entered again; one that tests a pseudo room number (Indy 4 Room 57: 163) visits its real room under that number first.
2. **The hero put in the room before its entry** where no decoded arrival brings him (the hero's room compared with VAR_ROOM), as `loadRoomWithEgo`'s `putActor(room)` does; and **an actor stood at an x position** a script waits for (`ActorX`: `getActorX` then compared, only on the scene's side). A **branch back** is a wait, not the way on: Monkey Island 1 Room 72's local script 201 loops until Guybrush is left of x 180, so x 179 is the value (both floppy and CD now play it their own way).
3. **A scene started at its cutscene** also gets the values that guard the way to it and those that let more of it run after its `cutscene` instruction (a test whose branch jumps forward over a block): Monkey Island 2 Room 79, Wally, now speaks 6 lines (`notEqualZero bit[0x237]`, +0x0037); started there before, it showed nothing.

| Game | Scenes | Own way | At cutscene | Showing nothing |
|---|---|---|---|---|
| Indy 3 (EGA; 256 colours the same) | 153 | 88.9% → 89.5% | 11.1% → 10.5% | 4 → 4 |
| Monkey Island 1 floppy | 164 | 87.2% → 88.4% | 12.8% → 11.6% | 5 → 1 |
| Monkey Island 1 CD | 160 | 88.1% → 89.4% | 11.9% → 10.6% | 3 → 1 |
| Monkey Island 2 | 190 | 88.9% → 88.9% | 11.1% → 11.1% | 3 → 2 |
| Indy 4 | 426 | 90.6% → 92.3% | 9.4% → 7.7% | 11 → 6 |
| **Total** | 1,093 | **89.2% → 90.3%** | **10.8% → 9.7%** | 26 → 14 |

LOOM unchanged (EGA 0.9%, CD 0.4% at the cutscene; answer key `../loom-answer-key-20261031/compare-run8-loom-session.txt`: 238 of 238, same rooms, 219 same category). Of the 14 showing nothing, several animate without a line (Indy 3 Room 62's doors are drawn and sounded) or wait on two values at once (Indy 4 Room 44: a bit, then the hero's walk box and x). Tests: `AWaitForTheHeroToWalkPastAPointStandsHimThere`, `ASceneStartedAtItsCutsceneGetsTheValuesThatLetItsLinesRun`.

## "This object" passed to a script, a script that must be running, a wait for the camera (1 November, fourth batch)

Measured together (`*-run27.txt`, LOOM `../loom-editions-20261031/run13-*.txt`):

1. **Call arguments that are not literals**: the arguments a trigger's code passes to the script it starts or chains to are read as that script's locals when they are "this object" (`var[0x007]` in object code: the picked object) or the verb's second object (`local[0x000]` in object code: a second-object value). A local the script sets to `var[0x007]` is "this object" too (Indy 4 Script 63 +0x0005). Monkey Island 2 Room 18's coffin, Close: `startScript 203, [var[0x007]]`, whose local 203 closes it only in state 1; Indy 4 Room 3's door (Script 63, state 1) and Room 43's X marks the spot (`chainScript 210, [0x0206, local[0x000]]`: dug with object 1001). A scene started at its cutscene gets "this object" as its local too. The bracket of a call's argument list was cut at the argument's own bracket (`[var[0x007]]`), which hid every such argument.
2. **A script that must be running** (`ScummSetupKind.ScriptRunning`: `isScriptRunning` then compared, only on the scene's side): started in the scene's room. Indy 3 Room 32's painting starts Script 115 only while Script 114 runs.
3. **A wait for the camera** (`ScummSetupKind.CameraX`: `VAR_CAMERA_POS_X` compared, only on the scene's side): the hero stood at that x and the camera taken to him. Indy 3 Room 86's local script 201 loops until the camera is left of x 0x186; the knight's scene now plays its 11 lines the game's own way.

| Game | Scenes | Own way | At cutscene | Showing nothing |
|---|---|---|---|---|
| Indy 3 (EGA; 256 colours the same) | 153 | 89.5% → 92.2% | 10.5% → 7.8% | 4 → 4 |
| Monkey Island 1 floppy | 164 | 88.4% → 89.6% | 11.6% → 10.4% | 1 → 1 |
| Monkey Island 1 CD | 160 | 89.4% → 90.6% | 10.6% → 9.4% | 1 → 1 |
| Monkey Island 2 | 190 | 88.9% → 90.5% | 11.1% → 9.5% | 2 → 2 |
| Indy 4 | 426 | 92.3% → 93.2% | 7.7% → 6.8% | 6 → 6 |
| **Total** | 1,093 | **90.3% → 91.7%** | **9.7% → 8.3%** | 14 → 14 |

No scene moved the other way. LOOM unchanged (EGA 0.9%, CD 0.4% at the cutscene). `summarise.py` now finds the result line after the scripts line (`MEASURE_SCRIPTS=1`). Seen and left: Monkey Island 1 Room 14's cooking pot starts its scene only in debug mode (`notEqualZero var[0x027]`, `VAR_DEBUGMODE`); scenes that play only when VAR_ROOM is another room (Monkey Island 1 Room 30's sword in Room 36, Monkey Island 2 Room 53's dog in Room 8) are the next step. Tests: `ThisObjectPassedToAScriptIsThePickedObject`, `TheSecondObjectPassedToAScriptIsGivenToTheHero`, `AScriptThatMustBeRunningIsStartedAndACameraWaitIsMet`.

## The second object in a direct run, scenes used in another room, a variable beside its constant (1 November, fifth batch)

Measured together (`*-run30.txt` against `*-run27.txt`; `*-run28.txt` and `*-run29.txt` are the steps, LOOM `../loom-editions-20261031/run14-*.txt`):

1. **The second object in a direct run**: `ActivateObjectVerb(A, verb, B)` gives the verb's code `local[0x000]` = B and `local[0x001]` = the verb, as the sentence scripts' `startObject A, verb, [B, verb]` (Indy 4 Script 2 +0x0520). The game's own way can refuse a pair the explorer picks: Indy 4's sentence script calls Script 9, which accepts "Use A with B" only when A has class 1, 2 or 3, so the peg (Room 72), Sophia (Room 97) and the pulley (Room 50) were refused and their direct run lacked B (Indy 4's totem pole and sentry statue begin the game's own way through 4 below). The app's pick passes it too.
2. **Used in another room** (`ScummSetupKind.PlayedIn`): an object verb whose test of VAR_ROOM against another room leads to its scene is played in that room with the object held (its code read from the room it lies in; `ActivateObjectVerb` reads a held object's code from there). It is a last try of its own, after the other values: Indy 4 Room 76's lamp also tests Room 77 on a later branch, and visiting it first misled the search. Monkey Island 1 Room 30's sword ("On those helpless dogs?", Room 36) and Monkey Island 2 Room 53's dog (Room 8) now play their own way.
3. **A story variable beside its constant**: an equality test also tries a value beside the constant (constant − 1), so a scene on its unequal side can be reached (Indy 3 Room 24's first aid kit: `isEqual var[0x0C1], 1000` prints "I'll save it for when I need it"; at 999, "I feel much better now"). Only for a variable compared with nothing else that leads on (Indy 4 Rooms 27 and 65 compare `var[0x0EE]` with 1, 2 and 3), and never in place of a constant the code compares with.
4. **A kept value gives way**: while a scene has not begun, a later value of the same target may replace the one kept (it printed a line but did not begin the scene).

| Game | Scenes | Own way | At cutscene | Showing nothing |
|---|---|---|---|---|
| Indy 3 (EGA; 256 colours the same) | 153 | 92.2% → 92.8% | 7.8% → 7.2% | 4 → 4 |
| Monkey Island 1 floppy | 164 | 89.6% → 91.5% | 10.4% → 8.5% | 1 → 1 |
| Monkey Island 1 CD | 160 | 90.6% → 91.9% | 9.4% → 8.1% | 1 → 1 |
| Monkey Island 2 | 190 | 90.5% → 91.6% | 9.5% → 8.4% | 2 → 1 |
| Indy 4 | 426 | 93.2% → 94.4% | 6.8% → 5.6% | 6 → 5 |
| **Total** | 1,093 | **91.7% → 92.9%** | **8.3% → 7.1%** | 14 → 12 |

No scene moved the other way. LOOM unchanged. Real window (`WindowCheck 30,402,8`, Monkey Island 1 floppy, private profile): the pick opens Room 36 with Guybrush at the mansion and the dogs, "On those helpless dogs?", then "That's inhumane!" ([picture](window-elsewhere/30-402-8.png)). Tests: `AVerbThatPlaysOnlyInAnotherRoomIsUsedThereWithTheObjectHeld`, `ADirectRunPassesTheSecondObjectAndAVariableIsTriedBesideItsConstant`.
