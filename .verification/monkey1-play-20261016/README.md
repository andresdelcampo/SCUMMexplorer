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
- Right click is not delivered (R-7.16): in the game's default two-button interface (`bit[0x223]` = 0, Script 4 +0x0000) the right click that uses an object's default verb is therefore not available; the left click with a chosen verb works, "Walk to" runs exits on one click (R-1.5), and the game's own ')' key switches to its one-button interface, where a left click with "Walk to" uses the default verb.
- Picking another room from the list enters it from the session's state (state preview), as the first visit does.
- The CD release (`MonkeyCd`, v5) and the eight-disk Spanish EGA release were not checked in play (S9).
