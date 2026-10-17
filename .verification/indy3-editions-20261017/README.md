# Indy3 S9 Editions: the Spanish EGA and 256-colour copies (17 October 2026)

Scope: stage S9 for the families Indy3Ega and Indy3Vga. Copies checked: `Games/indy3 ega es` (DOS EGA floppy, Spanish, 91 LFL files, `INDY3.EXE` identical in size to the English one) and `Games/indy3 vga es` (DOS 256 colours, Spanish, 90 room files, `INDY.EXE`), against the references `Games/indy3 ega en` and `Games/indy3 vga en`. Stages compared: S4b boot, S5 first visit, S6 play (route of R-7.18 and its sounds), S7 cutscenes, and the texts and charsets (R-14.2 to R-14.4, R-3.9, R-3.3). Checks chosen as in `.verification/editions-20261005` (only where a difference was plausible). ScummVM was not run (brief).

## What was run (all through RTK, `rtk proxy dotnet ...`)

| Check | Harness / test | Output |
|---|---|---|
| Boot | `.verification/indy3-session-20261014/BootTrace` (EGA), `.verification/indy3vga-session-20261015/BootTrace` (256 colours) | `boot-trace-ega-es.txt`, `boot-trace-vga-es.txt` |
| Room audit (S5) | `.verification/indy3-objects-20261014/RoomAudit`, `.verification/indy3vga-objects-20261015/RoomAudit`; the English audits re-run with the same build for the comparison | `room-audit-ega-es.tsv`, `room-audit-vga-es.tsv` |
| Object ids | `.verification/ScriptDump objmap` (EGA, rooms matched by directory position) | (console) |
| Play route of R-7.18 and its sounds | `ScummIndy3PlayTests.TheAcceptanceRouteRunsInTheSpanishEgaCopy`, `ScummIndy3VgaPlayTests.TheAcceptanceRouteRunsInTheSpanishCopy` (new) | test output |
| Cutscene scan and fresh-start runs | `.verification/indy3-cutscenes-20261017/CutsceneList <folder> scan run`, after merging master (4990902, the MonkeyFloppy run fix) | `cutscenes-ega-es.txt`, `cutscenes-vga-es.txt` |
| Texts and charsets | `ScummIndy3PlayTests.EveryTextByteAboveAsciiHasAGlyphInTheCopysCharset` (new, four copies), `ScummIndy3CutsceneTests` (Spanish releases added) | test output |
| Window | `SpanishWindowCheck` (new, beside this file): the real `MainWindow`, private `SCUMM_EXPLORER_PROFILE_ROOT`, Room 73 in play, out through the door, Talk to Marcus in Room 1 | `window/window-check.txt`, `window/es-02-room-1-choices.png` |

## Edition × check × result

| Check | `indy3 ega es` | `indy3 vga es` |
|---|---|---|
| Detection / opening | pass: Indiana Jones and the Last Crusade (`IndianaJones3`), same family and profile as English | pass: same title, v4 blocks, engine version 3, family Indy3Vga (as English) |
| Boot (S4b) | pass: 1141 frames (English 1145), rooms 76, 79, 0, 6, 1, 92, 1, 73, 0 unsupported; Script 1 offsets shifted by its texts (loadRoom 73 at +0x0563, English +0x0564) | pass: 1002 frames, rooms 76, 79, 0, 6, 1, 73 (identical to English), 0 unsupported |
| Copy-protection room and answer | pass: Room 92, object 929 starts the questions; the answers derived from local script 204's own checks are the same (4, 6, 10, 0 for the seeded run); no edition data needed | n/a (no Room 92, as English) |
| Object ids | pass: 88 rooms, every object id at the same directory position as English (names translated) | pass by the route and the scan: the same ids act (Room 73 objects 830, 836, 837; Room 1 actor 4) and the 153 cutscene entries have the same room/object/verb keys |
| Verb names | translated: Walk to = "Andar hacia", Pick up = "Coger", What is = "Que es" (Script 96 same layout and keys' positions) | a translation of its own: Walk to = "Ir a", the others as EGA Spanish |
| Room audit (S5) | pass: 88 rooms, 0 gaps; outcome, active images and actors equal to the English audit of the same build in all 88 rooms | pass: 87 rooms, 0 gaps; equal to English in all 87 rooms |
| Play route of R-7.18 (S6) | pass: same ids, positions and branches; texts the copy's own (`mazo`, the choice "No seas bobo, Marcus, se lo tenia merecido.") | pass: same; texts its own (the choice "No seas bobo, Marcus. Se lo tenia merecido       .") |
| Sound ids on the route | pass: 35, 30 (Room 92), 3, 13 (twice), 2; the chain's 13 at Room 73 local script 205 **+0x005C** (English +0x0058: its line is longer), now read from the script by the test | pass: same ids and offsets as English 256 colours |
| Cutscene scan (S7) | pass: 333 triggers, 153 entries, 71 scenes, **90 begin** from a fresh start, 63 do not; the same keys and the same begun/not begun per entry as English (post-merge counts; 95 before the shared run fix) | pass: 332 triggers, 153 entries, 71 scenes, 90 begin, 63 do not; identical to English per entry |
| Accents and charset | pass: texts in CP437 (0xA4 ñ, 0xA8 ¿, 0xAD ¡; no accented vowels are written); the charsets `98.LFL`/`99.LFL` add glyphs at 0xA4, 0xA5, 0xA8, 0xAD; every text byte from 0x80 has a glyph; the window draws ¿ on the verb line | difference in data: texts write á (0xA0, a glyph in every copy), and ñ as `~` (0x7E), whose glyph is the one this charset redraws; 0xA8 and 0xAD occur 50 times (29 and 21) in its scripts but this charset has no glyph for them (width 0), so they are drawn as nothing, as the original reads the same width table |
| Window (one look) | pass: Room 1 dialogue choices "Pero Marcus,  ¿ has perdido tu sentido del humor ?" and "¿ Que significa: …" drawn with ¿ (`window/es-02-room-1-choices.png`) | not run (one look per brief) |

No edition failed; the registry levels are unchanged (S9 records editions; it raises no cell).

## Differences found and how they are handled

1. **Texts and charsets differ in data only.** Each copy's play screen draws glyph codes as its strings hold them with its own charset, so nothing needed code: the per-copy coverage is a test (`EveryTextByteAboveAsciiHasAGlyphInTheCopysCharset`), the 256-colour Spanish copy's missing inverted marks are pinned as a data fact.
2. **Offsets shift with text length** (Script 1 in the boot, Room 73 local script 205's sound). The route test now reads the chain sound's offset from the copy's own script instead of a constant.
3. **The two Spanish copies are separate translations** (Walk to "Andar hacia" vs "Ir a", the switch's line "EH! ¿QUIEN HA APAGADO LAS LUCES?" vs "HEY!! QUIEN APAGO LAS LUCES!!"). Test texts are per copy, not per language.
4. **Found, not fixed (owner: S7 shared scan, `ScummCutsceneIndex`):** the scan's first text replaces every escaped byte with a space (line 207) and object names are decoded as Latin-1 (line 135), so the list shows "EH!  QUIEN…", "Pap   Como…" and would show "esta¤o" for Room 86's "estaño" objects in a title. The fix is to decode those bytes as CP437 (`ScummTextDecoder`), as the play screen and Game Texts do; it is in a file another agent owned during this run, so it is written down in the tracker and the tests check text fragments that hold before and after it.

## Uncertainties kept

- ScummVM was not run: that the original draws nothing for 0xA8/0xAD in the 256-colour Spanish copy is read from the charset's width table and `CharsetRendererV3` (source, not runtime).
- The 256-colour Spanish object ids were not compared room by room (the `objmap` dump reads XOR-encoded EGA rooms only); the evidence is the route and the 153 identical scan keys.
- The committed English EGA audit (`indy3-objects-20261014/room-audit.tsv`, 14 October) is older than the current build: seven rooms differ from today's English audit; the comparison above uses English audits re-run with the same build.
- The Spanish EGA `^` in some lines (e.g. Room 22's "castillo^") is drawn as the charset's glyph 0x5E; what that glyph shows was not looked at.
