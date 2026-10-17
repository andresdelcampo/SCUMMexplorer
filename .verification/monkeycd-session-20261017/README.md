# Monkey Island CD S4b: the generic session boots the first v5 game to its first playable room

| | |
|---|---|
| Family / stage | MonkeyCd / S4b |
| Fixture and edition | `Games/monkey1 vga en` (read from `D:\Juegos\Lucas\monkey1 vga en`): The Secret of Monkey Island CD, English, `The_Secret_of_Monkey_Island.cue`/`.bin` (ISO 9660 data track with `MONKEY.000`/`MONKEY.001`, 24 audio tracks) |
| Date and author | 2026-10-17, S4b MonkeyCd agent |
| Commits | the code commit "S4b MonkeyCd: v5 data source, …", then the record/docs commit |
| Registry change | unchanged, because S4b changes no matrix cell (Objects stays partial until S6) |

## What was done

- `ScummV5DataSource` (Core/Scumm/V5), returned by `ScummDataSources.For` for a game whose archive is a `ScummV5Archive`: global scripts (`SCRP` through `DSCR`), entry/exit (`ENCD`/`EXCD`) and local scripts (`LSCR`, numbered from 200), objects (`OBCD` > `CDHD`, `VERB`, `OBNA`), object images in state order with their `ZP01` masks, walk boxes (`BOXD` + `BOXM`, handed to the shared reader as a v4 `BX` body), scale slots (`SCAL`), costumes (`COST`, the shared VGA reader) and the index's object table (`DOBJ`). New archive readers in `ScummV5Archive.Session.cs`. The profile already gave the `V5` dialect.
- `ScummVars`: the v5 slots 54, 56 to 72 (vars.cpp). `ScummProfile.EngineVariablesAtStart` for version 5: as v4 plus `VAR_INPUTMODE` 3, `VAR_FADE_DELAY` 3, `VAR_V5_TALK_STRING_Y` -0x50, and `GameVariablesAtStart` (edition data): MonkeyCd's `var[74]` = 1225 (`GID_MONKEY`). `BootEndsWhenPlayerHasControl` also for MonkeyCd.
- CD music (`ScummCdAudioTracks`, data-driven): a sound whose `SOUN` block names a disc track gets its length from the cue sheet (or loops for ever); its start resets `VAR_MUSIC_TIMER` to 0 and the session counts it up by the frame's jiffies (ScummVM soundcd.cpp `playCDTrack`, `cdTimerHandler`); a script's stop of that sound stops the count; one track at a time. Shared additions, small: an optional by-id sound lifetime in the `ScummSession` constructor and an `AfterScriptStopsSound` hook.
- Script reader (v5 only): `o5_drawObject` with its sub-opcode (0x05/0x45/0x85/0xC5) and `o5_pickupObject` (0x25/0x65/0xA5/0xE5: object, room). Without it Room 38's entry decoded `pickupObject` as a three-operand `drawObject` and stopped at +0x0043 ("verbOps subopcode 0x00"). Executor: the v5 `pickupObject` (no room or owner check, untouchable class 32), nested `getInventoryCount` in `expression` (Script 9 +0x0000 reported "expression operands are unsupported"). `getActorFromPos` is answered in every generic session (Script 23 +0x008B reported "not modeled").
- Harness `BootTrace` (beside this file, copied from the Indy3Vga record): `BootTrace <folder> <out> [frames]` writes `boot-trace.txt` and the room picture; `BootTrace dump <folder> g:N | e:R | x:R | l:R:N | v:R:OBJ:VERB(hex) | find:TEXT`; `BootTrace cd <folder>` lists the CD-track sounds.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummMonkeyCdSessionTests"
# SCUMM_INDY3_EGA_TEST_ROOT, SCUMM_SOUND_TEST_ROOT, SCUMM_MT32_ROM_ROOT set as in the brief
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Monkey|...~Indy3|...~ScummGameSession|...~FirstVisit|...~Play|...~Support|...~Architecture|...~Profile|...~ScriptReader|...~V5|...~Executor"
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom"
rtk.exe proxy dotnet build .verification\monkeycd-session-20261017\BootTrace\BootTrace.csproj
rtk.exe proxy dotnet Exe\BootTrace\Debug\net10.0-windows\BootTrace.dll "D:\Juegos\Lucas\monkey1 vga en" .verification\monkeycd-session-20261017 0
rtk.exe proxy dotnet Exe\BootTrace\Debug\net10.0-windows\BootTrace.dll "D:\Juegos\Lucas\monkey1 vga en" <scratch> 900
```

## Results (numbers)

- Boot: **1573 frames** (limit 5000), about 1 s; rooms entered **0, 10, 0, 0, 38**; final room **38** (the lookout); the player has control; no cutscene open; pending: Script 159, the interface loop Script 23, Room 38 object 486's fallback verb (waiting for sound 108). `boot-trace.txt`, `room-38-after-boot.png` (Guybrush, the lookout and the fire, three actors drawn, none skipped).
- **Unsupported: 0.** Operations reported as not applied: `roomOps` (3 runs, display only). Assumed player input: **none** (no copy protection; `ScummProfile.BootInput` stays null). 5 seeded random picks in the boot.
- Before the fixes, in order: the boot hung in Room 10 at the 5000-frame limit (the credits wait for `VAR_MUSIC_TIMER` > 0xFA, which nothing advanced); then ended in Room 38 with 2 unsupported (`actorFromPos`, the misread `pickupObject`); then 1 (nested `getInventoryCount`).
- Left alone: 900 more frames end the lookout's track (sound 108, CD track 17 from frame 7173), object 486 loads Room 96 (sound 104, track 6), whose local script 200 puts the ego into Room 33 at (346,133) with track 22 looping: `room-33-after-900-frames.png` (5 objects, 2 stored images, 1 actor). 905 seeded picks over boot and 900 frames, unsupported 0.
- Interface variables set by the game: `VAR_VERB_SCRIPT` 4 after the boot (0xC9 during the credits).
- Tests: `ScummMonkeyCdSessionTests` (5) new and passing. Session/play/first-visit/support/architecture/profile/reader/executor neighbours (filter above): 191 passed, 0 failed. LOOM regression (`Loom`): 841 total, 836 passed, 5 skipped (the usual dumps), 0 failed. Rerun after merging master (S7 MonkeyFloppy, 4990902), filter `Loom|Monkey|Indy3|ScummGameSession|FirstVisit|Play|Support|Architecture|Profile|ScriptReader|V5|Executor|Cutscene`: 950 total, 945 passed, 5 skipped, 0 failed.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Read on GitHub `scummvm/scummvm` master, 17 October 2026: `vars.cpp` (`setupScummVars`, `resetScummVars`, `ScummEngine_v5::resetScummVars`), `script_v5.cpp` (`setupOpcodes` 0x05/0x25/0x45/0x65/0x50/0x85/0xA5, `o5_pickupObject`, `o5_actorFromPos`), `sound.cpp` (`updateMusicTimer`), `soundcd.cpp` (`cdTimerHandler`, `startCDTimer`/`stopCDTimer`, `playCDTrack`, `playCDTrackFromSoundID`). `o5_drawObject`'s sub-opcodes are from memory of the same file (not re-read). `getTimerFrequency` was not re-read: the 240 Hz engine timer (one CD-timer step per jiffy) is the value LOOM CD's session already uses. Facts: [boot sequence, v5](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [script reference](../../docs/engine/SCUMM_SCRIPT_MAP.md). No runtime comparison with the bundled ScummVM (not run, per the brief).

## Differences and uncertainties kept

- The credits' and the lookout's timing rest on the CD timer at one step per jiffy and on the cue sheet's track lengths; the real drive's start latency and track 17's user offset (`start += 75 * adjustment / 100`) are not modelled (offset 0).
- The trace line for a CD sound's end says "completed on its native AdLib resource clock" (the shared clock's wording); the clock used is the track length.
- The boot ends at the first moment the player has control (Room 38). The brief expected Room 33 (the floppy's end): on the CD the lookout itself is playable while its track plays; Room 33 follows only when the player waits. Whether a click at the lookout behaves as the original is S6.
- Room 38 shows 0 stored images (its 4 objects have no `OBIM` images that decode, or none): not checked against ScummVM (S5).
- `VAR_V5_TALK_STRING_Y` is set for version 5 only, though ScummVM sets it for 4 too (decision log).
- The v5 untouchable class is 32 in `pickupObject`; the shared hit test still uses the small-header 24 (S5/S6 item).
- ~3% of v5 script blocks remain unresolved for browsing (S2 polish); none was reached by the boot.

## User checks (exact steps and where; none if none)

None required. Optional: compare `room-38-after-boot.png` with the lookout at the end of the opening in the original.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Monkey Island script map, CD section](../../docs/monkey1/MONKEY1_SCRIPT_MAP.md), [boot sequence v5](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [script reference, v5 slots](../../docs/engine/SCUMM_SCRIPT_MAP.md), [tracker and decision log, MonkeyCd block](../../docs/monkey1/SUPPORT_STATUS.md), CHANGELOG 17 October.
