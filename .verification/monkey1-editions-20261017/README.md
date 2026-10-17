# MonkeyFloppy S9 Editions: the Spanish eight-disk 16-colour release (17 October 2026)

Scope: stage S9 for the family MonkeyFloppy. Copy checked: `Games/monkey1 ega es` (DOS, Spanish, eight disks `DISK01.LEC` to `DISK08.LEC`, 16 colours, `MONKEY.EXE` 71680 bytes) against the reference `Games/monkey1 ega en` (four-disk 256-colour floppy; the folder name says EGA). Stages compared: S4b boot, S5 first visit, S6 play (route of R-7.18, right click, sounds), S7 cutscenes, and the texts and charsets (R-14.2 to R-14.4, R-3.9, R-3.3). Model: the Indy3 S9 record (`.verification/indy3-editions-20261017`). ScummVM was not run (brief). Also in this record: the fix of the shared cutscene scan's text decoding (the S9 Indy3 open item), section "Scan decoding".

Build: worktree branch `worktree-agent-a607aa04a26bdd5d9` after merging master e32fc34 (MonkeyCd S4b/S5); every harness and test below ran on that build.

## What was run (all through RTK, `rtk proxy dotnet ...`)

| Check | Harness / test | Output |
|---|---|---|
| Boot | `.verification/monkey1-session-20261014/BootTrace <folder> <out>`; `ScummMonkeyFloppySessionTests.TheSpanishEightDiskEgaReleaseBootsTheSameWay` (now also the protection answer) | `boot-trace-es.txt` |
| Room audit (S5) | `.verification/monkey1-objects-20261015/RoomAudit` on both copies with the same build | `room-audit-es.tsv`, `room-audit-en-same-build.tsv` |
| Play route of R-7.18, its sounds, right click | `ScummMonkeyFloppyPlayTests.TheAcceptanceRouteFromTheDockRunsThroughTheGamesOwnInterface("es")`, `ARightClickOnAnObjectRunsItsDefaultVerb("es")` (both now theories over `en`/`es`) | test output |
| Cutscene scan and fresh-start runs (S7) | `.verification/indy3-cutscenes-20261017/CutsceneList <folder> scan` and `prun 8`; `ScummMonkeyFloppyCutsceneTests.TheSpanishReleaseListsTheSameScenesWithItsOwnAccentedTexts` | `cutscenes-es.txt`, `cutscene-runs-es.txt` |
| Texts and charsets | `ScummMonkeyFloppyPlayTests.EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets` (en, es) | test output |
| Window | `.verification/monkey1-play-20261016/PlayWindowCheck` run on the Spanish folder (real `MainWindow`, private `SCUMM_EXPLORER_PROFILE_ROOT` under the scratch output) | `window/play-check.txt`, `window/play-01-sentence.png`, `window/play-02-cutscene.png` |

## Edition × check × result

| Check | `monkey1 ega es` |
|---|---|
| Detection / opening | pass: The Secret of Monkey Island, v4, eight disks in one disk-set data source, family MonkeyFloppy |
| Boot (S4b) | pass: 2534 frames (English 2522), rooms 90, 0, 10, 0, 0, 38, 96, 33 (as English), unsupported 0 |
| Copy-protection answer | pass: answered from its own `var[0x10A]` (1690 for the seeded run; English 1543), Ctrl+M then the four digits through scripts 153 and 155, labelled assumed input; no quit |
| Room audit (S5) | pass: 81 rooms, 81 without a gap, 0 that could not run; objects in 30 rooms, actors in 57 (the 15 October addendum's numbers). Against English with the same build: outcome, active images and gaps equal in all 81 rooms; differences are data (below) |
| Play route of R-7.18 (S6) | pass: same ids, positions, branches and frames to within a few (dock, poster cutscene and Esc, SCUMM Bar door, the important-looking pirates' dialogue with choices 0x78 to 0x7A and 0x7F, back to the dock, the archway, Room 35's door, the rubber chicken into the inventory); texts the copy's own ("Mirar poster", "Quiero ser un pirata.", "Pues yo me largo.", "pollo") |
| Right click default verb | pass: runInputScript(2, 0, 2) on the poster runs its default verb 0x09 through Script 4 +0x0016, as English |
| Sound ids on the route | pass: the same ids in the same order (110, 108, 104 with the `triggerSound` stop, 2, 101, 100, 115); three offsets move with the texts: Room 28 entry `startSound` +0x008E (English +0x008A), Script 117 +0x0036 and +0x058B (English +0x0031, +0x053E); the test now reads them from the copy's script |
| Cutscene scan (S7) | pass: 294 triggers, 164 entries, 63 scenes, **104 begin** from a fresh start, as English; the same keys, categories, scripts, loads and groups per entry, and the same begun/not begun and rooms entered per run. Text counts and span sizes differ (translation). The poster's span starts at +0x00D9 (English +0x00CF) |
| Accents and charset | pass: texts write CP437 codes 0x82 é, 0x88 ê, 0xA0 á, 0xA1 í, 0xA2 ó, 0xA3 ú, 0xA4 ñ, 0xA5 Ñ, 0xA8 ¿, 0xAD ¡; every one has a glyph in each of the copy's charsets 1 to 4. 0xFA (18 uses) has no glyph in any Spanish charset; English (35 uses) lacks it in charsets 1 and 3 and has it in 2 and 4 (data; drawn as nothing where width 0, as the original reads the width table). The cutscene list shows the accents since the scan fix ("Room 19 · Empujar tripulación amotinada", "¡Oye!  ¡No me gustan las visitas!") |
| Window (one look) | pass: Room 33 in play in the real window: the Spanish verb screen in the 16-colour room, "Mirar poster" on the sentence line, the poster cutscene with "Re-eliga a la Gobernadora Marley.", Esc skips it, the right click runs the default verb (`window/play-02-cutscene.png`) |

No check failed; the registry levels are unchanged (S9 records editions; it raises no cell).

## Differences found and how they are handled

1. **Texts differ in data only** (a translation with CP437 accents and inverted marks). The play screen draws glyph codes as the strings hold them with the copy's charsets; the tests keep their texts per copy (`RouteTexts` in `ScummMonkeyFloppyPlayTests`), never per language.
2. **Offsets shift with text length** (three sound instructions, the poster's span, many script sizes in the room audit). The route test reads each sound's offset from the copy's own script (the script's only `startSound`/`stopSound`).
3. **Room 87's entry script differs**: English starts actor 9 (costume 0x31) only when script 0x77 is not running (`isScriptRunning` +0x000D); Spanish locks and loads costume 0x31 and always starts the actor (exit script unlocks it). The audit therefore draws actor 9 in Spanish Room 87 and not in English: the copy's own script decides, nothing to change.
4. **Stored images per room**: Room 40 has 18 (English 17) and Room 53 has 7 (English 8) object images, with the same zero active at first visit; Rooms 29, 33, 37, 39, 48, 50, 51, 53, 58, 81, 85 differ only in script sizes or in how many objects the room's scripts name.
5. **0xFA glyph** present in English charsets 2 and 4 and absent from every Spanish charset (see the table); not a code matter.

## Scan decoding (item 1: the S9 Indy3 open item)

`ScummCutsceneIndex` turned every escaped byte of a first line into a space and read object names as Latin-1. It now uses `ScummTextDecoder.DecodeLine` (glyph codes from 0x80 as CP437, as the play screen and Game Texts; a text control led by 0xFF or 0xFE, with its code and the two operands of the codes that have them, becomes spaces; other escapes spaces) and `ScummTextDecoder.DecodeGlyphs` for names. Lists compared with `CutsceneList` before and after (files in `scan-decoding/`, changed lines only):

| Copy | Changed lines | What changed |
|---|---|---|
| `loom ega mt32 en` (loom), `loom vga en` (loom), `indy3 ega en`, `indy3 vga en` | 0 | byte-identical |
| `monkey1 ega en` | 1 | "Room 35 · Talk to Citizen of Mêlée" (was "Mle": the name's 0x88/0x82 read as Latin-1 control characters). A first try also turned `\xFE` into "■" in "The three trials!\xFE\x01\xFF\x02" (one more distinct text in Room 28's two pirate entries); 0xFE is a control lead up to v6 in ScummVM `handleNextCharsetCode`, so it is spaced and those lines are unchanged |
| `loom ega mt32 es`, `loom ega es`, `loom ega v1.2 es` | 98 each | first lines gain ¿ ¡ and accents ("¿Ya terminó todo?", "¡Ahí dentro hay un buho!"); with non-ASCII and spaces removed, every changed line equals the old one |
| `indy3 ega es` / `indy3 vga es` | 21 / 4 | the same kind ("¡EH! ¿QUIEN HA APAGADO LAS LUCES?", "Papá  Como llegaste antes que yo?") |
| `monkey1 ega es` | 72 | the same kind, titles too ("Usar árbol"); Room 51's entry counts 41 texts instead of 40 (two lines that differed only in accented letters had collapsed into one) |

Tests: `ScummIndy3CutsceneTests.TheSpanishLinesAndNamesKeepTheirCharacters`, `ScummMonkeyFloppyCutsceneTests.TheScansFirstLineKeepsTheCopysCharactersAndSpacesItsControls`, `TheSpanishReleaseListsTheSameScenesWithItsOwnAccentedTexts`. LOOM regression (filter `Loom`, after merging master): 836 passed, 5 skipped, 0 failed; Monkey floppy, cutscene, support, mask and Indy3 play tests: 137 passed.

## Uncertainties kept

- ScummVM was not run: the Spanish copy's pictures, costumes and timing are not compared with the original; the window look is one screen.
- The 16-colour costumes are read with the shared v4 costume reader; Guybrush looks right in the window capture, other actors were not looked at.
- `VAR_VIDEOMODE` is still the VGA value 19 for this EGA release (S4b note); no script on the checked route reads it for anything but CGA.
- Room 87's actor difference is read from the scripts; which state script 0x77 stands for was not decoded.
- What glyph 0xFA should show (English charsets 2 and 4) was not looked at.
