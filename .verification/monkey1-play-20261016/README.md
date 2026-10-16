# S6 Play, The Secret of Monkey Island floppy (16 October 2026)

Family `MonkeyFloppy`, fixture `D:\Juegos\Lucas\monkey1 ega en` (the four-disk 256-colour floppy release, SCUMM v4; `TestGames.Root`). Requirement: REQUIREMENTS §7.1, R-7.7 to R-7.18 (Proposed 15 October; no new row was needed). Reference: ScummVM `engines/scumm` master, read 16 October 2026 from raw.githubusercontent.com (no commit hash shown): `cursor.cpp` (`ScummEngine_v5::resetCursors`, `setBuiltinCursor`, `animateCursor`), `verbs.cpp` (`drawVerb`, `findVerbAtPos`, `verbMouseOver`, `checkExecVerbs`), `input.cpp` (`processKeyboard`, `processInput`), `vars.cpp` (`setupScummVars`, `resetScummVars`), `script_v5.cpp` (`o5_cursorCommand`, `o5_verbOps`, `o5_saveRestoreVerbs`), `scumm_v4.h` (`ScummEngine_v4 : ScummEngine_v5`). The bundled ScummVM was not run (brief).

## What was built or changed (shared play code)

| Piece | Where | ScummVM / game source |
|---|---|---|
| Text in play through one interface over the two charset renderers: v3 files (`99 - n`.LFL, 1 bit, EGA colour in the low 4 bits) and the v4 `CHAR` blocks of the archive or disks (`CharsetRendererClassic`: colour = palette index for pixel 1, the charset's colour map for the others) | `Rendering/IScummTextCharset.cs`, `V3/ScummV3Charset.cs`, `V4/ScummV4Charset.cs`, `Session/ScummSessionPlay.cs` (`PlayCharset`, by `EngineVersion`) | `drawVerb` → `drawString(4)`, `CharsetRendererClassic::printChar` |
| Each text verb drawn with the charset it was created with (`vs->charset_nr`), its rectangle `LineRows` high | `Rendering/ScummVerbArea.cs` (`ScummTextVerbDrawing.CharsetFor`) | `o5_verbOps` 9 (`charset_nr = _string[0]._default.charset`) |
| Messages on the play screen through the same interface, each in the charset it was printed with | `Rendering/ScummPlayScreen.cs` | `CharsetRendererClassic` |
| `VAR_CURSORSTATE` (52) and `VAR_USERPUT` (53) copied after every `cursorCommand` from version 4 | executor `PublishCursorVariables`, `ScummScriptRuntimeState.PublishesCursorVariables`, `ScummProfile.PublishesCursorVariables` | `o5_cursorCommand` end: `if (_game.version >= 4) { VAR(VAR_CURSORSTATE) = _cursor.state; VAR(VAR_USERPUT) = _userPut; }` |
| The engine's save/load menu key (`VAR_MAINMENU_KEY`, 50, version 4 and later; Monkey: F5 = 0x13F, Script 1 +0x077F) not delivered | `ScummSessionPlay.PressPlayKey`, `ScummProfile.MainMenuKeyVariable`, `ScummVars.MainMenuKey` | `processKeyboard` (`showMainMenu`) |
| The play cursor is cursor 0 of `resetCursors` (16 × 16 cross-hair, hotspot 8, 7) for every play game, versions 3 and 4 (the screen drew the v2 23 × 21 crosshair before; finding of S6 Indy3Vga) | `ScummPlayScreen.CursorPoints` | `ScummEngine_v5::resetCursors`, `setBuiltinCursor`; v3/v4 inherit them |
| `PlaysThroughVerbs` for MonkeyFloppy (the Indy3 families by dialect, from the merge with S6 Indy3Vga); the click mode 1 comes from `EngineVersion` 4 | `ScummProfile.For` | `checkExecVerbs` (`_game.version > 3`) |
| The App starts play from the v4 index (`ScummGameSession.BootForPlay`, from S6 Indy3Vga) | `App/MainWindow.Play.cs` | — |

## The acceptance route (R-7.18), headless

`tests/SCUMMexplorer.Tests/ScummMonkeyFloppyPlayTests.cs`:

```
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter FullyQualifiedName~ScummMonkeyFloppyPlayTests
```

| Step | Input (room point) | What the game's scripts did | Source |
|---|---|---|---|
| boot | – | Room 33 (the dock); Guybrush walks in from the cliffside to (346, 133) with input on; verb screen of Script 22 (twelve text verbs in the charset they were created with, sentence line 0x64 "Walk to"); room screen rows 0 to 143; input script 4; `VAR_CURSORSTATE` = `VAR_USERPUT` = 1 | Room 96 local script 200 +0x0020; Script 22; Script 12 |
| (1) compose a sentence | click "Look at" (verb 9), pointer on the poster (268, 104) | Script 23 (+0x0000 runs only while var 52 > 0) finds object 438 under the virtual mouse (`findObject` +0x002E), puts it in `var[0x06C]` (+0x010B), runs its verb 0x5A (default verb 9 into `var[0x0B5]`) and Script 12 names the line "Look at poster" | Script 23 +0x0000..+0x0151; Room 33 object 438 verb 0x5A; Script 12 +0x0017 |
| (1) run it | click on the poster | Script 4 area 2 mode 1: no second object needed, `doSentence 9, 438, 0` (+0x044B); sentence script 2 walks Guybrush to the poster and starts object 438 verb 9 | Script 4 +0x03E0..+0x044B; Script 2 +0x016C..+0x039D |
| (5) skip a cutscene | Esc (`PressKey(0x1B)`) once the override is set | the poster's first look is `cutscene` + `beginOverride` (+0x00D4) with "Re-elect Governor Marley…"; Esc aborts to +0x0134 (`endCutscene`), the second line is never said | Room 33 object 438 verb 9 +0x00CF..+0x0134 |
| (2) leave the room | walk right until the door shows; "Open" + click on the door (716, 108); click on it again ("Walk to") | object 437 verb 1 starts Script 25 (opens it, state 1); verb 0x0A: `ifState 437, 1` then `loadRoomWithEgo 0x142, 0x1C` → Room 28 (the SCUMM Bar); its input script is Room 28 local 202, which chains to 4 | Room 33 object 437 verbs 0x01, 0x0A; Room 28 entry +0x009A |
| (4) a dialogue | "Talk to" + the important-looking pirates (428, 104) | sentence `0x0D, 332`; object 332 verb 0x0D starts Room 28 local script 220 (the conversation, while `var[0x0C3]`, the trials done, is not above 3); it sets `VAR_VERB_SCRIPT` = 14 and offers "I want to be a fireman.", "I mean to kill you all!", "I want to be a pirate." as verbs 0x78 to 0x7A (charset 1, Script 17 case 5; keys '1'..) | Room 28 object 332 verb 0x0D +0x0000..+0x0007; Room 28 local script 220; Script 17 +0x00C7 |
| (4) the choice | click on verb 0x7A | Script 14 prints the choice as Guybrush's line (`printEgo` code 5 of the chosen verb, +0x00F7) and puts 0x7A in `var[0x0C1]` (+0x0105); the pirates answer ("So what?" … the three trials … "GROG!!!"), then offer the next choices; "I'll just be running along now." (0x7F) ends it and the input script returns to 202 | Script 14 +0x007F..+0x0105 |
| out again | "Open" + "Walk to" the bar's door (object 322); the archway of the dock (object 436); "Open" + "Walk to" the door of the street (object 452) | Room 28 object 322 verb 0x0A (the first exit runs Script 117 once, `bit[0x1BE]`) → Room 33; object 436 → Room 35; object 452 → Room 29 | Room 28 object 322 verb 0x0A +0x0000..+0x0019; Room 33 object 436 verb 0x0A; Room 35 object 452 verb 0x0A +0x002B..+0x0031 |
| (3) take an object | "Pick up" + the rubber chicken (248, 116) | sentence `0x0B, 391`; object 391 verb 0x0B `pickupObject var[0x007]`; the inventory script 9 names slot 0x65 "chicken" (code 6 of `var[0x086]`) and turns it on; Guybrush says "Maybe no one will miss just this one thing…" | Room 29 object 391 verb 0x0B; Script 9 +0x0048..+0x00B6 |

All steps pass. Every input was a click or a verb's key; no key reached area 4 on the route and the boot's assumed protection answer is not among the player's inputs (R-15.7: the session starts from the booted state; the answer is never shown as the user's choice). The SCUMM Bar's mugs (objects 373 to 377) cannot be the object of step 3: the index gives them class 24 (untouchable) and Room 28's entry keeps them so until the three trials are done (`var[0x0C3]` > 3), which is why the route goes on to the rubber chicken.

A second test pins the keys: F5 (the main menu key) and the pause and restart keys are refused; 'l' is "Look at"'s click `(1, 9, 1)`. A third pins the cursor shape (`CursorPoints`).

**The same route in ScummVM (described, not run):** after the title Guybrush walks in from the cliff path onto the dock; Look at the poster says "Re-elect Governor Marley." and Esc skips the rest; Open and Walk to the SCUMM Bar's door enters the bar; Talk to the important-looking pirates starts the "I want to be a pirate" conversation with the three trials; leaving the bar, the street's archway leads up to Mêlée's high street, the door of the Voodoo shop leads in, and Pick up chicken takes the rubber chicken with a pulley. This is the game's own flow as decoded above; no runtime comparison was made.

## WPF check through the real window

`.verification/monkey1-play-20261016/PlayWindowCheck` (private profile through `SCUMM_EXPLORER_PROFILE_ROOT` in the output folder), run 16 October:

```
rtk proxy dotnet build .verification/monkey1-play-20261016/PlayWindowCheck/Monkey1PlayWindowCheck.csproj
rtk proxy Exe\Monkey1PlayWindowCheck\Debug\net10.0-windows\Monkey1PlayWindowCheck.exe "D:\Juegos\Lucas\monkey1 ega en" .verification\monkey1-play-20261016\window
```

Result (`window/play-check.txt`, captures `window/play-0*.png`): play started in Room 33 with the room view replaced by the game screen and the system pointer hidden, Normal-mode note empty and no tooltip; the twelve verbs and the sentence line drawn in the game's v4 charset and colours; Guybrush walked in to (346, 133) under the window's own timer (86 frames); "Look at" + pointer on the poster → sentence line "Look at poster", "Look at" in its highlight colour (Script 23 `verbOps … color 0x0E`) and the v4 cross-hair over the poster (capture 1); the click ran the poster's cutscene ("Re-elect Governor Marley.", capture 2); Esc through the window's own key handler (`PlayKeyDown`) skipped it; 'l' through the window's text-input handler was delivered as `runInputScript(1, 9, 1)`. The clicks were given to the window's session at screen points (the call the image's mouse handler makes after mapping the pointer); the mouse mapping itself is the user's check.

## Regression

LOOM and the generic-session tests (filter `Loom|FirstVisit|ScummGameSession|MonkeyFloppy|Indy3|ScummEngineOpcode|Support|Verb|Charset|Message|Architecture|Play`, `SCUMM_SOUND_TEST_ROOT=D:\Juegos\Lucas\loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT=D:\Juegos\Lucas\indy3 ega en`): **913 passed, 5 skipped, 0 failed** after the merge with S6 Indy3Vga and the cell change; support tests 33 of 33 with the regenerated matrix; `ScummIndy3PlayTests`, `ScummIndy3VgaPlayTests` and the three MonkeyFloppy play tests pass. Before the merge with S6 Indy3Vga the same set without `Play` gave 896 passed, 5 skipped, 0 failed. LOOM is unchanged by construction: the cursor variables are copied only from engine version 4 in the generic session (LOOM CD keeps its own session), the main-menu key exists only from version 4, and the charset interface keeps the v3 drawing byte for byte (`DrawPlainLine` with the low 4 bits).

## Differences and uncertainties (kept)

- The pointer, the clicks' timing and the keys are the player's (assumed input when driven headless); perceived timing and audio are the user's checks (no sound plays during play yet).
- A verb's rectangle is its drawn width by the charset's font height (`LineRows`), not ScummVM's `_charset->_str` of the drawn pixels; a click on the last row of a glyph that hangs below the font height would miss.
- `getActorFromPos` (Script 23 for Talk to / Give) uses the session's costume drawing, as for Indy3.
- Monkey Island's Script 4 key 0x17 (Ctrl+W) asks "Are you sure you want to win? (Y/N)" and starts the ending: it is the game's own key and is delivered (R-7.11); nothing in the explorer presses it.
- Right click: superseded by the addendum below (16 October, R-7.16 Rule).
- Picking another room from the list enters it from the session's state (state preview), as the first visit does.
- The CD release (`MonkeyCd`, v5) and the eight-disk Spanish EGA release were not checked in play (S9).

## Addendum, 16 October: right click reaches the game (R-7.16, Rule)

User decisions of 16 October: "Right click goes to the game in verb-based games, as in ScummVM; keep the Interact menu elsewhere.", then "why not use right click only in the games that need it? -instead of everywhere?". ScummVM master (`input.cpp` `processInput`, `verbs.cpp` `checkExecVerbs`, read 16 October 2026): from version 4 a right press is `MBS_RIGHT_CLICK` and reaches the input script as the click with mode 2 (`(_game.version > 3) ? (left ? 1 : 2) : 0`); up to version 3 it is Esc outside LOOM. `ScummProfile.RightClickReachesGame` (play through verbs and engine version 4 or later) turns it on for this game only among the played ones.

- **What it does here:** Script 4 +0x0000 `equalZero bit[0x223]` (two-button interface, the default), +0x0005 `isEqual local[0x002], 2` (a right click), +0x000C `equalZero var[0x06E]` (no second object pending), +0x0011 `notEqualZero var[0x0B5]` (the hovered object has a default verb, set by Script 23 +0x0128..+0x0142 from the object's verb 0x5A), +0x0016 `move var[0x06B] = var[0x0B5]`; the area 2 path then runs `doSentence var[0x06B], var[0x06C], var[0x06D]` (+0x044B) and sentence Script 2 runs the object's verb. On the poster (object 438, default verb 9) a right click looks at it. In the one-button interface (')' key) Script 4 skips this and the right click is a plain click (+0x0034 on).
- **Headless:** `ScummMonkeyFloppyPlayTests.ARightClickOnAnObjectRunsItsDefaultVerb`: `runInputScript(2, 0, 2)`, then "sentence script 2 runs verb 0x09 with objects 438" and "Re-elect Governor Marley." on screen; the only delivered input is the right click.
- **Window:** the PlayWindowCheck now ends with a right click on the poster through the window's own right-button path (`PlayRightClick`, called by the image's `MouseRightButtonDown` after mapping the pointer): `runInputScript(2, 0, 2) right click in the room at screen (84, 104), room (268, 104); Look at poster sentences 1 -> 2; messages [Re-elect Governor Marley.]` (`window/play-check.txt`, `window/play-04-right-click.png`). A first run of the check missed the poster with the earlier left click (the camera was still moving when the check computed its point; the check's own timing, not the app's) and was rerun.
- **Interact menu:** the menu belongs to the LOOM session (its **Interact ▴** button and right click, R-6.9); Play in this game never offered it, so nothing moved. The check reports the button hidden during play.
- Not done: ScummVM's both-buttons-at-once = Esc (version 4) is not modelled (the app's Esc/Skip gives it).

## Addendum, 16–17 October: sound during play (shared by Indy3 EGA, Indy3 256 colours and Monkey Island floppy)

Reference: ScummVM `engines/scumm` master read 16 October 2026 from raw.githubusercontent.com (no commit hash shown): `scumm.cpp` (`setupMusic`), `vars.cpp` (`setSoundCardVarToCurrentConfig`), `sound.cpp` (`triggerSound`, `soundKludge`, `isSoundRunning`, `stopSound`, `stopAllSounds`, `updateMusicTimer`, `convertADResource`, `readSoundResourceSmallHeader`), `players/player_ad.cpp` (`startSound`, `getSoundStatus`, `stopAllSounds`, `getMusicTimer`). The "no sound plays during play yet" line above is superseded.

Already there and now used in play: the generic session runs `startSound`, `stopSound` and `isSoundRunning` on its AdLib sound clock (`ScummSoundClock`; R-13.9 constants by `ScummProfile.AdlibTiming`) and keeps `VAR_MUSIC_TIMER` (`ScummSessionFrames.UpdateMusicTimer`); the boots already waited on them (Indy3's opening music 35 and its timer marks, Monkey Island's credits music 110). A scan of every global, entry, exit and local script of the three games finds **no** `soundKludge`, `stopMusic` or `startMusic` (ScummVM's `soundKludge` only queues iMuse commands); `startSound`/`stopSound`/`isSoundRunning` occur 138/86/26 times (Indy3 EGA), 137/87/26 (256 colours), 135/68/24 (Monkey Island).

| Piece | Where | ScummVM source |
|---|---|---|
| What a music start stops, by profile: Indiana Jones (both releases) the previous music, Monkey Island floppy every sound; the generic session puts the engine's stop on `SoundEvents` just before the start (labelled `Player_AD startSound` or `triggerSound`). LOOM's session is unchanged | `ScummProfile.MusicStart`, `ScummGameSession.BeforeSoundStarts`, `ScummSession.EngineStopsSounds`, `ScummSoundClock.IsMusic`/`Playing` | `setupMusic`: DOS + AdLib + (LOOM or Indy3) → `Player_AD`, other DOS v3+ → iMuse; `Player_AD::startSound`: byte 2 = 0x80 is music, `stopMusic()` first; `triggerSound`: Monkey EGA/VGA with iMuse, `stopAllSounds()` before any resource that is not `ASFX`; `convertADResource`: AD with 0x80 is `ADL ` music, else `ASFX` |
| The play window plays the session's sound events through the device the user picked (the browsing setting): `SceneSoundPlayer` follows any `ScummSession` and renders with the sound player's own rendition (`RenderSoundWave`, AdLib at +6 dB). The boot's sounds are not played; frames wait while a started track renders (as LOOM); Pause (Space) pauses the sounds with the clock; picking another room, another kind of resource or another game stops them; Skip (Esc) only aborts the cutscene and the scripts' own stops follow (as LOOM) | `App/SceneSoundPlayer.cs`, `App/MainWindow.Play.cs` (audio output only), `App/MainWindow.xaml.cs` (`RenderSoundWave`; another kind of resource now calls `StopPlay`, before it the game ran on unseen) | — |

**Sound ids on the routes** (pinned by `ScummIndy3PlayTests.AssertRouteSounds`, shared by both Indy3 releases, and `ScummMonkeyFloppyPlayTests.AssertRouteSounds`; `ScummPlaySoundTests` pins the profile rule and that the bar's entry does not start its music again while it plays):

| Game | Step | Sound | Script and instruction |
|---|---|---|---|
| Indy3 (both) | boot | 35 opening music | Room 76 local script 201 +0x0023 |
| Indy3 EGA | boot | 30 copy-protection screen | Room 92 local script 204 +0x0005 (and object 929's fallback verb +0x0005) |
| Indy3 (both) | Close the door | 3 | Script 27 +0x002B |
| Indy3 (both) | the chain takes the mallet back | 13 | Room 73 local script 205 +0x0058 |
| Indy3 (both) | Push the switch, lights back | 13 twice | Room 73 object 837 verb 0x06 +0x0002 and +0x0063 (EGA) / +0x02B0 (256 colours) |
| Indy3 (both) | Open the door | 2 | Script 26 +0x002B |
| Monkey | boot | 110 credits, 108 lookout, 104 title (awaited by `isSoundRunning` +0x000D, stopped +0x0017: no stall) | Script 149 +0x001D; Room 38 local 203 +0x000E; Room 96 local 200 +0x0009 |
| Monkey | boot | the engine stops every sound before 104 (108 is stopped by no script) | `triggerSound` workaround at Room 96 local 200 +0x0009 |
| Monkey | Open a door (dock, street) | 2 | Script 25 +0x0034 |
| Monkey | enter the SCUMM Bar | 101 bar music (`var[0x04B]` = 0x65, Script 1 +0x0466) | Room 28 entry +0x008A, after `isSoundRunning` +0x0080 |
| Monkey | leave the bar | stop 101; first exit's Script 117 plays 100 and stops it | Room 28 object 322 verb 0x0A +0x0006; Script 117 +0x0031, +0x053E |
| Monkey | the shop of Room 29 | 3 (door), 115 | Script 26 +0x002B; Room 29 local script 200 +0x0021 |

**WPF check** (`SoundWindowCheck/`, private profile, run 17 October; `sound/sound-check.txt`):

```
rtk proxy dotnet build .verification/monkey1-play-20261016/SoundWindowCheck/PlaySoundWindowCheck.csproj
Exe\PlaySoundWindowCheck\...\PlaySoundWindowCheck.exe "D:\Juegos\Lucas\indy3 ega en" "D:\Juegos\Lucas\monkey1 ega en" .verification\monkey1-play-20261016\sound
```

Result: Indy3 play in Room 73 with the scene-sound player following the play session; Close + door → Script 27 +0x002B sound 3 started on AdLib and its wave opened (0.50 s; labelled "Estimated preview route", the EGA route's label after the boot's assumed protection answer); opening Monkey Island stopped it ("play was left"); Monkey play from the dock, Room 28 picked from the list → the dock's exit script stops 117/118, the bar's entry starts 101 (music, AdLib, wave opened, 180 s preview length) and the session has it running; Space paused and resumed the sounds with the game; a sound picked in the tree stopped the play timer and its sounds. The agent cannot hear: how they sound is the user's check.

Tests: the three play classes, `ScummPlaySoundTests`, sound/AdLib/support/architecture/session tests: 86 passed; LOOM filter: 836 passed, 5 skipped, 0 failed (after the merge with the R-7.16 right-click work).

Kept differences: the scripts always see an AdLib card (`VAR_SOUNDCARD` 3, S4b decision) and `isSoundRunning`/the music timer follow the AdLib clock whatever the output device (ScummVM uses `Player_V2` for the PC speaker; not modelled); Monkey Island's music is an iMuse song in ScummVM (looping unless stopped), the session's clock and the rendition use the AD block at the browsed half tempo (S3 estimate); a rendered AdLib track lasts at most three minutes (the sound player's preview length), so a looping piece falls silent after that; tracks overlap as separate players (no OPL channel sharing, no three-slot effect limit of `Player_AD`); Indy3's AdLib rendition still uses LOOM's driver constants (R-13.9 S3 polish). Roland is not offered for these games (no MT-32 data, as when browsing). The App stops its own voices on a room-list pick while the session may still count a global sound as running.

**Listening checks (the user's):**
1. Device AdLib. Open `indy3 ega en`, pick Room 73. Click Close, then the door on the left twice: a short door sound. Push the light switch (left wall, after the door closes): a click as the lights go out, another as they come back (Esc skips to that). Open + the door: the opening sound. Nothing should sound at the moment play starts.
2. Same in `indy3 vga en` (no copy-protection room in this release's boot).
3. Open `monkey1 ega en`, pick Room 33 (the dock): silent at the start. Open + Walk to the SCUMM Bar's door: the door sound, then the bar's music as Guybrush enters. Press Space: the music pauses with the game; Space again: it goes on. Talk to the important-looking pirates: the music goes on under the conversation (the route's conversation branch has no sound command). Leave through the bar's door: the music stops, and the first exit's scene (Script 117, Rooms 70 and 72) plays its own piece. Pick Room 29 from the list (or walk there): the shop's music.
4. Switch the device to PC speaker during play: current sounds stop; the next ones play on the speaker. Choose No Sound: nothing plays and the game goes on.
5. Pick a sound or a script in the tree during play: the game's sounds stop.
