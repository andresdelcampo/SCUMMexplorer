# Monkey Island floppy S4b: the generic session boots The Secret of Monkey Island to the first room

| | |
|---|---|
| Family / stage | MonkeyFloppy / S4b |
| Fixture and edition | `Games/monkey1 ega en` (four-disk 256-colour floppy release, `DISK01.LEC` to `DISK04.LEC`, version string "VGA 1.0 12/3/90"; the folder name says EGA, the data is VGA); also tried: `Games/monkey1 ega es` (eight-disk 16-colour Spanish release) |
| Date and author | 2026-10-14, S4b MonkeyFloppy agent |
| Commits | 5bad9f2 (disk-set data source), 3e89556 (executor fixes), 4bd9824 (session, profile, tests), 1dca2a3 (Proposed R-3.15, R-15.7, R-15.8), and the documentation commit after them |
| Registry change | unchanged, because S4b changes no matrix cell (Objects and Play come with S5 and S6) |

## What was done

- `ScummV4DiskSetDataSource` (Core/Scumm/V4): one `IScummDataSource` over every disk archive. A resource lives on the one disk whose room list holds its room (the shared index `000.LFL` names the room of every script, costume and sound); `ScummDataSources.For` returns it for a v4 game that has no single archive. All disks are assumed inserted (labelled, Proposed R-3.15): the game's "Insert Disk %c" strings are never reached.
- `ScummProfile` data (R-15.6): engine variables of version 4 at start (`VAR_HEAPSPACE` 1400, `VAR_FIXEDDISK` 1, `VAR_DEBUGMODE` 0), 800 zeroed globals, `CameraFollowStartsScene`, `BootInput` (the plan below) and `BootEndsWhenPlayerHasControl`. The game is told apart in the profile by its family (`GameSupport.FamilyOf`), never in shared code.
- Executor and reader fixes, each from the ScummVM source and pinned: `o5_actorOps` case 17 (scale) reads one value in version 4 (`VersionFourReadsOneValueForAnActorsScale`); `actorFollowCamera` on an actor outside the current room starts that room (`setCameraFollows`), actor 0 starts room 0 (the boot's script 1 ends with the ego put in Room 38 while Room 0 is current; the run cannot pass it otherwise: `TheBootReachesTheDock…` finds "Room 38 entered by Script 01 +0x0E26 (actorFollowCamera)"). The property is opt-in so LOOM stays identical.
- `ScummGameSession` boot: a boot that stands still 400 frames (nothing changes in the room and the pending scripts) is answered by keys through the game's own input script (`VAR_VERB_SCRIPT`), one per frame: Enter for the "press ENTER" screen, then the four digits of `var[0x10A]`, the year the game's own script derived before it waited (read, never written). The boot ends when the player has control (room, no cutscene, user input on). `PressKey` accepts the null room (the credits wait in Room 0). The session's AdLib music timer reads the disks' `AD` blocks (`ScummAdlibAudio.ReadLifetime(byte[], …)`, half tempo as the browsed disks play).
- Harness `BootTrace` (beside this file) writes `boot-trace.txt` and `room-33-after-boot.png`; `BootTrace dump <folder> g:N | e:R | l:R:N | find:TEXT | r:R:N` disassembles through the disk-set source; `BootTrace scan <folder>`.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build                      # whole project
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom"                                               # LOOM regression
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~ScummMonkeyFloppySessionTests|...~SupportMatrixTests|...~SupportConformanceTests|...~GameSupportTests|...~ScummSessionArchitectureTests|...~ScummGameSessionTests"
rtk.exe proxy dotnet build .verification\monkey1-session-20261014\BootTrace\BootTrace.csproj
rtk.exe proxy dotnet BootTrace.dll "Games\monkey1 ega en" .verification\monkey1-session-20261014 900
rtk.exe proxy dotnet BootTrace.dll dump "Games\monkey1 ega en" g:1 g:152 ...
```

## Results (numbers)

- Boot: **2522 frames** (limit 5000), 1.7 s wall time; rooms entered in order **90, 0, 10, 0, 0, 38, 96, 33**; final room **33**; no cutscene open; the player has control. 900 more frames leave Room 33 stable (pending: the random loop 156, the interface loop 23, Room 33's local script 202).
- Without the answer the boot stands still in Room 90 (script 152 +0x013E, waiting for a digit): the 5000-frame limit is reached.
- Unsupported (opcode, operand or state the run could not follow): **0**. Display-only operations reported as not applied: `lights`, `oldRoomEffect`, `resourceRoutines`, `roomOps`, `stringOps` (counts in `boot-trace.txt`). 1970 seeded random picks (900 extra frames), each labelled an estimate.
- Assumed input: 5 keys (Enter, then 1, 5, 4, 3: `var[0x10A]` = 1543 in this run), all labelled *Player input assumed*; the game accepted the answer (the credits followed).
- Room 33 after the boot: 5 objects, 2 stored images, the ego (costume 1) drawn at (346,133), none skipped: `room-33-after-boot.png` (a harbour village at night with the ego on the quay). No first-visit guarantee (S5).
- Tests: whole project 983 total, 978 passed, 5 skipped, 0 failed (baseline 976, 971, 5: +7 new). `Loom*`: 841 total, 836 passed, 5 skipped (the same list as before: none of the new tests has Loom in its name; the whole-project count rose by exactly the seven new tests). New: `ScummMonkeyFloppySessionTests` (7). `ScummSessionArchitectureTests` (3), `ScummGameSessionTests` (Indy3, 4: 1341 frames as before), `SupportMatrixTests`, `SupportConformanceTests`, `GameSupportTests`: pass.
- Spanish eight-disk EGA release (`monkey1 ega es`): the same boot, 2534 frames, rooms 90, 0, 10, 0, 0, 38, 96, 33, unsupported 0 (test `TheSpanishEightDiskEgaReleaseBootsTheSameWay`). Its `VAR_VIDEOMODE` is 19 (the profile does not tell EGA from VGA for a v4 game); the boot reads that variable only for CGA.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source files (`engines/scumm`, `master`, read 14 October 2026): `vars.cpp`, `script_v5.cpp` (`o5_actorOps`), `camera.cpp` (`setCameraFollows`), `sound.cpp` (`updateMusicTimer`), `scumm.cpp` (`setupMusic`); the boot itself is the one of [the v3 record](../indy3-session-20261014/README.md) (`runBootscript`). The facts are in [SCUMM_BOOT_SEQUENCE.md](../../docs/engine/SCUMM_BOOT_SEQUENCE.md) (v4 section) and [SCUMM_SCRIPT_MAP.md](../../docs/engine/SCUMM_SCRIPT_MAP.md). No runtime comparison with the bundled ScummVM was made: the boot's picture, the credits' length and timing are not compared (S5).

## Differences and uncertainties kept

- The credits' timing rests on an **estimated** music timer: ScummVM plays this game's AdLib music through iMuse, the session plays the song's `AD` block at the half tempo the browsed disks use (set by ear on 11 October). The credits finish; their real-time length is unchecked.
- The answer is the right one by construction (read from the game's variable). A wrong answer four times makes script 152 quit (+0x002E); that path was not run.
- The boot ends at the first moment the player has control; Room 96 (title music, input off) is passed through as part of the boot. Whether the game's input state at Room 33's first frame equals the original's was not compared.
- `Ctrl+M` in the trace is key 0x0D (Enter); `DescribeKey` of the shared session has no name for it.
- Disks: every disk is assumed inserted; the original asks for disk changes during play.
- The eight-disk release's costumes are 16-colour: the session reads costumes with the VGA reader (`ScummV4DataSource.ReadCostume`), so its actors would draw wrongly; the boot does not need them.
- Room 38's cutscene (the opening) completes within the boot's frames: its text lines, walks and delays were followed by the bounded executor with the shared message and walk estimates (labelled in the trace).

## User checks (exact steps and where; none if none)

None required. (A look at `room-33-after-boot.png` against the real game's first room is welcome.)

## Where the facts went (links to the script map / engine notes / tracker entries)

[Monkey Island script map](../../docs/monkey1/MONKEY1_SCRIPT_MAP.md), [boot sequence v4](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [variables and opcode rules](../../docs/engine/SCUMM_SCRIPT_MAP.md), [tracker and decision log (MonkeyFloppy block)](../../docs/monkey1/SUPPORT_STATUS.md), Proposed rows R-3.15, R-15.7 and R-15.8 in [REQUIREMENTS](../../docs/REQUIREMENTS.md).
