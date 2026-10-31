# Monkey Island CD S5: the rooms show the first visit of the generic session

| | |
|---|---|
| Family / stage | MonkeyCd / S5 |
| Fixture and edition | `Games/monkey1 vga en` (The Secret of Monkey Island CD, English, SCUMM v5, disc image) |
| Date and author | 2026-10-17, S4b/S5 MonkeyCd agent |
| Commits | the S5 code commit and this record's commit (after the S4b commits) |
| Registry change | unchanged, because the Objects cell stays partial until S6 (REQUIREMENTS section 3.1). The tracker records "S5 done, with three rooms written down". |

## What was done

- `ScummProfile.HasFirstVisitScenes` is true for the v5 format of the one v5 family whose boot the session follows (MonkeyCd); Monkey Island 2 and Indiana Jones 4 stay without (no S4b). `ScummRoomFirstVisit.Run` reads the starting states from the v5 data source (`DOBJ`) and the picture from the resource file.
- Executor: `o5_getObjectState` (v5 0x0F/0x8F; 19 rooms' entry and local scripts and Scripts 25, 26 reported it as not modelled) and nested actor queries in `expression` (`getActorX`/`Y`, `getActorFacing`, … as the plain instructions answer them; Script 45 and Room 42 local script 206).
- Costumes: format 0x59 (32 colours; ScummVM `ClassicCostumeLoader::loadCostume`, the run byte is 3 bits of colour and 5 of length) besides 0x58; Room 28's pirates use it and the room could not run before.
- Harness `RoomAudit/` (copied from the floppy's record; "unsupported" added to the markers): `room-audit.txt`, `room-audit.tsv`, `room-28.png` (the SCUMM Bar).

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build .verification\monkeycd-objects-20261017\RoomAudit\MonkeyCdRoomAudit.csproj
rtk.exe proxy dotnet Exe\MonkeyCdRoomAudit\Debug\net10.0-windows\MonkeyCdRoomAudit.dll "D:\Juegos\Lucas\monkey1 vga en" .verification\monkeycd-objects-20261017   # SCUMM_EXPLORER_PROFILE_ROOT private
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "...~Loom|...~Monkey|...~Indy3|...~ScummGameSession|...~FirstVisit|...~Play|...~Support|...~Architecture|...~Profile|...~ScriptReader|...~V5|...~Executor|...~Cutscene|...~Costume"
```

## Results (numbers)

- First audit run (before the fixes): 83 rooms, 81 run without a gap, 2 could not run (28, 99); 23 items not followed (`getObjectState` 19, expressions 5 rooms, plus Rooms 88 and 89).
- Final: **83 rooms; 82 run, 0 gaps; 1 room could not run (Room 99)**; outcomes Settled 60, FrameLimit 19 (ambient scripts keep changing the room; the state at the limit is shown), RoomLeft 3.
- Not followed, written down (R-9.19): Room 88's entry script, "execution reached an undecoded byte at +0x0000"; Room 89 local script 200, "disassembly stops at +0x0091: saveRestoreVerbs subopcode 0x00 is not defined"; Room 99, "Object 1000 is absent from the global state table" (the run throws; the App shows the plain background with its Advanced note). None of them was investigated further in this run (budget).
- The index states alone show no gap in any room (the audit's "before S5" line): this game's stored images never leave colour-0 holes at the start, so the gain of S5 is the script-drawn state, not gap filling.
- Pinned (`ScummMonkeyCdFirstVisitTests`): Room 30 active 390, 398, 399; Room 37 active 479, 485; Room 70 active 773, 776, 778, 781 (all Settled, no gap); Room 28 runs with costume 24 drawn and nothing skipped.
- Tests: the filter above 958 total, 953 passed, 5 skipped (LOOM dumps), 0 failed.

## Comparison with ScummVM (source files and date; runtime comparison if any)

`script_v5.cpp` (`o5_getObjectState`, `o5_expression`), `costume.cpp` (`ClassicCostumeLoader::loadCostume`, `ClassicCostumeRenderer` mask/shift for 32 colours), from memory of ScummVM master, not re-read this run (17 October 2026). **No picture comparison with the bundled ScummVM** (not run, per the brief): the S5 acceptance's ten-room comparison is not done.

## Differences and uncertainties kept

- Ten-room picture comparison with ScummVM: not done (user check below).
- Rooms 88, 89, 99 as above.
- The v5 untouchable class (32) is not yet what the shared hit test reads (24): hotspots of untouchable objects may be offered (S6).
- 19 rooms end at the frame limit; their state is the one at the limit.

## User checks (exact steps and where; none if none)

- Open the CD game in the explorer, look at the room cards of Rooms 28, 30, 33, 37, 38, 70 and compare with the same rooms at their first visit in ScummVM.

## Where the facts went

[tracker, MonkeyCd block](../../docs/archive/monkey1/SUPPORT_STATUS.md), [boot sequence v5](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), CHANGELOG 17 October.
