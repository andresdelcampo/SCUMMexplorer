# Indiana Jones 256 colours S4b: the generic session boots the room-file release to its first room

| | |
|---|---|
| Family / stage | Indy3Vga / S4b |
| Fixture and edition | `Games/indy3 vga en` (read from `D:\Juegos\Lucas\indy3 vga en`): English 256-colour DOS release, `00.LFL` index and one plain `NN.LFL` per room (v4-style blocks, version 3 scripts) |
| Date and author | 2026-10-15/16, S4b Indy3Vga agent |
| Commits | eac366c (data source, profile, tests), then the record/docs commit |
| Registry change | unchanged, because S4b changes no matrix cell (Objects stays partial until S6) |

## What was done

- `ScummV4RoomFileDataSource` (Core/Scumm/V4), returned by `ScummDataSources.For` when the game's single archive was built from room files (`ScummV4Archive.IsRoomFileGame`): scripts in the profile's dialect (`V3Indy3`), each script naming the room file that holds it (`73.LFL`, offset in that file), the index's object table (`ReadIndexedObjects`), costumes with one byte per limb command.
- Walk boxes: `ScummV4DataSource.ReadRoom` read every BX block with the 20-byte v5 box; this layout stores 18-byte boxes (`ScummV4Archive.WalkBoxSize`, which `ReadWalkBoxes` already used for browsing). With the wrong size the first boot hung: Script 2's `walkActorToObject` in Room 73 never finished on garbage boxes (found with a watchdog probe: 25 278 script observations in 90 s, the last one `waitForActor`).
- `ScummProfile.EngineVersion` (new, additive): the engine version that runs the scripts. V3 for the room-file game (ScummVM's detection table runs indy3 VGA as version 3), else the container version. Follows it: old-v3 walking (`UsesV4Walking`), bits inside word variables (`SeparateBitVariables`), no v4 engine variables (`VAR_HEAPSPACE` would be var 40, one of Indy3's extra timers), no 800 zeroed globals, `actorFollowCamera` not starting a scene. `ExtraTimerVariables` (vars 39 to 41) now by family (Indy3Ega or Indy3Vga) instead of `GameId.IndianaJones3`, which the scanner does not give the VGA release. `VideoMode` stays 19 (VGA). `HasFirstVisitScenes` is lifted for the room-file layout with the Indy3 dialect (format and dialect, no game test).
- `ScummCostume.ReadVga(..., byteCommands)`: the animation's limb commands as bytes (ScummVM `ClassicCostumeLoader::costumeDecodeData`, version 3). Over every animation of every costume (1752), the byte layout draws a plausible figure 1720 times, the word layout 1408 times; 953 differ (`TheProfileGivesTheVersionThreeEngineAndTheRoomFileSource`).
- Harness `BootTrace` (beside this file, copied from the Monkey Island record and pointed at the room-file source) writes `boot-trace.txt` and `room-73-after-boot.png`.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummIndy3VgaSessionTests|...~ScummGameSessionTests|...~ScummMonkeyFloppy|...~ScummRoomFirstVisit|...~BrowsedMasks|...~Support|...~ScummSessionArchitecture|...~Profile"
# SCUMM_SOUND_TEST_ROOT=D:\Juegos\Lucas\loom ega mt32 en, SCUMM_INDY3_EGA_TEST_ROOT=D:\Juegos\Lucas\indy3 ega en
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom|...~FirstVisit|...~Costume|...~V4|...~WalkBox"
rtk.exe proxy dotnet build .verification\indy3vga-session-20261015\BootTrace\BootTrace.csproj
rtk.exe proxy dotnet Exe\BootTrace\Debug\net10.0-windows\BootTrace.dll "D:\Juegos\Lucas\indy3 vga en" .verification\indy3vga-session-20261015 900
```

## Results (numbers)

- Boot: **1002 frames** (limit 5000), 1.5 s; rooms entered **76, 79, 0, 6, 1, 73**; final room **73** (the college gym); Script 001 chains to Script 119 at +0x04B1; no cutscene open. 900 more frames leave Room 73 stable (pending: Script 17, Room 73 local scripts 200, 201, 202, 204, Script 119).
- Unsupported (opcode, operand or state the run could not follow): **0**. Operations reported as not applied: **0**. Assumed player input: **none** (the release has no copy-protection room: the index has no Room 92 and Script 001 loads none; the EGA boot goes 76, 79, 0, 6, 1, 92, 1, 73). 1637 seeded random picks over the boot and the 900 frames, each labelled an estimate.
- Interface variables set by the game: `VAR_VERB_SCRIPT` 4, `VAR_INVENTORY_SCRIPT` 12, `VAR_SENTENCE_SCRIPT` 2 (the same as EGA).
- Room 73 after the boot: 14 objects, 10 stored images, 2 actors drawn (the ego, costume 1, at (119,83); actor 9, costume 38, the boxer in the ring, at (256,86)), none skipped: `room-73-after-boot.png`.
- Tests: `ScummIndy3VgaSessionTests` (4) new; with Indy3 EGA, Monkey, first-visit, support, architecture and profile tests 58 passed. LOOM regression and neighbours (`Loom|FirstVisit|Costume|V4|WalkBox`): 863 total, 858 passed, 5 skipped (the usual skipped dumps), 0 failed.

## Comparison with ScummVM (source files and date; runtime comparison if any)

From memory of ScummVM `engines/scumm` (no fetch this run, 15 October 2026): `detection_tables.h` (indy3 VGA is version 3, not GF_OLD_BUNDLE), `vars.cpp` (`resetScummVars`: v4-only variables), `script.cpp` (`readVar`: bits of word variables up to version 3), `costume.cpp` (`costumeDecodeData`: one command byte per limb up to version 3), `scumm.cpp` (the Indy3 extra timers). No runtime comparison with the bundled ScummVM (not run, per the brief).

## Differences and uncertainties kept

- The AdLib music timer of the session reads the room files' sound blocks at the half tempo the browsed v4 disks use (an estimate set by ear for Monkey Island, 11 October); the opening's length rests on it.
- The ScummVM facts above were not re-read from the source this run; they agree with what the data shows (byte costume commands, 18-byte boxes, no Room 92).
- The ego's look after the boot is costume 1 as in the EGA test; the opening's walk positions follow the shared walk estimates.

## User checks

None required. Optional: look at `room-73-after-boot.png` against the real first room.

## Where the facts went

[Indy3 script map, VGA note](../../docs/indy3/INDY3_SCRIPT_MAP.md), [tracker, Indy3Vga block and decision log](../../docs/indy3/SUPPORT_STATUS.md), CHANGELOG 15 October.
