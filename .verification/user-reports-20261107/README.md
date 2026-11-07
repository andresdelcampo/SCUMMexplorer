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

## Tools

`PlanPick/` (headless: every plan from each source room, `walk <room> <x> <y> [frames] [after rooms]` frame by frame, `first <rooms>` times the first-visit pictures; `PLANPICK_AFTER` frames after a guard's cutscene begins). WindowCheck `K:x,y`.

## Tests

`UserReports20261107Tests`: an EGA script decoded once per game; the EGA plans live and walking. Full suite: `full-suite.txt`.
