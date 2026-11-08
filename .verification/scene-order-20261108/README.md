# Scene order (8 November 2026)

ROADMAP work item 3c (user, 2 November: "some always predate others, so the bits are all set correctly when playing them, across all games").

## What it does

`ScummSceneSetupSearch.Order.cs`. The game's code is read once per game: every object verb, room entry and local script, and every global script except the boot (Script 1, whose other branches, such as Monkey Island 2's part selection at +0x0009, are not story) and helpers shared by more than 3 scenes. For each piece, the story flags it sets to constants (bits raised, variables from 0x40; the last constant written wins).

When a picked scene's setup needs a flag that exactly one piece of code outside the scene sets, that code ran first. The scene also gets:
- the other flags that code sets ("…, which sets it: earlier code that sets bit[…]");
- the flag values its tests need on the way to that write (read as the search reads a scene's way to its cutscene: `Ranked` with a goal instruction), followed back up to 4 steps.

Flags the scene sets itself, values the setup already has, and values against a test on the scene's own way are left alone. The whole set is kept only when the scene still begins and reaches the rooms it reached without it (one extra run per scene).

## Measured (run 65 against run 64, `../scene-setup-20261031/*-run65.txt`, `MEASURE_ORDER=1`)

| Copy | Setups | Given earlier state | Ways changed |
|---|---|---|---|
| Indy 3 EGA / 256 | 56 / 56 | 0 / 0 | none |
| Monkey Island 1 EGA / VGA | 48 / 47 | 2 / 2 | none |
| Monkey Island 2 | 82 | 11 | none |
| Indy 4 | 192 | 10 | none |
| Maniac Mansion classic / enhanced | 18 / 17 | 1 / 1 (3 offered, 2 dropped by the run) | none |
| Zak McKracken classic / enhanced | 27 / 27 | 0 / 0 | none |

27 of 570 setups (4.7%). Scenes started at their cutscene unchanged: Indy 3 4.5%, Monkey Island 1 7.9% and 7.5%, Monkey Island 2 5.3%, Indy 4 3.1% (5.0%); 10 show nothing. Checked in the window on Monkey Island 2 Room 5's entry (the pirates' story plays, `window-mi2-room5/`).

## Second step: the rooms reachable at that point (8 November 2026)

`ScummSceneSetupSearch.Reach.cs`. Every room load with a literal room in the game's code is read: each object's verbs (through the scripts they start, as `ScummDoorwayFinder` follows them), each room's entry and local scripts, and each global script. The values on the scene's side of the tests on the way to the load (`Ranked` toward it, rank 0: bits, story variables, items held) are that way's gate. A room then needs what **every** way into it needs, through the room the way comes from (a dominator-style fixpoint: a load from a global script, or into a room no read way leads into, needs nothing; a way the read misses makes a room need less, never more).

A scene in that room, **one a fresh start begins included** (`ScummCutscenePlanner.PlanSetup` → `Place`), gets those values its setup does not have, its own code does not set and its way does not test otherwise, then the state of the code that sets them (first step). The whole set is kept only when the scene still begins and reaches the rooms it reached without it. The note reads "Story point (estimate): …".

Two corrections to the first step came with it:
- a variable needed at 0 holds what it starts with, so the code that writes 0 is no sign of an earlier step (Indy 3 Room 40's local 201, `var[0x076]`);
- the earlier code gives only the writes one run of it can make with the needed write (forward and back by falling through and forward jumps), not those on its other branches.

### Measured (run 67 against run 65, `../scene-setup-20261031/*-run67.txt`, `MEASURE_ORDER=1`)

| Copy | Fresh-begun scenes placed | Setups with room state | Setups with earlier code | Ways changed |
|---|---|---|---|---|
| Indy 3 EGA / 256 | 10 of 93 / 10 of 93 | 7 / 7 | 0 / 0 | none |
| Monkey Island 1 EGA / VGA | 0 of 106 / 0 of 102 | 0 / 0 | 2 / 2 | none |
| Monkey Island 2 | 17 of 101 | 7 | 15 | none |
| Indy 4 | 6 of 225 | 3 | 10 | none |
| Maniac Mansion classic / enhanced | 0 of 8 / 0 of 9 | 0 / 0 | 1 / 1 | none |
| Zak McKracken classic / enhanced | 0 of 21 / 0 of 21 | 0 / 0 | 0 / 0 | none |

43 of 779 fresh-begun scenes (5.5%) are now placed, and 49 of 634 setups (7.7%) carry room or earlier state (27 after the first step): 92 of 1,413 scenes (6.5%) in all. Per game, scenes with such state: Indy 3 17 of 155 (11.0%, each copy), Monkey Island 2 33 of 190 (17.4%), Indy 4 19 of 426 (4.5%), Monkey Island 1 2 of 165 and 2 of 160 (1.2%), Maniac Mansion 1 of 27 (3.7%, each copy), Zak McKracken 0 of 54 (0%). No placed scene's rooms or lines changed (the values are those the room already had in the game, so the scene plays as before, now with the story's flags set). Started at their cutscene unchanged: 5.0% of the five non-LOOM copies; 10 show nothing. Examples: Monkey Island 2's Scabb Island rooms (4, 6, 16, 20, 21, 22) all lie past global script 97's way into Room 2 (`bit[0x039]`, `var[0x20B]`), whose setter is the opening, global script 48 (one straight path: its flags come with it); Room 43 needs `var[0x0AF]` = 1; Indy 4 Room 28 needs `var[0x0EE]` = 3 (Room 27's way in). Checked in the window on Monkey Island 2 Room 43's book of pirate quotations (`window-mi2-room43/`: the push and the lines that follow play).

## Third step: doors and rooms in variables (8 November 2026)

- **Object states and classes on the way in** now count as a way's gate too (a door that must be open, ObjectState, its state bit in versions 1 and 2, a class). A setup sets them for objects of any room. Maniac Mansion's rooms now need the doors you walk through (Room 14: Room 11's door, object 180, open).
- **A room held in a variable** is read when a `move` of a constant into that variable comes before the load on the same path: one way per constant, its gate read toward that `move` (a choice of rooms). The rest are listed (`MEASURE_LOADS=1`, `loads/`): Zak McKracken's travel (room and object both in variables set elsewhere, 40), Indy 4's Atlantis rooms (64), Indy 3 19, Maniac Mansion 2, Monkey Island 2 3, Monkey Island 1 2.
- **Only story variables** (from 0x40) come with the earlier code and the ways in. The first step had let in the engine's own variables on the way to an earlier write: Maniac Mansion's microwave got `var[0x00A]` = 132, which is the second object (`VAR_ACTIVE_OBJECT2`, ScummVM `setupScummVars` for version 2), and its pick then did nothing in the window (the headless run was unaffected). Found by the window check: on the build before 8 November the microwave plays, on run 68's build it did not, now it plays again.
- **The window keeps these plans** with the others: a fresh-begun scene's plan is worked out in the background after the scenes that need a setup, and saved for the next launch (`ScummCutscenePlanner.IsPlanned`). A pick made before its plan is ready waits for it (on a first launch of Maniac Mansion, about 13 seconds for the microwave; the next launch plays it at once).

### Measured (run 69 against run 67, `../scene-setup-20261031/*-run69.txt`)

| Copy | Fresh-begun scenes placed | Setups with room or earlier state | Scenes with such state | Ways changed |
|---|---|---|---|---|
| Maniac Mansion classic / enhanced | 2 of 8 / 2 of 9 | 4 of 19 / 4 of 18 | 6 of 27 (22.2%) each | none |
| Indy 3 EGA / 256 | 13 of 93 each | 8 of 62 each | 21 of 155 (13.5%) each | none |
| Monkey Island 1 EGA / VGA | 3 of 106 / 3 of 102 | 4 of 59 / 4 of 58 | 7 of 165 (4.2%) / 7 of 160 (4.4%) | none |
| Monkey Island 2 | 30 of 101 | 28 of 89 | 58 of 190 (30.5%) | none |
| Indy 4 | 7 of 225 | 19 of 201 | 26 of 426 (6.1%) | none |
| Zak McKracken classic / enhanced | 2 of 21 each | 2 of 33 each | 4 of 54 (7.4%) each | none |

In all, 160 of 1,413 scenes (11.3%) carry state of their place in the story (92 after the second step). No placed scene's rooms or lines changed in the headless run; started at their cutscene stay 5.0%; 10 show nothing.

Checked in the window: Maniac Mansion (after Room 1, Syd and Bernard assumed) the microwave (radioactive steam), the TV (its cutscene, then Room 40's lines), the water valve (`window-maniac/`); Indy 3 Room 7's typewriter ("This isn't a radio") and Room 37's entry, the biplane with Henry's lines (`window-indy3/`); Monkey Island 2 Room 43's book (`window-mi2-room43/`).

## What it does not reach yet

- **Rooms with a way in that needs nothing** (a global script that loads them, an open door): the room says nothing about the story point.
- **Rooms whose variable is set elsewhere** (Zak McKracken's travel, Indy 4's Atlantis), listed in `loads/`.
- **Setups that need no flag** and **flags set in several places** (first step).

## Tools

`Measure` with `MEASURE_ORDER=1`: for each flag a setup needs, the code that sets it (`ScummSceneSetupSearch.CodeSetting`), and what earlier code adds; a `PLACED` line for each fresh-begun scene given room state; `MEASURE_LOADS=1` what each room needs and the loads not read; `MEASURE_WINDOWPATH=room,object,verb,reenter` the window's order for a scene with a setup. WindowCheck: `WINDOWCHECK_TRACE=n` logs the session's last trace lines after a scene, and each state line the window's status (a Maniac Mansion pick needs a room opened first, `r:1`, for the assumed party). `ScummSceneSetupSearch.RoomNeeds(room)` lists what every way into a room needs. Tests: `UserReports20261107Tests.APickedSceneGetsTheStateOfTheCodeBeforeIt` (Monkey Island 2 Room 5), `ASceneAFreshStartBeginsGetsWhatEveryWayIntoItsRoomNeeds` (Room 43).
