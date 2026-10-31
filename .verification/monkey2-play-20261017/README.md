# Monkey Island 2 S6: play through the game's own interface

Family `Monkey2`, fixture `Games/mi2 talkie en` (`MONKEY2.000`/`.001`, DOS release with the fan "Talkie CD 0.2 beta" patch, English, SCUMM v5), 17–18 October 2026. Requirements: REQUIREMENTS §7.1 R-7.7 to R-7.18 (R-7.16 confirmed), R-1.13, R-15.7.

## What was done

- **Two shared play additions** (also needed by S6 Indy4, made first in their own commits):
  - **(a) The play screen draws each v5 object with the image of its current state** (`ScummPlayScreen.Scene`, `ScummV5DataSource.ReadObjectImages(room, states)`, which now decodes a room's state images once). Before, play drew the first stored image (IM01) whatever the state. Commit 9b28623; test `ScummPlayScreenStateImageTests` (Room 51's object 0x24D in state 3 shows IM03).
  - **(b) iMUSE music during play.** The songs a v5 game's scripts start (`startSound`, `soundKludge` 8; stopped by `stopSound`, `soundKludge` 9, 10, 11) already played through the S3 iMUSE player of the device the user picked (`RenderSoundWave`: `ScummImuseMt32` with the user's MT-32 ROMs, `ScummImuseAdlib`, `ScummImuseSpeaker`). They pause with the game, stop on a room-list change and when play is left, as before. What was missing is looping. The S3 renderers unroll a looping song to about three minutes (`ScummImuseTimeline`), but in the game such a song loops until a script stops it. `ScummImuseTimeline.RestartSeconds` and `ScummImuseMusic.Loop(sound, device)` now give each device part's loop window. `SceneSoundPlayer` takes a `Loop(Restart, End)` per sound and device (it replaces the old loops-for-ever flag) and goes back to the loop point when the wave reaches the loop end. A CD track still restarts from 0 at its end. Commit c3b1c5b; tests `ScummImuseMusicTests` (68 of the first 200 MI2 sound ids loop on AdLib) and two `V5ImuseLoopTests`. The loop point is an estimate: markers and script-set player commands are not followed.
- **iMUSE jump hooks in the session** (commit 4a950b5, shared and additive). Without them the play route could not start. After the boot, Room 4's local script 204 has Guybrush walk away from the campfire, arms jump hook 1 of the campfire song (sound 108, `soundKludge [0x10C, 108, 0, 1]` at +0x016A) and waits for the song to end (`soundKludge [0x0D, 108]` loop, +0x01A0..+0x01BA). The session's clock had the song loop for ever, so the game stayed in that cutscene. Now `Player::setHook` class 0 for a playing song rebuilds the song's AdLib timeline from the second the hook was armed (`ScummImuseTimeline.BuildWithHook`):
  - A hook marker (sysex 0x7D 48) with that id is taken; an id below 0x80 is used up by the jump.
  - A jump to another track of the MIDI file plays that track's rest (`ScummImuseSong.Parse(part, track)`). Sound 108's hook-1 markers (every 2.8 s) jump to track 1, beat 4, tick 400.
  - The sound clock retimes the voice (`ScummSoundClock.Retime`), so `isSoundRunning` and the status command follow it. Sound 108 now ends at 98.97 s and the game goes on by itself to Woodtick (Room 7, frame 324 of play).
  - Test: `V5ImuseLoopTests.AnArmedJumpHookIsTakenFromTheSecondItWasArmedAndCanEndTheSong`.
- **Text verbs skip the two bytes of codes 9, 10, 12, 13 and 14** (`ScummV4Charset.DrawPlainLine`, as ScummVM's `drawString`; commit d5af7d3). The talkie patch's dialogue choices start with code 10 (a voice cue) and end with code 9, and play drew those bytes as glyphs.
- **The route of R-7.18** (`tests/SCUMMexplorer.Tests/ScummMonkey2PlayTests.cs`, `TheAcceptanceRouteFromWoodtickRunsThroughTheGamesOwnInterface`). The play boot (`BootForPlay`, the window's path) ends at the campfire (Room 4). The game then goes on by itself, as above, to the edge of Woodtick (Room 7, Guybrush at (696, 53)). From there:
  1. **Compose and run a sentence; skip a cutscene.** Clicking "Look at" (verb 8) gives "Look at". With the pointer on the sign (object 46) the line reads "Look at sign", and a click runs `runInputScript(2, 0, 1)`. Script 4 runs `doSentence`, and sentence Script 2 runs object 46's verb 8. It prints "`Welcome to Woodtick`" and opens a cutscene with an override (+0x0028..+0x002F). Esc (`abortCutscene`) skips "`No Trezer huntin zone`" (+0x0034).
  2. **Take an object.** "Pick up" (9) on the sign: object 46 verb 9 +0x000C `pickupObject 0x2D`, so the shovel (45) reaches the inventory with the "Oops… I think I broke the sign" cutscene. Inventory Script 9 shows it in a slot as Room 94's object 911 (named by object 45's verb 0x5B, `var[0x12E]` = 0x038F).
  3. **A dialogue.** A click to the west walks Guybrush to the bridge, where Largo stops him (Room 7 local script 203, then 204). Script 14 becomes the input script and lists four choices as verbs 0x78 to 0x7B. The route picks 0x78, "Is this some sort of bribe situation?" (204 +0x00BE). Largo hangs Guybrush upside down, and Room 7's local script 206 becomes the input script for the second list. The route picks "Then who keeps up the law and maintains order?" (204 +0x0509), which Script 206 prints through code 5. Largo takes the riches (objects 49 to 56), and the shovel stays.
  4. **Leave the room.** The route walks back west and takes the dinky door (object 41) with the default "Walk to" (0x0B, +0x0023 `loadRoomWithEgo 0x40, 8`). This reaches Room 8, the map shop, with Guybrush at (51, 112).

  Every input was a click with mode 1, and no key reached the game except the Esc skip.
- **Right click** (R-7.16), `ARightClickOnTheSignRunsItsDefaultVerb`. A right click on the sign is `runInputScript(2, 0, 2)`. Script 4 +0x0005 takes the default verb from `var[0x09C]` (the sign's verb 0x5A sets 8), and the sign is looked at.
- **Play screen**, `ThePlayScreenDrawsWoodtickWithTheVerbsAndTheCursor`. It draws Room 7, the verbs, the inventory icons and the cross-hair.
- **Registry:** `PlaysThroughVerbs` for Monkey2 (`ScummProfile`). Monkey2 **Objects ✔ and Play ✔** (`GameSupport.Families`; README and REQUIREMENTS tables regenerated with `SCUMM_UPDATE_SUPPORT_MATRIX=1`).

## Commands run (through RTK)

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx
& '...rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Imuse|FullyQualifiedName~Monkey|FullyQualifiedName~Indy|FullyQualifiedName~PlaySound|FullyQualifiedName~Cutscene|FullyQualifiedName~SupportConformance|FullyQualifiedName~Architecture|FullyQualifiedName~PlayScreen|FullyQualifiedName~Sound"
# with SCUMM_INDY3_EGA_TEST_ROOT, SCUMM_SOUND_TEST_ROOT, SCUMM_MT32_ROM_ROOT set:
& '...rtk.exe' proxy dotnet test ... --filter "FullyQualifiedName~Indy3|FullyQualifiedName~PlaySound|FullyQualifiedName~Loom|FullyQualifiedName~Scumm|FullyQualifiedName~Imuse"
# SCUMM_UPDATE_SUPPORT_MATRIX=1 for the matrix, then the support tests again without it
& '...rtk.exe' proxy dotnet build .verification/monkey2-play-20261017/PlayWindowCheck/Monkey2PlayWindowCheck.csproj
Exe\Monkey2PlayWindowCheck\Debug\net10.0-windows\Monkey2PlayWindowCheck.exe "D:\Juegos\Lucas\mi2 talkie en" <scratch folder>   # private SCUMM_EXPLORER_PROFILE_ROOT inside it
```

## Results (numbers)

- `ScummMonkey2PlayTests`: 3 of 3 pass (route, right click, play screen). `ScummPlayScreenStateImageTests` 1, `ScummImuseMusicTests` 2, `V5ImuseLoopTests` (3 new) pass.
- Play, sound, iMUSE, Monkey, Indy, cutscene, conformance and architecture filter: 219 of 219 pass (before the registry change). The Indy3, LOOM, Scumm and iMUSE regression with the data roots set (`SCUMM_INDY3_EGA_TEST_ROOT`, `SCUMM_SOUND_TEST_ROOT`, `SCUMM_MT32_ROM_ROOT`), after the registry change: 1101 passed, 5 skipped, 0 failed in 4 min 4 s. The test host then stayed alive without exiting and was stopped by hand about 45 minutes later. The results had all been reported; the cause, a foreground thread left by some test, was not looked into.
- Route timing:

  | Point on the route | Frame |
  |---|---|
  | Woodtick (Room 7), player in control | 324 |
  | The sign's cutscene | 338 |
  | The shovel taken, quiet again | 405 |
  | Robbed; input script back to Script 4 | 1047 |
  | Room 8 | 1189 |

- Route sounds (session events; all `Conditional`, because the boot's route followed a seeded pick, so the scene sound player labels them "Estimated preview route" in its log):

  | Sound | Event | Source |
  |---|---|---|
  | 169 | start / stop | Script 93 +0x0014 / +0x004C |
  | 107 | start | Room 04 entry script +0x006D (the Woodtick music, 233.6 s of AdLib) |
  | 6 | start | Room 07 object 46 verb 9 +0x0010 (the sign breaks) |
  | 151 | start | Room 07 local script 203 +0x0075 (Largo's theme, 190.5 s) |
  | 101 | start | Room 07 local script 204 +0x0B09 |

- The real window (`window/play-check.txt`, captures `window/play-0*.png`):
  - Play started in Room 4 with the system pointer hidden and no Normal-mode note. The scene sound player follows the session on AdLib.
  - The window reached Woodtick after 324 frames.
  - The verb area draws the nine verbs, the inventory icons (Room 94 objects 990, 989, 995, 991, 992, 993, 994, 996) with arrows 0xD0/0xD1, and the sentence line.
  - "Look at" then the sign gives `runInputScript(1, 8, 1)` and `(2, 0, 1)`, and the sentence line reads "Look at sign". The cutscene is skippable, and Esc through `PlayKeyDown` ends it.
  - "Pick up" then the sign puts the shovel's icon 911 in slot 0xCC.
  - A right click through `PlayRightClick` gives `(2, 0, 2)` and "`Welcome to Woodtick`".
  - A walk west opens Largo's dialogue: Script 14 with choices 0x78 to 0x7B. A click on 0x78 makes Guybrush say "Is this some sort of bribe situation?".
  - Scene-sound log: 169, then 107 (233.57 s), 6 and 151 (190.49 s) opened as AdLib waves.

## Comparison with ScummVM (source files and date; runtime comparison if any)

ScummVM `engines/scumm` master, read 17 October 2026:
- `imuse/imuse.cpp`: `doCommand_internal`, param 1 cases 12/20 `setHook(a[2], a[3], a[4])`, case 13 a volume fade; param 0 case 13 `getSoundStatus_internal`, which in MI2 ignores players fading out.
- `HookDatas::set`.
- `imuse/imuse_player.cpp`: `maybe_jump`, `jump` (`setTrack`, `jumpToTick`), `decode_sysex_bytes`.
- `imuse/sysex_scumm.cpp` (case 48).
- `imuse/imuse_internal.h` (`setHook`, `HookDatas`).
- `string.cpp` (`drawString`: codes 9, 10, 13, 14 skip two bytes, 12 sets the colour).
- `object.cpp` (`drawObject`, `getObjectImage`).
- The input chain as in S6 MonkeyCd (`verbs.cpp`, `input.cpp`, `script_v5.cpp`, `cursor.cpp`; facts in `docs/engine/SCUMM_SCRIPT_MAP.md`).

The bundled ScummVM was not run (the brief says not to). The same route side by side in ScummVM is a user check (below).

## Differences and uncertainties kept

- **Sound labels.** All play sounds after the boot are `Conditional` (the boot's route took a seeded pick), so `SceneSoundPlayer` logs them as "preview start", with the estimated-route note in Advanced. They play all the same. `RenderPlaySound` names iMUSE songs "effect" in the log, because the v5 sound's music flag is the AdLib bundle's. Both are log labels only.
- **Loop points are estimates**: hooks other than an armed jump hook, markers, the trigger queue and player commands (volume, parts, transposes, fades) are not followed. Sound 108's hook-1 path (the end at 98.97 s) is our reading of the data, not compared with ScummVM.
- **MI2's status ignores fading players** (`getSoundStatus_internal` with `ignoreFadeouts` for `GID_MONKEY2`). This is not modelled: a song fading to volume 0 (`[0x10D, 107, 0, 60]` at Room 4 local script 203 +0x001F) still counts as playing until the session's clock ends it. The route does not depend on it.
- **Dialogue choice text** starts with character 7 after the code-10 cue (the talkie patch). It is drawn as the charset's glyph 7, and the spoken line also starts with it ("\x07Then who keeps up…"). The plain copies (S9) may not have it.
- **Not run:** the window check used AdLib (the private profile's default). Roland MT-32 and PC speaker in play go through the same `RenderSoundWave` path, which was not listened to here.
- **Timing** of walking, messages and the music's loop points are the user's check (R-2.4).

## User checks (exact steps and where; none if none)

1. **Music in play.**
   - Steps: open `D:\Juegos\Lucas\mi2 talkie en` and choose Roland MT-32 (ROMs in `D:\Juegos\Lucas\mt32`), then AdLib. Select Room 4 in the list. Play starts at the campfire and Guybrush walks to Woodtick, where the Woodtick music (sound 107) starts.
   - Check that it keeps looping (it is 233 s long before its loop point) and that it pauses with Space and stops when another room is picked.
   - Walk west to the bridge: Largo's theme (151) should start.
2. **Route side by side with ScummVM.**
   - Steps: Look at the sign (Esc in the cutscene), pick up the sign, walk west, choose "Is this some sort of bribe situation?" and then "Then who keeps up the law…", then go through the small door to the map shop.
   - Check the dialogue lines' order, the inventory icon and the verb colours.
3. **Right click.**
   - Steps: right-click the sign at Woodtick.
   - Expected: Guybrush looks at it.

## Where the facts went (links to the script map / engine notes / tracker entries)

- [Script map](../../docs/monkey2/MONKEY2_SCRIPT_MAP.md): verbs and the input chain, Woodtick objects, sounds 107, 108, 151.
- [Engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md), section "Play input, verbs, classes and charsets (v5)": iMUSE jump hooks, text-verb codes.
- [Tracker and decision log](../../docs/archive/monkey2/SUPPORT_STATUS.md).
- Registry: Monkey2 Objects ✔ and Play ✔.
