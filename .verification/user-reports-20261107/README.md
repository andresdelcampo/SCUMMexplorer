# The user's report of 7 November 2026: the castle plans

The user: "Top view castle maps 20, 28, 32 no longer show correctly - Indy won't move. Regression from last fixes?"

## What it was

Reproduced in the real window (`WindowCheck`) on the English EGA copy (`indy3 ega en`): picking Room 20 showed "Opening Room 20… / Loading the scene…" for **36 s** (Room 28 the same) before the plan appeared; until then there is no play session, so nothing moves and clicks go nowhere (`copy-indy3-ega-en/`, `ega-wait/`). The 256-colour copies took about 12 s. Once live, Indy walks on every plan in every copy.

| Cause | Evidence | Fix (shared) |
|---|---|---|
| The background scene derivation (`StartPlayCutsceneDerivation`, its second phase `PlanSetup`) kept every worker busy while a room opened; the pick's own work waited behind it | `dotnet-trace` of the EGA window while Room 20 opened: 74% of the samples in `ScummCutscenePlanner.PlanSetup` | A room being opened holds the derivation's next step (`UpdateScanGate`: the gate it already had for a sound being rendered) |
| Two thirds of that time was `File.ReadAllBytes`: the EGA data source decoded each script from its LFL file every time it was asked, and `ScummDataSources.For` makes a new source per call. The 6 November tour (`DressHeroForScene`, `SceneScripts`) asks for many more scripts per scene | the same trace: `File.ReadAllBytes` 66% exclusive, under `ScummV3DataSource.GlobalScript` and `RoomLocalScript` | `ScummV3DataSource` keeps each decoded script once per game (thread-safe, shared by every source of the game). LOOM EGA reads through it too |
| After a still room (now a session since 6 November), the next pick put the still room's source session back in play while the play clock kept stepping it on the window's thread, and the pick copied it on another thread at the same moment (the boot's Room 73 showed meanwhile) | `seq-clock/`: Room 73 on screen during the Room 28 pick | The play clock and input stop while a pick is prepared (`StartPlayCoreAsync`); the waiting picture stays |

Measured on the EGA copy in the window (`ega-gate/`): Room 20 first pick 36 s → 16 s (it includes the game's boot and the plan's own first-visit picture, about 6 s, whose guards never rest: the 30 October record), Rooms 28 and 32 picked after it about 3 s. Indy walks in all three.

## Not a fault

- A click on a part of a plan that is not joined to Indy's corridor does not move him: Room 20's boxes 5 to 7 are a corridor of their own (`SourceDump box:20`), and Script 28 only `walkActorTo`s the click (+0x0032). ScummVM's `Actor_v3::walkActor` stops the same way when `getNextBox` finds no way. The plan shows only the sections Indy has been in, so the black around them may be unreachable.
- A guard who sees him starts the confrontation within a second or two of walking (Script 58 → 64 → 60), as the 6 November record says.
- A first harness version clicked with posted mouse messages; WPF ignores those unless the window is active under the real cursor, which made some runs look as if Indy did not move. `WindowCheck`'s `K:x,y` now hit-tests the window and passes the point to the image's own handler (`PlayScreenPress`).

## Work item 1, the same day: two scenes that ran their verb directly

Two shared causes found by reading the scenes whose result was a direct run with no line and no other room (`../scene-setup-20261031/*-run60.txt` against `*-run58.txt` and `*-run57.txt`; `ways.py` there compares each scene's way between runs, as `summarise.py` counts only the scenes started at their cutscene).

| Scene | Cause | Fix (shared) |
|---|---|---|
| Indy 3 Room 2, the diary (Open, used in Room 18) | A picked scene entering Room 18 closes the room's arrival cutscene; the game's cutscene-end hook (Script 23 +0x0022, `actorFollowCamera var[0x001]`) follows the hero, who was still in the boot's Room 73, so the game went back there. The game's own `loadRoomWithEgo` puts him in the room before `startScene` | `EndArrivalCutscenes` puts the hero in the room before closing the arrival. Direct → the game's own way, both copies |
| Indy 4 Room 33, Use the Sunstone with the pedestal | The pick queued (Use, pedestal, Sunstone); Script 2 +0x04A3 runs Script 9 on the first object and refuses one without class 1 to 3. The player names the held item first, and the Sunstone's own code hands over (object 934 verb 0x08 +0x0057: `startObject 0x0179, 0x08, [0x03A6]`) | A new way, `GameHeldFirst`: when the usual order does not begin a scene with a held second object, the sentence with the held object first is tried before the verb is run directly. Always putting it first was tried and lost 26 scenes their own way (Give, verb 0x50, in Monkey Island and Indy 4), so it is the second try |

Measured: no other scene changed way in any copy (run 58: the diary only; run 60: the pedestal only). The counts of scenes started at their cutscene stay: Indy 3 5.8%, Monkey Island 1 8.5% and 8.1%, Monkey Island 2 6.3%, Indy 4 3.1%. Indy 4 Rooms 48 and 72 use the Sunstone with verb 0x06, whose code was not read yet. Harness additions: `PlanPick scene <room> <object> <verb> <edits>` (the planner's run with a given setup), `classes`, `where`; `SCUMM_TRACE_SCRIPTS=label;label` puts those scripts' instruction traces in the session trace. Full suite: `full-suite-item1.txt`.

## Tools

`PlanPick/` (headless: every plan from each source room, `walk <room> <x> <y> [frames] [after rooms]` frame by frame, `first <rooms>` times the first-visit pictures; `PLANPICK_AFTER` frames after a guard's cutscene begins). WindowCheck `K:x,y`.

## Tests

`UserReports20261107Tests`: an EGA script decoded once per game; the EGA plans live and walking. Full suite: `full-suite.txt`.
