# LOOM EGA editions, first pass (5 October 2026)

Reference: `Games/loom ega mt32 en`. Tried: `loom ega en`, `es`, `fr`, `de`, `hebrew`, `v1.2 es`, `v1.2 de` (all 74 `.LFL` files, no 82–85).

Run with `GlossaryCheck <edition> <reference> <out>` (`glossary-*.md`) and `RoomAudit <edition> <out>` (room BMPs not committed).

| Check | Result |
|---|---|
| Folder opens and is identified as LOOM | all seven |
| `RoomAudit`: first-visit rooms, remaining gaps | 0 gaps after filling in every edition (3 before filling, as in the reference) |
| Glossary: same / moved / absent | 75–76 same, 32–33 moved, 0 absent in every edition |

Not yet checked: sounds degrading cleanly without files 82–85 in the real window, text extraction and the Game Texts window per language, the Hebrew copy's text and font handling (right-to-left or patched?), what v1.2 changed against the plain editions, the cutscene catalog on each edition.

## Second pass (5 October)

- **Sounds without files 82–85** (plain `loom ega en`): `SoundPanelCheck` shows sound 24 as "No playable audio" and sound 28 plays as AdLib; the app only offers the Roland device when `SceneSoundPlayer.HasRolandSounds` finds the files (`MainWindow.xaml.cs` lines 232 and 2598), so nothing breaks. Record: `sound-plain-en/`.
- **Texts** (`TextGalleryCheck`, plain en / fr / hebrew): 1245 / 1225 / 1224 entries, 2 unresolved (Room 70 local scripts 203 and 204, truncated operands, same in all), object names 133 each. The harness's last check, "Search narrows to matching original text", fails in all three; it probably searches for an English word and was not run against the reference this time, so unexamined. Records: `texts-*/`.
- Still open: whether the Hebrew strings are real Hebrew and how the font draws them; v1.2 differences; cutscene catalog per edition.

## Hebrew (5 October)

- The copy is a DOS Hebrew build: `README.TXT` and `Go.bat` are in CP862 (the `Go.bat` banner links old-games.org), and the game's text bytes 0x80–0x9A are Hebrew letters (6119 runs of three or more such bytes in its room files against about 550 in the other editions, which are accents).
- `WindowCheck` on the real window: rooms 3 and 17 render normally (`window-hebrew/`); no message text was on screen, so the in-game Hebrew glyph drawing is **not yet seen**.
- **Open defect (Game Texts window only):** `LoomScummTextDecoder` decodes bytes of 0x80 and up as CP437, so Hebrew entries show as Latin accents and box characters. A fix needs (a) detecting a Hebrew edition (a text-run heuristic is possible, the ratio is about 11 to 1, but nothing in the index names the language) and (b) a decision on order: Hebrew builds usually store the text in visual (reversed) order, and WPF's bidi would flip it again. Not changed; waiting for the user's call before touching the shared static decoder.

**Decision (user, 5 October): the Hebrew version is dropped.** The Game Texts decoding defect above stays unfixed on purpose; the folder is left in `Games/`.

## v1.2 against the plain editions (5 October; byte comparison of all files)

- **v1.2 de is byte-identical to plain `loom ega de`** (all 80 files, `LOOM.EXE` included): nothing to compare.
- **v1.2 es differs from plain `loom ega es` in two files** (same sizes), and adds `INSTALL.BAT`/`INSTALL2.BAT`:
  - `00.LFL`: 75 bytes in the room-to-disk table (for example disks 2 become 1): the installer's disk layout, no effect on the data the app reads.
  - `15.LFL`: two bytes, 0x90 to 0x00, inside a repeating `90 ff ff 00 01 00` pattern (room 15 image or similar data); not decoded further, no script or text involved.
- `LOOM.EXE` is the same file in both. So v1.2 changes nothing the app shows; no per-edition handling needed.

## Correction to the first-pass glossary numbers, and the replay catalog (5 October)

- The first pass compared every edition with `loom ega mt32 en` as the "reference", but `GlossaryCheck` treats the reference as Spanish-numbered and applies the measured bit shift to the other folder. The measured shift is **0 for plain en, es, fr, de, v1.2** (they number story bits like the Spanish release) and **-1 only for the Roland copy** `loom ega mt32 en`. So the Roland upgrade is what moved the bit numbers, not "English". The comparison that means something is against `loom ega es`: `glossary-vs-es-en.md` (107 same, 7 moved: Script 5, the input script, is 212 instructions in plain en against 162, and Script 51 and Script 5 use a few variables more), `glossary-vs-es-fr.md` (115 same, 0 moved) and `glossary-vs-es-de.md` (116 same, 0 moved). The 143 entries not counted are engine-side or not applicable. The `glossary-loom-ega-*.md` files of the first pass are kept but their "moved" counts are an artefact of the wrong reference.
- **Replay catalog:** `LoomReplayCatalog.Load` fingerprints the files it was generated from (SHA-256), so on every edition other than the reference the catalog is empty with a note and only the four established starts remain. That is graceful, not a failure. The sequence tests in `LoomReplaySequenceTests` pin boundaries by offset (`endCutscene at Script 85 +0x05A8`; plain en has it at `+0x05AB`), so they fail on the plain editions by construction. Making cutscene replay work on a plain edition means rerunning the discovery pipeline on it (`replay-coverage-20261001/`, `english-catalog-20261004/`) and shipping a second catalog: not done.

**Decision (user, 5 October): no replay-pipeline rerun for the other editions.** `loom ega mt32 en` is the only edition that gets the cutscene catalog and the replay tests; the other editions open, show rooms and texts, and keep the four established starts.

**Keeping track (user, 5 October):** the list of verified editions lives in the main `README.md`, section "Verified editions"; add a bullet there whenever an edition is checked.
