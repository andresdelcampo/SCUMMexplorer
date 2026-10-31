# Monkey Island 2 S4b: the generic session boots LeChuck's Revenge to the campfire on Scabb Island

| | |
|---|---|
| Family / stage | Monkey2 / S4b |
| Fixture and edition | `Games/mi2 talkie en` (read from `D:\Juegos\Lucas\mi2 talkie en`), the conformance fixture named in `SupportConformanceTests` (`GameFamily.Monkey2`). DOS `monkey2.000`/`monkey2.001` with the fan "Talkie CD 0.2 beta" patch (Script 1 +0x01C5 says so; `monster.sou` speech). The other local copies (`mi2 mt32 en`, `mi2 sb16 en`, `mi2 classic es`, `mi2 talkie es`) are S9 work. |
| Date and author | 2026-10-17, S4b/S5 Monkey2 agent |
| Commits | 2bd0dc9 (reader), 9c47419 (soundKludge), 18bd3d3 (state images), cbafbd5 (session/profile), b5e07c6 (tests, harness); record commit after them |
| Registry change | unchanged, because S4b changes no matrix cell |

## What was done

- Data source: `ScummDataSources.For` already returns `ScummV5DataSource` for the v5 archive (MonkeyCd's); nothing game-specific was added. The profile gives the `V5` dialect.
- Script reader (v5 only): `roomOps` sub-opcode 4 is `setPalColor` (three words, a second sub byte, the index; ScummVM `o5_roomOps` SO_ROOM_PALETTE without `GF_SMALL_HEADER`). Before, Script 6 +0x0007 read two words and the rest of the script was misdecoded ("or reads unknown destination var[0x401]").
- Executor: `o5_soundKludge` (`ScummBoundedScriptExecutor.SoundKludge.cs`): the word list goes to iMUSE `doCommand`, the answer to `VAR_SOUNDRESULT`. Start (8), stop (9) and stop-all (11) are sound events like `startSound`; the status (13) comes from the session's sound clock; a player command (high byte 1) for a song that is not playing answers -1 (`findActivePlayer`); player commands for a playing song, the marker trigger queue (14, 15, 16, 23) and getParam are answered 0 **as labelled estimates** (the song clock does not follow them). `[-1]` flushes.
- Sound clock: a v5 sound's length is its iMUSE AdLib part (ROL when there is none) unrolled by `ScummImuseTimeline` (S3): a song that jumps back loops until a script stops it, any other ends with its last event. This is what `isSoundRunning` and the soundKludge status read. Monkey Island CD's tracks keep the CD clock.
- Object images: a v5 object stores one image per state (`IMnn`). `ScummV4ObjectImage.State` keeps the number; `ScummV5DataSource.ReadObjectImages(room)` gives one image per object (IM01) and `ReadObjectImages(room, states)` the one each state shows (`drawObject` finds `IMnn` with n = state). Room 108 has 15 images for object 1154; the views key on the object id and threw on duplicates.
- Boot input: `ScummBootInputPlan.ClickObject` (edition data in `ScummProfile`): the boot, standing still 400 frames, clicks the middle of that object's box through the game's input script (`ClickScreen`, mode 1), as a player would; a click plan presses no key. Monkey2: object 0x484 (1156), "Monkey Island 2" (full game). `BootEndsWhenPlayerHasControl` also for Monkey2.
- Harness `BootTrace` (beside this file, copied from the MonkeyCd record; `o:<room>` lists a room's records and images).

## Commands run (through RTK)

```powershell
rtk.exe proxy dotnet build SCUMMexplorer.slnx
rtk.exe proxy dotnet build .verification\monkey2-session-20261017\BootTrace\BootTrace.csproj
rtk.exe proxy dotnet Exe\BootTrace\Debug\net10.0-windows\BootTrace.dll "D:\Juegos\Lucas\mi2 talkie en" .verification\monkey2-session-20261017 0
rtk.exe proxy dotnet Exe\BootTrace\Debug\net10.0-windows\BootTrace.dll dump "D:\Juegos\Lucas\mi2 talkie en" g:1 g:128 g:130 g:131 g:6 o:108
rtk.exe proxy dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummMonkey2"
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Monkey|...~ScriptReader|...~Executor|...~Support|...~FirstVisit|...~Play|...~ScummGameSession|...~Profile|...~V5|...~Architecture|...~Indy3"
rtk.exe proxy dotnet test ... --filter "FullyQualifiedName~Loom"
```

## Results (numbers)

- Boot: **3409 frames** (limit 5000), about 2 s; rooms entered **108, 0, 87, 0, 3, 103, 109, 0, 5, 4**; final room **4** (the campfire on Scabb Island beach, `room-04-after-boot.png`: Guybrush and one listener, 2 actors drawn, none skipped); the player has control (user input on, no cutscene). Pending: Script 22, Room 4 local script 205, Room 4 entry script.
- **Unsupported: 0.** Operations reported as not applied: `roomOps` (27 runs, display only). 780 seeded random picks (most from Script 130's random wheel draws).
- **Assumed player input: one click** on object 1156 in Room 108 at screen (28, 64), delivered as `runInputScript(2, 0, 1)` to Script 131 (`VAR_VERB_SCRIPT` = 0x83), which takes the "Monkey Island 2" branch (+0x006E: `bit[0x237]` = 0, `var[0x062]` = 0, string 0x1A "Regular Mode", `var[0x06A]` = -2), releasing Script 130 +0x034A.
- **No copy protection in this edition at boot parameter 0**: Script 128 +0x0000 jumps to +0x0248 when `local[0]` is 0 (the "LAME-O COPY PROTECTION" recipe screen runs only for other boot parameters), and Script 130 +0x005F skips its recipe check likewise. ScummVM passes boot parameter 0 for DOS MI2 (it only sets -7873 for the Mac release and Indy 4).
- `monkey2.cfg` does not exist: `roomOps loadString 0x1B` leaves string 0x1B as Script 1 wrote it ("E`@@@@@@@"), as ScummVM's SO_LOAD_STRING does when the file is missing; so `bit[0x24B]` = `bit[0x24C]` = 1 from its first character.
- Before the fixes, in order: `soundKludge` not modelled (Script 1 +0x0089, frame 0); the boot hit the frame limit waiting in Room 108 for the choice; the picture of Room 108 threw on duplicate object ids; Script 6 misdecoded (`or var[0x401]`); with the click the boot ended in Room 87 when Script 1 ended (before `BootEndsWhenPlayerHasControl`).
- Tests: `ScummMonkey2SessionTests` (5) new and passing. Neighbour filter above: 222 passed. LOOM regression (`Loom`): 841 total, 836 passed, 5 skipped (the usual dumps), 0 failed.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Read on GitHub `scummvm/scummvm` master, 17 October 2026: `scumm.cpp` (`runBootscript`, `_bootParam` and `_copyProtection` in the constructor and `setupScumm`), `script_v5.cpp` (`o5_soundKludge`, `o5_isSoundRunning`, `o5_roomOps` cases 13/14, every `GID_MONKEY2` special case), `vars.cpp` (`ScummEngine_v5::resetScummVars`: nothing for MI2), `imuse/imuse.cpp` (`doCommand_internal`, `getSoundStatus_internal`, `enqueue_trigger`, `enqueue_command`, `handle_marker`, `getMusicTimer`), `imuse/imuse_player.cpp` (`getMusicTimer`, `getParam`), `sound.cpp` (`soundKludge`, `processSoundQueues`, `updateMusicTimer`). `o5_roomOps` sub-opcode 4's v5 form is from memory of the same file (not re-read). No runtime comparison with the bundled ScummVM (not run, per the brief).

## Differences and uncertainties kept

- The engine writes `VAR_SOUNDRESULT` when it processes its queue (frame end, or a `[-1]` list); the session writes it at the command. Same value whenever the script flushes before reading (the pattern seen).
- iMUSE player commands, hooks with an id other than 0, markers and the trigger/command queue are not followed: their answers are 0 and labelled; a song's length is the one its own data gives with no script around (S3's timeline). Scripts that steer a song (Script 52 queues commands on sound 154's markers) run on that estimate.
- `VAR_MUSIC_TIMER` is not driven by the iMUSE song for v5 (ScummVM: the playing players' tick × 2 / PPQN, scaled by the timer frequency); the boot did not wait on it.
- `getActorScale` (S5) reads the scale `actorOps` set; walk-box scaling of `_scalex` is not modelled.
- ScummVM's MI2 workarounds that touch the boot route were not needed (Largo's theme delay of sounds 103/110 while 151 plays; `isEqual VAR_SOUNDCARD` 5).
- The boot ends at the first moment the player has control (Room 4). Whether this matches the original's first playable moment at the campfire is a user check.
- The fan talkie's speech (`monster.sou`) is not played by the session (no effect on state).

## User checks (exact steps and where; none if none)

Optional: compare `room-04-after-boot.png` with the campfire scene at the end of the opening in the original; check that the Lite/full choice screen (Room 108) is the one the game shows after the LucasArts screen.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Monkey Island 2 script map](../../docs/monkey2/MONKEY2_SCRIPT_MAP.md) (boot rows), [boot sequence v5](../../docs/engine/SCUMM_BOOT_SEQUENCE.md), [tracker and decision log](../../docs/archive/monkey2/SUPPORT_STATUS.md), CHANGELOG 17 October.
