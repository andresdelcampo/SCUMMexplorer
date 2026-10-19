# Monkey Island 1 S9 Editions: the CD's own language folders, and `monkey1 vga es` (18 October 2026)

Scope: stage S9 for the family MonkeyCd, reference `Games/monkey1 vga en` (`The_Secret_of_Monkey_Island.cue`/`.bin`). The brief named `Games/monkey1 vga es` as the Spanish copy to check. Findings:

1. **`monkey1 vga es` is not the CD.** It is the Spanish **four-disk 256-colour floppy** (`000.LFL`, `901.LFL` to `904.LFL`, `DISK01.LEC` to `DISK04.LEC`, `MONKEY.EXE` 69589 bytes): SCUMM v4, family **MonkeyFloppy**. It was checked in its own family against `Games/monkey1 ega en` (the four-disk 256-colour English floppy; the folder name says EGA), section "MonkeyFloppy: `monkey1 vga es`".
2. **The Spanish CD is on the reference disc.** The disc's ISO track holds `ENGLISH/`, `FRENCH/`, `GERMAN/`, `ITALIAN/` and `SPANISH/`, each with its own `MONKEY.000` (8955 bytes in every folder), `MONKEY.001` (Spanish 4782166, English 4779713), `MONKEY.EXE` and the iMUSE drivers; the 24 CD audio tracks are shared (`BootTrace files`). The scanner read only `ENGLISH/` (`ScummV5Archive.FindGame`: most rooms, ENGLISH on a tie). **Change (small, additive):** `GameFolderScanner.DiscFolder` (init property) and `ScummV5Archive.FindGame(source, folder)` read one named folder; null keeps the default. Nothing in the app sets it, so **the window still opens the disc in English only** (see "Open"). The `SPANISH/` folder is the MonkeyCd edition checked below.

Model: `.verification/monkey1-editions-20261017`. ScummVM was not run (brief). Build: worktree branch `worktree-agent-acff2f51ccaba0606` on master defbf54e. Evidence files under `.verification/**` (`*.txt`, `*.png`) are git-ignored; the window captures are kept locally in `window/`.

## What was run (all through RTK)

| Check | Harness / test |
|---|---|
| Disc listing | `.verification/monkeycd-session-20261017/BootTrace files <folder>` (new mode); arguments `path|SPANISH` read a language folder in this harness, `monkeycd-objects-20261017/RoomAudit` and `indy3-cutscenes-20261017/CutsceneList` |
| Boot | `ScummMonkeyCdSessionTests.TheDiscsSpanishFolderBootsTheSameWay`; MonkeyFloppy: `.verification/monkey1-session-20261014/BootTrace`, `ScummMonkeyFloppySessionTests.TheSpanishFourDiskVgaReleaseNeedsOnlyEnterBecauseItsQuizDoesNotWait` |
| Room audit (S5) | `MonkeyCdRoomAudit "<disc>|SPANISH"` and the English folder with the same build; `Monkey1RoomAudit` on `monkey1 vga es` and `monkey1 ega en` |
| Play route of R-7.18, right click, sounds | `ScummMonkeyCdPlayTests` route and right click as theories (null = ENGLISH, "SPANISH"); `ScummMonkeyFloppyPlayTests` route, right click and charsets with the `vga es` case |
| Cutscenes (S7) | `CutsceneList "<disc>|SPANISH" prun 8`; `ScummMonkeyCdCutsceneTests.TheDiscsSpanishFolderListsTheSameScenesWithItsOwnTexts`; `ScummMonkeyFloppyCutsceneTests.TheSpanishReleaseListsTheSameScenesWithItsOwnAccentedTexts("monkey1 vga es")` |
| Accents and charsets | `ScummMonkeyCdPlayTests.EveryTextByteAboveAsciiHasAGlyphInTheCopysCharsets` (ENGLISH, SPANISH) |
| Window | `.verification/monkey1-play-20261016/PlayWindowCheck` on `monkey1 vga es` (real `MainWindow`, private `SCUMM_EXPLORER_PROFILE_ROOT`): `window/` |

## MonkeyCd: the disc's `SPANISH/` folder × check × result

| Check | `monkey1 vga en` `SPANISH/` |
|---|---|
| Detection / opening | pass with `DiscFolder = "SPANISH"`: The Secret of Monkey Island, v5, family MonkeyCd, the disc's audio tracks attached. **Not reachable in the window** (no way to pick the folder) |
| Boot (S4b) | pass: rooms 0, 10, 0, 0, 38 (as ENGLISH), player in control, unsupported 0, no assumed input (the CD has no protection) |
| Room audit (S5) | pass: 83 rooms, 82 without a gap, Room 99 cannot run (as ENGLISH); outcome, active images, gaps and actors equal to ENGLISH in every room |
| Play route of R-7.18 (S6) | pass: the same ids, positions and branches (poster and its skipped cutscene, the SCUMM Bar door, the important-looking pirates' dialogue with its three choices and "Pues yo me largo.", back to the dock, the archway, Room 35's door, the rubber chicken); texts its own: "Caminar a" (the floppy says "Ir a"), "Mirar cartel" (floppy "Mirar poster"), "Hablar a piratas importantes", "Coger pollo", "¡Las tres pruebas!" |
| Right click | pass: runInputScript(2, 0, 2) on the poster runs its default verb 8 ("Re-eliga a la Gobernadora Marley.") |
| Music in play (disc tracks) | pass: the route's sound ids and sources are ENGLISH's (110, 108, 104, 122, 2, 101, 100, 3, 115) and the music ids play disc tracks (shared audio tracks); the test does not pin their offsets for SPANISH (they move with the texts) |
| Talkie speech | – (the CD has no speech) |
| Cutscene scan (S7) | pass: 286 triggers, 160 entries, 62 scenes; every key, scene flag, script list and group equal to ENGLISH; titles its own ("Room 33 · Mirar cartel"); fresh-start runs: see "Fresh-start runs" |
| Accents and charsets | pass: the texts write é ê è á í ó ú ñ Ñ ¿ ¡ (CP437 0x82, 0x88, 0x8A, 0xA0 to 0xA5, 0xA8, 0xAD) and 0xFA; charsets 1 (dialogue) and 2 (messages) draw all but 0xFA (charset 1, as ENGLISH); charset 3 lacks í ú ñ Ñ ¡, charset 6 (verbs) Ñ ¿ ¡ (data) |
| Window | not possible (see detection); the floppy's Spanish window look below |

## MonkeyFloppy: `monkey1 vga es` × check × result (against `monkey1 ega en`)

| Check | `monkey1 vga es` |
|---|---|
| Detection / opening | pass: v4, four disks in one disk-set data source, family MonkeyFloppy |
| Boot (S4b) | pass: 2134 frames (English 2522), rooms 90, 0, 10, 0, 0, 38, 96, 33 (as English), unsupported 0 |
| Copy protection | one labelled key only (Ctrl+M, Enter, through script 153). The copy's Script 152 differs from English in two instructions: the digit wait compares `var[0x069]` with 0xFF00 (English 0xFFFF, +0x013E), so the quiz takes its four digits without a key, and the check compares `var[0x10A]` with itself (English with `var[0x0B3]`, +0x01C6), so it is accepted; no digits are assumed, no quit |
| Room audit (S5) | pass: 81 rooms, 81 without a gap; outcome, active images, gaps and actors equal to English in every room |
| Play route of R-7.18 (S6), right click, sounds | pass: the floppy route, right click and sound ids with the Spanish floppy texts (the same translation as the eight-disk `monkey1 ega es`: "Mirar poster", "Hablar a piratas importantes", "Pues yo me largo.", "pollo"); sound offsets read from the copy's scripts |
| Cutscene scan (S7) | pass: 294 triggers, 164 entries, 63 scenes, the same keys, scripts and groups as English; the poster scene begins; fresh-start runs: see below |
| Accents and charsets | pass: every text byte from 0x80 has a glyph in the copy's charsets but 0xFA in charset 1 (as both other floppies); ñ ¿ ¡ written |
| Window (one look) | pass: Room 33 in play in the real window: the Spanish verbs ("Abrir" … "Apagar"), "Mirar poster" on the sentence line, the poster cutscene "Re-eliga a la Gobernadora Marley.", Esc skips, the right click runs Look at (`window/play-check.txt`, four captures). No accented text is on this route; the glyph test covers them |

## Fresh-start runs

`CutsceneList <copy> prun 8` with the same build: the disc's ENGLISH folder **101 of 160** begin (as the S7 record), its SPANISH folder **101 of 160**, with the same begun/not begun and rooms entered for every entry (0 lines differ); `monkey1 vga es` **104 of 164**, as the English floppy and the eight-disk Spanish release (S9 of 17 October). About 70 s per copy on 8 workers.

## Differences found and how they are handled

1. **Copy folders on one disc** (MonkeyCd): data, read by `DiscFolder`; the default stays ENGLISH.
2. **Texts and verb names** are each copy's own (`RouteTexts` per copy in `ScummMonkeyCdPlayTests`; the CD's Spanish says "Caminar a"/"cartel", the floppy's "Ir a"/"poster").
3. **The Spanish VGA floppy's protection does not wait and accepts any answer** (Script 152 +0x013E and +0x01C6, see the table): the session answers from the copy's own scripts; only the Enter of the "press ENTER" screen is assumed.
4. **Charset coverage** differs per charset (CD charsets 3 and 6 lack some Spanish glyphs; 0xFA lacks a glyph in charset 1 everywhere): data, drawn with the copy's width tables.

## Tests

Regression on this build (after merging master defbf54e): the Indy4, Monkey (floppy, CD, 2), Indy3, Maniac, Zak, support, architecture, scanner, discovery, session and V5 tests 363 passed, 0 failed; LOOM (filter `Loom`) 836 passed, 5 skipped, 0 failed.

## Open

- The window cannot open the disc's FRENCH, GERMAN, ITALIAN or SPANISH copy: choosing one is a product decision (a picker or a per-folder entry), not taken here. Tracker note in `docs/monkey1/SUPPORT_STATUS.md` (MonkeyCd).
- FRENCH, GERMAN and ITALIAN were not run.
- ScummVM was not run.
