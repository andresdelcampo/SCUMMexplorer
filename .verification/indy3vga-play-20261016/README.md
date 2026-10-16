# S6 Play, Indiana Jones and the Last Crusade in 256 colours (16 October 2026)

Family `Indy3Vga`, fixture `D:\Juegos\Lucas\indy3 vga en` (English, one `NN.LFL` per room, v4-style blocks, run by ScummVM as version 3; `TestGames.Root`). Requirement: REQUIREMENTS §7.1, R-7.7 to R-7.18 (Proposed 15 October, Indy3Ega; kept as written). Reference: ScummVM `engines/scumm` master, read 16 October 2026 (no commit shown on the raw files): `cursor.cpp` (setBuiltinCursor, resetCursors, animateCursor), `verbs.cpp` (drawVerb, restoreVerbBG, redrawVerbs, verbMouseOver, checkExecVerbs), `charset.cpp` (CharsetRendererV3::setColor, printChar, setCurID; CharsetRendererPC::drawBits1), `palette.cpp` (updatePalette), `script_v5.cpp` (o5_roomOps SO_ROOM_PALETTE, o5_verbOps new), `scumm.cpp` (ScummEngine_v3/v4 constructors, `_shadowPalRemap`), `gfx.cpp` (drawStripToScreen). The bundled ScummVM was not run (brief). Indy3 EGA record: `.verification/indy3-play-20261015/README.md`.

## What changed

| Piece | Where | Source |
|---|---|---|
| Play is turned on by format and dialect, not by family: the Indy3 dialect run by the generic session (old-bundle v3 or one file per room); F5 (319) stays undelivered for both releases | `ScummProfile.PlaysThroughVerbs`, `UndeliveredKeys` | Scripts 2, 4, 12, 16, 18, 20, 23, 24, 27, 29, 96, 113 compared instruction by instruction with EGA (script map, VGA note) |
| Click mode from the engine version: 0 (the container is v4, the engine version 3) | `ScummProfile.ClickInputMode` | `checkExecVerbs`: `code = (_game.version > 3) ? ... : 0` |
| The charset of a version 3 engine: `99.LFL` − n (identical bytes in both releases) | `ScummSessionPlay.PlayCharset` (one line) | `CharsetRendererV3::setCurID` (1 bpp, no colour table) |
| The whole screen shows the shadow palette: verbs, messages and cursor follow `roomOps roomPalette` like the room | `ScummPlayScreen.Compose` (one line) | `updatePalette`: `if (_shadowPalRemap) data = _currentPalette + _shadowPalette[i] * 3`; `_shadowPalRemap` true for v3 outside Mac b/w, CGA, Hercules; `o5_roomOps` case 4 (small header): `_shadowPalette[b] = a; setDirtyColors(b, b)` |
| Play boots from the game's own index: `00.LFL` for old-bundle games, the v4-style index with the game's data source otherwise | `ScummGameSession.BootForPlay`, `MainWindow.Play.cs` (StartPlayAsync) | as the first visit (`ScummRoomScene`) |

**Screen composition, as ScummVM draws it for this version.** Verb colours are raw indices into the 256-colour palette (`drawVerb`: dim colour when `curmode == 2`, else highlight when the pointer is over it and `hicolor` is set, else `color`; `o5_verbOps new` gives 2, 14 (version 3), 8). Text colours: `CharsetRendererV3::setColor` with `GF_OLD256` sets a shadow for bit 0x80 and keeps `color & 0x7F`; no `_roomPalette`/`_shadowPalette` lookup in the charset, shadow colour 0 (`CharsetRendererPC::setShadowMode`). The scripts of this release use only colours 0x00–0x0F and 0x8x, where the shared renderer's EGA rule (`& 0x0F`, 0x80 shadow) gives the same pixels, so the renderer was not changed. The room palette's first 16 entries are the EGA colours, so verbs and messages look as in EGA. Cursor: `default_cursor_colors = {15, 15, 7, 8}` as raw palette indices (no remapping for an 8-bit PC game), one step every two frames (`animateCursor`).

## The acceptance route (R-7.18), headless

`tests/SCUMMexplorer.Tests/ScummIndy3VgaPlayTests.cs` (the route itself is `ScummIndy3PlayTests.RunAcceptanceRoute`, shared with EGA: Room 73 and Room 1 have the same objects, boxes and walk points):

```
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Indy3VgaPlay|FullyQualifiedName~ScummIndy3PlayTests"
```

Result: 5 of 5 passed (EGA route, VGA route, VGA profile, VGA screen palette, VGA light switch).

| Step | Input (screen point) | What the game's scripts did (VGA) | Source |
|---|---|---|---|
| boot | – | Room 73, input script 4, sentence "Walk to", 12 verbs on, Talk/Travel dim, rows 16–143; charset 0 loaded | Script 1 (VGA offsets, S4b record); Script 96; Script 16 |
| (1) compose a sentence | "Pick up" (105, 161), the mallet (176, 128) | sentence line "Pick up mallet"; every click delivered with mode 0 | Script 4 +0x0114-equivalent (+0x011E), Script 20, Script 16 |
| (1) run it, (3) take an object | second click on the mallet | sentence script 2 runs verb 0x0B with object 836; inventory [836], verb 0x65 "mallet"; meanwhile Indy says the VGA-only F5 hint | Script 4 doSentence +0x0267; Room 73 object 836 verb 0x0B; Script 12; Script 119 +0x01A2 |
| the game's interruption | "Close" the door | "The mallet's chained to the bell." and the mallet goes back (frame 70), as EGA | Room 73 local 205 |
| (5) skip a cutscene | "Close" the door, "Push" the switch (24, 108) | cutscene; the gym darkens through 15 `roomOps roomPalette` loops (verb colour 2 is shown as 0xB2, tested); override at +0x024B; Esc → abortCutscene → skip jump to +0x02A7; slots restored | Room 73 object 837 verb 0x06 (VGA code, script map) |
| (2) leave the room | "Open" the door, a click on it | `loadRoomWithEgo` → Room 1 at frame 152 | Room 73 object 830 verb 0x0A |
| (4) a dialogue | "Talk", Marcus twice | sentence `0x0D, 0, 4`; Script 29 installs input script 18 and offers 0x78–0x7A | Script 2 +0x0041; Script 29 |
| (4) the choice | verb 0x79 | "Why Marcus, have you lost your sense of humor?", var[0x072] = 0x79, Marcus: "You'll have to be the judge of that." (frame 423) | Script 18; Script 29 |

The app chose no verb, object, sentence or answer; no key reached area 4 on the route.

**The same route in ScummVM (described, not run):** ScummVM runs this release as version 3 with the same scripts, so the route is EGA's (see that record) with three visible differences, all from this release's own scripts: Indy greets the player and mentions F5 in the gym (Script 119), the light switch dims the gym by palette remapping rather than switching the lights off (the whole screen, verbs and text included, darkens because `_shadowPalRemap` is on), and Script 9's save screen lists hint-book numbers. No runtime comparison was made.

## WPF check through the real window

The EGA harness is generic and was reused unchanged (`.verification/indy3-play-20261015/PlayWindowCheck`, private profile through `SCUMM_EXPLORER_PROFILE_ROOT` in the output folder):

```
rtk proxy dotnet build .verification/indy3-play-20261015/PlayWindowCheck/Indy3PlayWindowCheck.csproj
rtk proxy Exe\Indy3PlayWindowCheck\Debug\net10.0-windows\Indy3PlayWindowCheck.exe "D:\Juegos\Lucas\indy3 vga en" .verification\indy3vga-play-20261016\window
```

Result (`window/play-check.txt`, exit 0): play started in Room 73 with the room view replaced by the game screen, system pointer hidden, Normal-mode note empty; Pick up → `runInputScript(1, 11, 0)`; the mallet → `(2, 0, 0)`, sentence line "Pick up mallet"; second click → inventory [836], verb 0x65 "mallet"; key 'x' through the window's text-input handler → Look's shortcut, sentence line "Look"; 66 frames played by the window's timer. Capture `window/play-03-inventory.png` shows the 256-colour gym, the F5 hint in yellow on the text screen, the verbs in green with Talk/Travel dim, "mallet" in its highlight colour and the crosshair. The profile folder (`window/profile`) is not committed.

## Regression

Filter `Loom|FirstVisit|ScummGameSession|MonkeyFloppy|Indy3|ScummEngineOpcode|Support|Verb|Architecture`, `SCUMM_SOUND_TEST_ROOT=D:\Juegos\Lucas\loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT=D:\Juegos\Lucas\indy3 ega en`: **897 passed, 5 skipped, 0 failed** (before the cell change); support tests 33 of 33 with `SCUMM_UPDATE_SUPPORT_MATRIX=1` after it. LOOM is unchanged by construction (its profile has no `PlaysThroughVerbs`; its engine-version gate gives the same charset as before). Indy3 EGA: the same profile values (the new rule is true for it as the family test was); the shadow-palette line changes nothing on its route (its scripts use `lights`), the EGA route test passes unchanged.

## Differences and uncertainties (kept)

- **Crosshair shape.** ScummVM draws the v3/v4/v5 built-in cursor from `resetCursors` (a 16 × 16 image: vertical bar in column 8, rows 0–5 and 9–14; horizontal row 7, columns 1–6 and 9–14; hotspot 8, 7), not the 23 × 21 v2 crosshair the shared play screen draws for EGA and now VGA. Not changed in this run (EGA must not change; the file is owned by the MonkeyFloppy S6 agent): an open follow-up for the shared play code.
- The text colour rule of the shared renderer is LOOM's (0x40 outline). This release's global scripts never set bit 0x40; room scripts were not scanned.
- ScummVM's Room 46 palette enhancement for this release (`_roomPalette[11] = 86`, `[13] = 80`, an optional bug fix) is not modelled.
- Everything the EGA record lists stays true here: assumed pointer and timing headless, `getActorFromPos` from the session's drawing, inventory objects run no verb code of their own, talk text position not compared, no right click, room-list picks are state previews, no sound during play.
- Perceived timing and audio are the user's checks.

## Addendum, 16 October: the right button stays the app's (R-7.16, Rule)

User decisions of 16 October: "Right click goes to the game in verb-based games, as in ScummVM; keep the Interact menu elsewhere.", then "why not use right click only in the games that need it? -instead of everywhere?". This release runs as engine version 3 (as ScummVM does), so its scripts never see a right click (ScummVM's Esc outside LOOM, mode 0) and the right button stays the app's: `ScummProfile.RightClickReachesGame` is false (`ScummIndy3VgaPlayTests.TheProfilePlaysThroughTheVerbsWithTheVersionThreeClickAndTheSameKeys`). Nothing changes for the user. See the EGA record's addendum.

## Addendum, 16–17 October: sound during play

Sounds now play during play through the device the user picked (the "no sound during play" item above is superseded). The shared route pins this release's ids (`ScummIndy3PlayTests.AssertRouteSounds` through `ScummIndy3VgaPlayTests`): boot 35 (Room 76 local 201 +0x0023; this boot does not pass Room 92), Close door 3 (Script 27 +0x002B), the chain 13 (Room 73 local 205 +0x0058), the switch 13 at object 837 verb 0x06 +0x0002 and **+0x02B0** (EGA: +0x0063), Open door 2 (Script 26 +0x002B); here every route event is Reached (the EGA route's are labelled estimates). Not checked in the window for this release (the window check ran EGA and Monkey Island). Shared record: [Monkey Island play record, sound addendum](../monkey1-play-20261016/README.md).
