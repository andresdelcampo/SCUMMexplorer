# Indy3 S4b: the generic session boots Indiana Jones and the Last Crusade EGA

| | |
|---|---|
| Family / stage | Indy3Ega / S4b |
| Fixture and edition | `Games/indy3 ega en` (EGA, English, DOS floppy) |
| Date and author | 2026-10-14, S4b agent |
| Commits | 50728b1, bad564e, 81b03ab (proposed rows), the boot-answer fix and the documentation commit after them |
| Registry change | unchanged, because S4b changes no matrix cell (Objects and Play come with S5 and S6) |

## What was done

- `ScummGameSession` (Core/Scumm/Session), one data-driven concrete session built on the S4a `ScummSession`; the variants come from `ScummProfile` (dialect `V3Indy3`, AdLib timing, three extra timers, video mode). `ScummDataSources.For` already gave `ScummV3DataSource`; 139 global scripts are declared by the index.
- The boot is ScummVM's: indexed script 1 and frames until it has ended or chained on. The unsupported items it hit were fixed one by one from ScummVM and each has a unit test: `pickupObject` (inventory array), `getObjectOwner`, `getInventoryCount`, `findInventory`, `doSentence` with its stack and the per-frame sentence script, `waitForSentence`, `waitForActor` of the Indy3 dialect, `saveVerbs`/`restoreVerbs`/`deleteVerbs` with save ids, `pseudoRoom`, `getVerbEntrypoint`, loading room 0, the AdLib music timer, and a disassembler fix (a literal `doSentence 0xFE` has no objects).
- The screen the boot waits on (Room 92, copy protection) is answered by the game's own answering script, started with every local 0 (a wrong answer), 12 times, labelled *Player input assumed*.
- Harness `BootTrace` (beside this file) writes `boot-trace.txt` and `room-73-after-boot.png`.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build            # whole project
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom"                                       # LOOM regression
rtk.exe proxy dotnet RoomAudit.dll "Games\loom ega mt32 en" <out>                                      # LOOM room audit
rtk.exe proxy dotnet BootTrace.dll "Games\indy3 ega en" .verification\indy3-session-20261014 900
```

## Results (numbers)

- Boot: **1341 frames** (limit 5000), about 2 s wall time, rooms entered in order **76, 79, 0, 6, 1, 92, 1, 73**; final room **73**; no cutscene open; script 1 chained to script 119. 900 more frames leave Room 73 stable with the room's own ambient scripts running.
- Unsupported (opcode, operand or state the run could not follow): **0**. Display-only operations reported as not applied: `lights`, `oldRoomEffect`, `resourceRoutines`, `roomOps`, `stringOps` (see `boot-trace.txt` for counts). 1437 seeded random picks, each labelled an estimate.
- Room 73 after the boot: 14 objects, 9 stored images, 2 actors drawn (the ego, costume 39, at 224,86 and actor 9 at 256,86), none skipped: `room-73-after-boot.png`. No first-visit guarantee (S5).
- Copy-protection screen: 12 assumed answers, `var[0x06B]` = 3, `var[0x039]` = 1, `bit[0x5E4]` = 1 (failed); script 1 then says "I'm disappointed in you!" (+0x04E1).
- Tests: whole project 976 total, 971 passed, 5 skipped, 0 failed. `Loom*`: 840 total, 835 passed, 5 skipped (the same count and skips as the S4a record). New: `ScummGameSessionTests` (4), `ScummEngineOpcodeTests` (11), `ScummAdlibMusicTimerTests` (2); `ScummSessionArchitectureTests` (3) still pass (no `GameId` in `Scumm/` except `ScummProfile`; no LOOM type in the engine layer). `SupportMatrixTests`, `SupportConformanceTests`, `GameSupportTests`: pass.
- LOOM room audit (English Roland EGA): 76 rooms, gaps 3 before filling, 0 after, 0 rooms with gaps, as the S4a baseline.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source files (`engines/scumm`, `master`, read 14 October 2026): `scumm.cpp`, `vars.cpp`, `script.cpp`, `script_v4.cpp`, `script_v5.cpp`, `room.cpp`, `object.cpp`, `sound.cpp`, `players/player_ad.cpp`, `players/player_v2base.cpp`. The facts are in [SCUMM_BOOT_SEQUENCE.md](../../docs/engine/SCUMM_BOOT_SEQUENCE.md) (v3). No runtime comparison with the bundled ScummVM was made: the boot's picture and timing are not compared (S5).

## Differences and uncertainties kept

- The answer to the copy-protection screen is wrong, so `bit[0x5E4]` is 1 (S5 may choose the correct answer; the right values are in `var[0x063..0x066]`).
- Script 73 stays pending after the boot (Script map, open questions).
- The AdLib driver constants of the session are ScummVM's for Indy3 (limit 256, tempo x1, delays >> 1); the sound player still plays Indy3 EGA AdLib with LOOM's constants (Proposed R-13.9).
- The PC-speaker music timer is not modelled; the session's device is AdLib (`VAR_SOUNDCARD` 3).
- Estimates: 1437 random picks; delays read from picked values.
- The input constants (`SingleClickGap`, `DoubleClickGap`, `InputMode`, camera 160) were not changed: S4b delivers no clicks.

## User checks (exact steps and where; none if none)

None. (A look at `room-73-after-boot.png` against the real game's first room is welcome but not required.)

## Where the facts went (links to the script map / engine notes / tracker entries)

[Indy3 script map](../../docs/indy3/INDY3_SCRIPT_MAP.md), [boot sequence v3](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [variables](../../docs/engine/SCUMM_SCRIPT_MAP.md), [tracker and decision log](../../docs/archive/indy3/SUPPORT_STATUS.md), Proposed rows R-13.9, R-15.5 and R-15.6 in [REQUIREMENTS](../../docs/REQUIREMENTS.md).
