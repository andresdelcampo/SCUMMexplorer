# Indiana Jones and the Fate of Atlantis S4b: the generic session boots the second v5 game to its first playable room

| | |
|---|---|
| Family / stage | Indy4 / S4b |
| Fixture and edition | `Games/indy4 vga en` (read from `D:\Juegos\Lucas\indy4 vga en`): the English talkie CD, `ATLANTIS.cue`/`.img` (one data track with `ATLANTIS.000`/`.001`, no audio tracks). Chosen because `SupportConformanceTests` names it for Indy4; the Spanish classic and talkie copies are left to S9. |
| Date and author | 2026-10-17, S4b/S5 Indy4 agent |
| Commits | fb15da8 (soundKludge), cb75d20 (iMUSE lengths), 9efee28 (boot until control, tests), 9e47ba5 (encoding repair of two files), then this record's commit |
| Registry change | unchanged, because S4b changes no matrix cell |

## What was done

- Data source and dialect: no change needed. `ScummDataSources.For` already returns `ScummV5DataSource` for a v5 resource file and `ScummProfile.For` the `V5` dialect.
- Engine variables: the v5 set added for Monkey Island CD applies unchanged; `ScummEngine_v5::resetScummVars` sets nothing for `GID_INDY4` (the `_scummVars[74] = 1225` line is `GID_MONKEY` only), so `GameVariablesAtStart` stays empty.
- `ScummProfile.BootEndsWhenPlayerHasControl` also for Indy4: script 1 loads Barnett College (Room 4) with user input off; the opening (Room 4 local script 206) turns it on. Without it the boot stopped at Script 1's room load (197 frames, the opening not played).
- Executor: `o5_soundKludge` (0x4C) is the iMUSE command interface (`Sound::soundKludge`, `IMuseInternal::doCommand_internal`): commands 8, 9 and 10/11 with param 0 become the same start/stop/stop-all events as `startSound`/`stopSound`/`stopMusic`; every other command (priority, volume, fade, pan, jump, loop, hook, trigger queue) changes how a song sounds, not which sounds play, and is reported as a presentation operation not applied. Before: Room 68's exit script reported "operation 'soundKludge' is not modeled".
- Session sound clock: a v5 game's sound lengths (what `isSoundRunning` answers) come from the sound's iMUSE AdLib part through the S3 player's timeline (`ScummImuseTimeline`): a song that jumps back loops until stopped, else it ends at its timeline's end. The default path (hooks 0, markers and script-set loops not simulated) makes every length an estimate; `ScummSoundClock.Status` now reports a finite length that carries a reason as an estimate ("Sound 21 status is an estimate: iMUSE default path ..."). Before: "This is not an indexed old-bundle LOOM sound".
- Harness `Indy4BootTrace/` (copied from the MonkeyCd record; renamed so its output folder `Exe/Indy4BootTrace` does not collide): `Indy4BootTrace <folder> <out> [frames]`, `dump <folder> g:N | e:R | x:R | l:R:N | find:TEXT`, `sounds <folder> [ids]`.

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet build .verification\indy4-session-20261017\Indy4BootTrace\Indy4BootTrace.csproj
rtk.exe proxy dotnet Exe\Indy4BootTrace\Debug\net10.0-windows\Indy4BootTrace.dll "D:\Juegos\Lucas\indy4 vga en" .verification\indy4-session-20261017 0
rtk.exe proxy dotnet Exe\Indy4BootTrace\Debug\net10.0-windows\Indy4BootTrace.dll dump "D:\Juegos\Lucas\indy4 vga en" g:1 g:74 e:4 l:4:206 e:68 x:68 l:68:200 l:68:201
# SCUMM_INDY3_EGA_TEST_ROOT, SCUMM_SOUND_TEST_ROOT, SCUMM_MT32_ROM_ROOT set as in the brief
rtk.exe proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Indy4|...~Monkey|...~Indy3|...~ScummGameSession|...~FirstVisit|...~Play|...~Support|...~Architecture|...~Profile|...~ScriptReader|...~V5|...~Executor|...~SoundClock"
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom"
```

## Results (numbers)

- Boot: **721 frames** (limit 5000), about 1 s; rooms entered **68, 0, 4** (the LucasArts logo, no room, Barnett College); final room **4**; the player has control; no cutscene open; pending: Script 75, Room 4 local script 221, Script 79. Indy (actor 1, costume 2) at (117,138). `boot-trace.txt`, `room-04-after-boot.png`.
- **Unsupported: 0.** Operations reported as not applied: `roomOps` 15 runs, `soundKludge` 2 runs (the fade and priority commands of Room 68's exit), `actorFollowCamera` 2 runs. 37 seeded random picks.
- **Assumed player input: none.** Copy protection: script 1 takes the same branch for boot parameter 0 and for ScummVM's bypass value -7873 (0xE13F), so the talkie CD's boot meets no protection question; `ScummProfile.BootInput` stays null. No path choice (Team/Wits/Fists) is met by the boot: it comes later in the game.
- One assumed input inside the opening, labelled by the session: Script 79's `findObject` reads the virtual mouse, taken to rest below the room (160, 200).
- Before the fixes, in order: the boot stopped at Script 1's load of Room 4 (197 frames, user input still off, 1 unsupported: `soundKludge`); then 721 frames with control and 0 unsupported.
- Tests: `ScummIndy4SessionTests` (4) new and passing. Neighbour filter above: 225 passed, 0 failed (before S5). LOOM regression (`Loom`): 841 total, 836 passed, 5 skipped, 0 failed. Final runs after S5: see the objects record.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Read on GitHub `scummvm/scummvm` master, 17 October 2026 (through a fetch agent): `vars.cpp` (`setupScummVars`, `ScummEngine::resetScummVars`, `ScummEngine_v5::resetScummVars`, verbatim), `imuse/imuse.cpp` (`doCommand_internal`, verbatim), `scumm.cpp` (`runBootscript` verbatim; the `setupScumm` lines `if (!_copyProtection && _game.id == GID_INDY4 && _bootParam == 0) _bootParam = -7873;` came through the fetch tool's summary, not seen verbatim), `script_v5.cpp` (`OPCODE(0x4c, o5_soundKludge)` verbatim; the function body from the summary). Facts: [boot sequence, v5](../../docs/engine/SCUMM_BOOT_SEQUENCE.md). No runtime comparison with the bundled ScummVM (not run, per the brief).

## Differences and uncertainties kept

- iMUSE lengths are the default path's: a song a script loops, jumps or hooks may play longer or shorter in the original; scripts that poll `isSoundRunning` read labelled estimates.
- iMUSE player commands through `soundKludge` (volume, fades, jumps, hooks, the trigger queue) are not applied to the session's sound state; queued commands that would start or stop a sound on a marker are not simulated.
- `room-04-after-boot.png` shows a magenta line across the room at about y 134 (below the shelves). Not investigated: it may be a palette entry the original sets or cycles (the opening changes `roomIntensity`) or a background row the original covers. Listed as a user check.
- `VAR_SOUNDCARD` stays 3 (AdLib), as ScummVM gives a MIDI/AdLib Indy 4.
- The game's id shows as `Unknown` in the trace's first line (`GameId` has no Indy 4 member; the family is `Indy4` through `GameSupport.FamilyOf`).

## User checks (exact steps and where; none if none)

- Optional: compare `room-04-after-boot.png` with Barnett College right after Marcus' first line in the original (the magenta line, the lighting).

## Where the facts went (links to the script map / engine notes / tracker entries)

[Indy 4 script map](../../docs/indy4/INDY4_SCRIPT_MAP.md) (boot, Rooms 68 and 4, sounds), [boot sequence v5](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [tracker and decision log](../../docs/indy4/SUPPORT_STATUS.md), CHANGELOG 17 October.
