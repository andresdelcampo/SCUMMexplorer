# Monkey Island floppy S5: every room shows the first visit of the generic session

| | |
|---|---|
| Family / stage | MonkeyFloppy / S5 |
| Fixture and edition | `Games/monkey1 ega en` (four-disk 256-colour floppy release, SCUMM v4, English; the folder name says EGA, the data is VGA) |
| Date and author | 2026-10-15, S5 MonkeyFloppy agent (resumed the unverified commit 3b2bcf6 of the previous agent) |
| Commits | 3b2bcf6 (v4 first visit, profile flag, App wiring; previous agent), the S5 fix commit (bit variables, pseudo rooms, arrivals, tests, harness) and the documentation commit after it |
| Registry change | unchanged, because the Objects cell stays partial until S6 (REQUIREMENTS section 3.1: full needs click responses too). The tracker records "S5 done". |

## What was done

- **Verified 3b2bcf6** (previous agent, written while the game data was missing): `ScummRoomFirstVisit.Run` reads the index from the first v4 disk and the room picture from the disk that holds the room; `ScummProfile.HasFirstVisitScenes` is true for this game (v4 dialect on several disks); the App room cards and room view take the first visit (R-5.3a, R-6.12a); the Advanced stored-object list keeps only objects the game's object search can select. Built and run: it works for every room; two engine faults and one visit rule were found and fixed (below).
- **Bit variables kept apart from the word variables for v4** (`ScummProfile.SeparateBitVariables`, set by `ScummGameSession`). ScummVM `readVar`/`writeVar` (script.cpp) use bits of the word variables only up to version 3; the session aliased them for Monkey Island too, so a game bit in 0x040..0x04F rewrote `VAR_ROOM` (Room 30's first visit ended with `VAR_ROOM` = 542). Since S4b this affected every write of those 16 x 50 bits; the boot test is unchanged.
- **Pseudo rooms** (`o5_pseudoRoom`, `startScene`): a load of a number from 0x80 up loads the real room it maps to and sets `VAR_ROOM` to the loaded number (`ScummSession.IndexedRoom`, `EnterRoom`); `putActorInRoom` into a pseudo number keeps the actor in the real room and `getActorRoom` gives the pseudo number back while that room is current. Monkey Island loads Room 58 (a forest maze) only as 201..220 and Room 1 (a beach) only as 130..132; under its own number Room 58 was black (no layout drawn).
- **The visit's arrival** (`ScummEgoArrivalIndex`, `ScummGameSession.VisitRoom`): a literal `loadRoomWithEgo` into a pseudo room counts for its real room (mapped by the game's literal `pseudoRoom` instructions) and enters it under that number; an arrival from the room's own code (one maze cell to another) is used only when no arrival from elsewhere exists; a room with pseudo numbers and no literal arrival is entered under one of them (the first, or a seeded pick), labelled "Assumed pseudo room".
- **Harness** `RoomAudit/` (beside this file): the audit of every room plus a static cross-check (objects the room's own entry and local scripts name with a literal `setState` above 0 or `drawObject`, against the objects shown, with the script and offset of each name). `monkey1-session-20261014/BootTrace` gained `visit <folder> <room> [needle | var[...]]` to trace a visit or watch a variable frame by frame.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet build .verification\monkey1-objects-20261015\RoomAudit\Monkey1RoomAudit.csproj
rtk.exe proxy dotnet Monkey1RoomAudit.dll "D:\Juegos\Lucas\monkey1 ega en" <out>             # this record's audit (SCUMM_EXPLORER_PROFILE_ROOT private)
rtk.exe proxy dotnet BootTrace.dll dump "D:\Juegos\Lucas\monkey1 ega en" e:58 e:1 e:30 l:30:205 find:pseudo ...
rtk.exe proxy dotnet BootTrace.dll visit "D:\Juegos\Lucas\monkey1 ega en" 30 var[0x004]
rtk.exe proxy dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build      # whole project, SCUMM_INDY3_EGA_TEST_ROOT and SCUMM_SOUND_TEST_ROOT set
rtk.exe proxy dotnet Indy3RoomAudit.dll "D:\Juegos\Lucas\indy3 ega en" <out>                  # Indy3 regression
rtk.exe proxy dotnet RoomAudit.dll "D:\Juegos\Lucas\loom ega mt32 en" <out>                   # LOOM regression
```

## Results (numbers)

- **Room audit** (`room-audit.txt`, `room-audit.tsv`, contact sheets `rooms-sheet0..4.png`): 81 rooms, **81 without a gap**, 0 rooms that could not run, about 28 s for all rooms (4 threads). Before S5 (index states only) no room showed any object image (the v4 index starts every object at state 0); after: **30 rooms show objects, 56 show actors**. The gap test only sees colour-0 holes under stored images, so "no gap" is not "complete"; see below.
- **How the runs ended:** 70 settled, 9 reached the 2400-frame limit (21, 28, 31, 34, 42, 51, 57, 58, 71: ambient scripts), 2 left the room (19 to 07, 45 to 33). What the run could not follow: `actorFromPos` (not modelled) in the map rooms 02 to 06 (Script 24), Script 19's camera follow of actor 5 into room 0 (Room 37), Room 85 (one note).
- **Unit tests:** `ScummMonkeyFloppyFirstVisitTests` (4): Room 33 (the boot room: no object image active, the ego in costume 1 at (346,133)); Room 30 entered without its route (`VAR_ROOM` 30, active 404, 412, 932, shopkeeper actor 11 in costume 28); Room 58 entered as pseudo room 218 (active 649..654, 665..668) and Room 1 as 130 (active 21, 22, 23, 296, labelled pick); eight script-heavy rooms without a gap, `VAR_ROOM` right and hotspots obeying the object search. Whole project (Indy3 and LOOM data set): 994 total, 989 passed, 5 skipped, 0 failed (before this stage 990 with 985 passing in master's last run; +4 new tests).
- **Indy3 EGA regression:** `ScummRoomFirstVisitTests` (3) pass unchanged. The Indy3 room audit still finds 88 of 88 rooms without a gap; **seven rooms changed** because Indiana Jones also uses pseudo rooms: 17, 18, 21, 23, 24 and 33 are now entered under their pseudo numbers and show 2 or 3 object images and the ego (before: none, or no ego); Room 72 settles after 918 frames with 7 active images (before 164 frames, 3). Pictures checked by eye: a library, a church, castle rooms and the border with Indy present. The Indy3 record (14 October) keeps its old numbers as written.
- **LOOM regression:** the LOOM EGA (English Roland) room audit gives 0 rooms with gaps, 3 gaps before filling, 0 after: identical to the S4a/S4b/S5 baseline. LOOM's session never sets the generic profile flags (bits stay as LOOM had them).

## Comparison with ScummVM (source files and date; runtime comparison if any)

- **Source** (`engines/scumm`, `master`, read 15 October 2026): `script.cpp` (`readVar`/`writeVar`: bits in the word variables only for version 3 and below), `script_v5.cpp` (`o5_pseudoRoom`, `o5_loadRoomWithEgo`, `o5_putActorInRoom`, `o5_getActorRoom`), `room.cpp` (`startScene`: `VAR_ROOM = room`, `_roomResource = _resourceMapper[room & 0x7F]`), `object.cpp` (`getClass`/`putClass`: for small-header games class 32 is 24, so untouchable is class 24 in v4 as in v3; fetched 15 October 2026). Facts in [SCUMM_SCRIPT_MAP.md](../../docs/engine/SCUMM_SCRIPT_MAP.md) and [SCUMM_OBJECT_STATES.md](../../docs/engine/SCUMM_OBJECT_STATES.md).
- **Runtime comparison with the bundled ScummVM: not made** (the run request says ScummVM cannot be driven headlessly here; the Indy3 S5 attempt failed with a white window). The sample below is a source-level comparison: what the room's own scripts would make visible on entry, per ScummVM's opcode semantics, against what the run shows.

Sample of 14 rooms (the most script-heavy by instruction count of entry plus local scripts, plus the pseudo rooms):

| Room | Script size | Ending | Active / stored | Actors | Static cross-check and reading of the entry script |
|---|---|---|---|---|---|
| 35 | 1687 | settled 248 | 1 / 16 | 4 | no literal object named by its scripts; 463 shown from a global script; agrees |
| 25 | 1284 | settled 90 | 0 / 8 | 1 | 303 (local 205), 964/965 (local 212) not shown: 212 starts only when object 307 is in state 1 (`ifState 0x133`), 205 is not started by the entry; agrees |
| 30 | 1167 | settled | 3 / 10 | 2 | 404, 412, 932 named and shown: entry picks `getRandomNr var[0x064], 3`; non-zero starts local 209 (412, 932 on) and keeps the shopkeeper (a seeded pick, labelled); `VAR_ROOM` was 542 before the bit fix |
| 53 | 1109 | settled 90 | 0 / 8 | 6 | 885, 996 (local 203), 887 (local 215), 888 (local 212): none started by the entry (it starts 201 only when `var[0x006]` is 2); agrees |
| 28 | 1038 | frame limit | 14 / 48 | 3 | 368 (local 204) not started by the entry (0xCC, 0xCD, 0xC8, 0xC9); the 14 shown are the bar's ambient animators in one phase |
| 83 | 981 | settled 90 | 0 / 1 | 1 | 966 (local 204) not started by the entry; agrees |
| 70 | 951 | settled 105 | 4 / 15 | 1 | 746..748 are set by the entry only when `bit[0x17F]` is set (`notEqualZero` jumps over them at 0); 909..917 shown through local 203 (`startScript 0xCB, [0x038C..]`); agrees |
| 31 | 922 | frame limit | 3 / 16 | 3 | 419 (local 202) not started by the entry (0xCF x3, 0xC9, 0xCD by bits); agrees |
| 39 | 906 | settled 95 | 13 / 52 | 1 | 52 objects named across local scripts 200/201 (branches by state); 13 shown; not resolved room by room |
| 51 | 782 | frame limit | 1 / 3 | 3 | nothing named literally; the circus's ambient scripts never rest |
| 29 | 622 | settled 92 | 1 / 7 | 3 | entry sets 397 (0x18D) to 1: shown; 398/399 (local 206), 400 (local 203) not started by the entry; agrees |
| 37 | 483 | settled 162 | 2 / 15 | 2 | 478..486 (local 201), 490 (local 202) not started by the entry (0xCE, 0xC8, 0xCB); agrees |
| 58 | 526 | frame limit | 10 / 33 | 2 | under pseudo room 218 the entry draws 9 tiles plus the exits for that cell (+0x04C7..+0x0547 and the following cells); the other 23 named tiles belong to other cells; before the fix: black |
| 1 | 122 | settled 90 | 4 / 8 | 2 | under 130: objects 0x15..0x17 to 1 (the `isLess` loop runs three times), 0x128 (296) when `bit[0x215]` is 0; before the fix: no object, no seagulls |

## Differences and uncertainties kept

- No ScummVM picture was compared. "Named, not shown" is explained above for 12 rooms by the entry script's own branches; Room 39's 39 unshown names were not resolved one by one.
- The visit does not play the route: the story variables are those the boot leaves (Part One start). A room whose entry depends on later story state shows the state it has at that point (state preview, labelled). The ego's arrival is a decoded, labelled pick; rooms with no decoded arrival show no ego (Advanced note).
- Room 1's pseudo number (130) is a pick: no literal arrival decides which part of the beach comes first (the map rooms load it through scripts with non-literal arguments). Room 58 is entered as 218 because the only literal arrival from elsewhere (the map, Room 85 object 866) uses it.
- The ScummVM actor keeps the pseudo number as its room; the session stores the real room and answers `getActorRoom` with the pseudo number only while that room is current (equivalent for the current room; an actor left in another pseudo cell of the same picture would compare differently).
- Rooms that reach the frame limit show their ambient scripts in one phase (labelled pick). Room 10 (the title) keeps the credits' actors off screen.
- The changed Indy3 rooms (17, 18, 21, 23, 24, 33, 72) were accepted by the user on 15 October in their earlier state; their new state follows the game's own pseudo-room numbers and is a regression only in the sense of "different"; listed for the user check.
- `ScummRoomFirstVisit.Cached` boots once per room (about 2 s per room the first time it is opened).

## User checks (exact steps and where; none if none)

Open `Games/monkey1 ega en` in the app (Normal mode) and look at the room cards and the room view of: **33** (the dock, Guybrush on the quay), **58** (the forest: paths and Guybrush, no longer black), **1** (the beach with the seagulls), **30** (the shop with the shopkeeper), **28** (the Scumm Bar's pirates), **35** (the high street), **53** (the mansion with its dogs), **70**, **39**. In `Games/indy3 ega en`, look again at **17, 18, 21, 23, 24, 33, 72** (now with Indy and more objects). Say which rooms look wrong compared with the real game.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Monkey Island script map](../../docs/monkey1/MONKEY1_SCRIPT_MAP.md) (pseudo rooms, `VAR_ROOM`, bit variables, Rooms 1, 30, 33, 58, 70), [variables and opcode rules](../../docs/engine/SCUMM_SCRIPT_MAP.md) (bit variables v3/v4, pseudo rooms), [object states](../../docs/engine/SCUMM_OBJECT_STATES.md) (v4 first visit), [tracker and decision log (MonkeyFloppy block)](../../docs/archive/monkey1/SUPPORT_STATUS.md).

## Addendum (15 October, later): depth masks of the 11 October branch merged

- The branch `claude/practical-mendeleev-628256` (R-6.16) adds the depth masks of 16-colour v4 rooms and object images; the 256-colour masks of this fixture were already read. The first visit's actors (`ScummRoomScene.ActorLayer`) are hidden behind them as before.
- **This fixture (`monkey1 ega en`, 256 colours): audit unchanged.** Re-run with the same harness: 81 rooms, 81 without a gap; `room-audit.tsv` identical to the recorded one apart from the timing column.
- **Eight-disk 16-colour copy (`monkey1 ega es`), first audit:** 81 rooms, 81 without a gap, 0 that could not run; objects in 30 rooms, actors in 57. Its actors now pass behind the foreground; `BrowsedMasksAndObjectsTests.FirstVisitActorsOfTheMonkeyIslandFloppyPassBehindTheForeground` checks it for rooms 33, 30 and 58 of both copies. Not compared with ScummVM.
