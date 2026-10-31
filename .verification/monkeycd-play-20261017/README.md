# Monkey Island 1 CD S6: play through the game's own interface

Family `MonkeyCd`, fixture `Games/monkey1 vga en` (`The_Secret_of_Monkey_Island.cue`/`.bin`, English, SCUMM v5), 17 October 2026. Requirements: REQUIREMENTS §7.1 R-7.7 to R-7.18 (R-7.16 confirmed), R-1.13, R-15.7/R-15.8.

## What was done

- **The route of R-7.18** (`tests/SCUMMexplorer.Tests/ScummMonkeyCdPlayTests.cs`, `TheAcceptanceRouteFromTheDockRunsThroughTheGamesOwnInterface`). The play boot (`ScummGameSession.BootForPlay`, the window's path) ends at the lookout (Room 38) with the player in control. The game then goes on by itself, as the S4b record shows, to the title (Room 96) and the dock (Room 33): Guybrush ends at (346, 133) 220 frames later. From there, with the CD's own ids (the floppy's verb and object numbers differ):
  1. **Compose and run a sentence.** Clicking "Look at" (verb 8) gives the sentence "Look at". The pointer over the poster (object 429; Script 23 finds it) gives "Look at poster". Script 12 joins the verb (code 5), string 49 (code 7, " ") and the object (code 6). The click runs `runInputScript(2, 0, 1)`; Script 4 runs `doSentence`; sentence Script 2 runs verb 0x08 with object 429.
  2. **Skip a cutscene.** The poster's first look is a cutscene with an override (object 429 verb 8 +0x00CF to +0x0134, "Re-elect Governor Marley."). Esc skips it, and the second line never shows.
  3. **Leave the room.** The route clicks "Open" (2) on the door (object 428), then the door itself, which uses the default Walk to (0x0B). This reaches Room 28, the SCUMM Bar, whose input script is its local script 202.
  4. **Hold a dialogue.** The route clicks "Talk to" (0x0A) on the important-looking pirates (object 322). Script 14 becomes the input script and lists the three lines as verbs 0x78 to 0x7A. The route picks 0x7A, "I want to be a pirate."; Guybrush says it, and the pirates answer through to "the three trials" and the grog chant. In the next choice list the route picks 0x7F, "I'll just be running along now.", which ends the conversation.
  5. **Go to the shop.** The route opens and uses the bar's door (315); Script 120 plays the flashback (Rooms 70 and 72) and returns to the dock. From there it takes the archway (427) to Room 35, then opens and uses Room 35's door (444) to reach Room 29.
  6. **Take an object.** The route clicks "Pick up" (9) on the rubber chicken (377), and it reaches the inventory. Inventory Script 9 gives slot 0xC8 the chicken's icon: object 961 of Room 99, named by the object's verb 0x5B in `var[0x178]`. The chicken becomes untouchable with class 32.

  Every input was a click with mode 1, and no key reached area 4. The play screen draws the room, the verbs and the v5 cross-hair at the pointer.
- **Right click** (R-7.16), `ARightClickOnAnObjectRunsItsDefaultVerb`. With the pointer on the poster, the right click is delivered as `runInputScript(2, 0, 2)`. Script 4 +0x0016 takes the poster's default verb from `var[0x0B6]` (8), and sentence Script 2 runs Look at.
- **Keys.** `VAR_MAINMENU_KEY` (319) is not delivered, nor are Esc and '.'. A verb's own key is that verb's click.
- **Charsets.** `ScummV5Archive.ReadCharset` reads `DCHR`/`CHAR`, giving charsets 1 (8 rows), 2 (9 rows) and 6 (14 rows, the verbs). `cursorCommand charsetColors` from Script 22 (+0x0003) now replaces colour-map entries.
- **Sound in play.** Sounds that start a disc track (the music: 100, 101, 104, 108, 110, 115, 122, …) play that stretch of the disc. The audio is the same as the room's music panel plays, whatever the device: `ScummCdAudioTracks.ReadWave`, `MainWindow.Play.RenderPlaySound`. A track with loop count 0xFF restarts when it ends, until a script stops it (`SceneSoundPlayer`, `loopsForever`). Sound effects (2, 3) are iMuse sounds of the resource file and play through the chosen device, as for browsing. Per `sound.cpp`, the CD release has no "new music stops every sound" rule; that rule is for `GID_MONKEY_VGA`/`EGA` only.
- **Engine changes, all from profile data, with no family test outside `ScummProfile.For`:**
  - `UntouchableClass`: 32 from version 5.
  - `VerbImagesFromNamedRoom`: `setVerbObject` without the small header.
  - `BootForPlay` reads a v5 index.
  - Code 7 in play verb names.
  - `charsetColors`.
  - `PlaysThroughVerbs` for MonkeyCd.

## Commands run (through RTK)

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx
& '...rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummMonkeyCd|FullyQualifiedName~ScummMonkeyFloppy|FullyQualifiedName~Indy3|FullyQualifiedName~PlaySound|FullyQualifiedName~Cutscene|FullyQualifiedName~SupportConformance|FullyQualifiedName~Architecture"
# with SCUMM_INDY3_EGA_TEST_ROOT, SCUMM_SOUND_TEST_ROOT, SCUMM_MT32_ROM_ROOT set:
& '...rtk.exe' proxy dotnet test ... --filter "FullyQualifiedName~Indy3|FullyQualifiedName~PlaySound|FullyQualifiedName~Loom|FullyQualifiedName~Scumm"
# SCUMM_UPDATE_SUPPORT_MATRIX=1 for the matrix, then the support tests again without it
& '...rtk.exe' proxy dotnet build .verification/monkeycd-play-20261017/PlayWindowCheck/MonkeyCdPlayWindowCheck.csproj
Exe\MonkeyCdPlayWindowCheck\Debug\net10.0-windows\MonkeyCdPlayWindowCheck.exe "D:\Juegos\Lucas\monkey1 vga en" <scratch folder>   # private SCUMM_EXPLORER_PROFILE_ROOT inside it
```

## Results (numbers)

- `ScummMonkeyCdPlayTests`: 4 of 4 pass (route, right click, keys, charsets).
- Monkey floppy, Indy3, play sound, cutscene, conformance and architecture tests: 138 of 138 pass. The Indy3, LOOM and Scumm regression with the data roots set: 1055 pass, 5 skipped, 0 failed.
- Route timing:

  | Point on the route | Frame |
  |---|---|
  | The dock | 220 |
  | The poster's cutscene | 240 |
  | The SCUMM Bar | 336 |
  | The dialogue's choices | 457 |
  | The conversation's end | 1331 |
  | Back on the dock | 2365 |
  | Room 35 | 2450 |
  | Room 29 | 2523 |
  | The chicken taken | 2560 |

- Route sounds, executed:

  | Sound | Event | Source | CD track |
  |---|---|---|---|
  | 110 | start | Script 152 +0x0049 | 17 |
  | 108 | start | Room 38 local 203 +0x000E | 17 from frame 7173 |
  | 104 | start / stop | Room 96 local 200 +0x0027 / +0x0033 | 6 |
  | 122 | start | Room 33 entry +0x0048 | 22, repeats |
  | 122 | stop | Room 33 exit +0x0000 | |
  | 2 | start | Script 25 +0x003A | effect, not a track |
  | 101 | start | Room 28 entry +0x0071 | 8, repeats |
  | 101 | stop | object 315 verb 0x0B +0x000C | |
  | 100 | start / stop | Script 120 +0x0036 / +0x053B | 12 |
  | 3 | start | Script 26 +0x0031 | effect, not a track |
  | 115 | start | Room 29 local 200 +0x0021 | 1, still playing at the end |

- The real window (`window/play-check.txt`, captures `window/play-0*.png`):
  - Play started in Room 38 and the system pointer is hidden.
  - The window reached the dock after 220 frames.
  - The verb area draws the nine verbs, the empty inventory grid (slots 0xC8 to 0xCF, arrows 0xD0/0xD1) and the sentence line.
  - "Look at" then the poster gives `runInputScript(1, 8, 1)`, then `(2, 0, 1)`. The sentence line reads "Look at poster", the cutscene appears, and Esc through `PlayKeyDown` skips it.
  - The key 'l' is the shortcut of verb 8.
  - A right click through `PlayRightClick` gives `runInputScript(2, 0, 2)`; the Look at poster sentences go from 1 to 2.
  - Scene-sound log: 104 opened (13.2 s, CD audio), then 122 opened (145.3 s, CD audio).

## Comparison with ScummVM (source files and date; runtime comparison if any)

ScummVM `engines/scumm` master, read 17 October 2026: `object.cpp` (`getClass`, `findObject`), `verbs.cpp` (`setVerbObject`, `checkExecVerbs`), `cursor.cpp` (`resetCursors`, `setBuiltinCursor`, `default_cursor_colors`), `sound.cpp` (`triggerSound`, `stopSound`), `soundcd.cpp`, `script_v5.cpp` (`o5_cursorCommand`, `o5_pickupObject`), `charset.cpp`/`string.cpp` (`convertStringMessage`, `setCurID`). The facts are in `docs/engine/SCUMM_SCRIPT_MAP.md`, section "Play input, verbs, classes and charsets (v5)".

The bundled ScummVM was not run (the brief says not to). The same route side by side in ScummVM is a user check (below).

## Differences and uncertainties kept

- **The lookout is on screen for one window frame only.** The boot's headless frames already run the lookout's track (sound 108) on the session clock. The window therefore starts play when that track has ended, and Room 96 follows at once. In the real game the player can walk at the lookout for about a minute. This is a consequence of S4b's boot-ends-at-control rule and is left for the owner of the boot.
- **Dialogue line order.** The first choice list puts 0x79 "I mean to kill you all!" at row 145, 0x7A at 153 and 0x78 at 161, as the script placed them. This was not compared with ScummVM.
- **Verb colours** follow the room palette's indices 2, 3, 6 and the charset's own entry 3 (the outline). Whether they look like the original is a user check.
- **`charsetColors` is a shared change.** It also reaches the floppy's verbs wherever its scripts use the command. The floppy tests pass; its look was not re-captured.
- **Code 7** is modelled for play verb names only. Messages that hold code 7 are unchanged and keep their old note.
- **Not needed by the route:** Room 99 (only its object images are read, for the icons) and Rooms 88 and 89.
- **Timing** of walking, messages and music sync is the user's check (R-2.4).

## User checks (exact steps and where; none if none)

1. **Listening and timing.**
   - Steps: open `D:\Juegos\Lucas\monkey1 vga en` and select Room 38 in the list. Play starts and reaches the dock with the dock's music, which is CD track 22 and repeats. Click "Look at", then the poster, and press Esc in the cutscene. Click "Open" on the door, then the door. In the SCUMM Bar its music should play (CD track 8), and the pirates' dialogue should play through.
   - Check that each music starts with its room and stops when you leave, and that the door creak (sound 2) plays on your device.
2. **Look.**
   - Steps: compare the verb area (colours of verbs and sentence line), the inventory icon after "Pick up" on the rubber chicken in Room 29, and the cursor with the bundled ScummVM at the same places.
3. **Right click.**
   - Steps: right-click the poster on the dock.
   - Expected: Guybrush walks to it and looks at it.

## Where the facts went (links to the script map / engine notes / tracker entries)

- [Script map, CD section](../../docs/monkey1/MONKEY1_SCRIPT_MAP.md): verbs and the input chain (CD), objects and classes (CD), sounds (CD).
- [Engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md), section "Play input, verbs, classes and charsets (v5)".
- [Tracker and decision log](../../docs/archive/monkey1/SUPPORT_STATUS.md), MonkeyCd block.
- Registry: MonkeyCd Objects ✔ and Play ✔ (`GameSupport.Families`; README and REQUIREMENTS tables regenerated).
