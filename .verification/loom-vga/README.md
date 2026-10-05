# LOOM VGA (CD) feasibility probe, step 1 (5 October 2026)

Read-only. Scripts: `iso_extract.py <Loom.bin> <outdir>` (ISO 9660 over `MODE1/2352` raw sectors: user data at byte 16 of each 2352-byte sector) and `probe.py <outdir>` (block tree). Extracted files go to a scratch folder, never into the repository.

## The two discs

| | `Games/loom vga en` | `Games/loom vga en kixx` |
|---|---|---|
| Image | `Loom.bin` + `Loom.cue` | `LOOMCD.BIN` + `LOOMCD.CUE` (renamed back, 5 October, so the cue matches its bin) |
| Volume label | `LOOM` | `LOOMCD` |
| Track 2 (CD audio) starts | 04:57:60, pregap 2 s | 04:58:00, pregap 2 s |
| Data files | `000.LFL`, `901`–`904.LFL`, `DISK01.LEC`, `LOOM.EXE` | the same seven |

**All seven data files are byte-identical between the two discs.** They differ only in the label, the disc layout (LBAs) and the audio track's start (a few frames), so one game reading serves both; the audio offsets are per disc (read them from the `.cue`).

## File formats (SCUMM v4)

- **`DISK01.LEC`**, 4,059,462 bytes, **XOR 0x69** over the whole file. A block is `size u32 LE (header included)`, a two-letter tag, the body. `LE` holds `FO` (room offsets: a count byte, then room u8 + offset u32 per room) and **78 `LF` room blocks** (`LF`: room number u16, then `RO` room image data, `SC` scripts, `CO` costumes, `SO` sounds).
- Room-level counts: 78 `RO`, 122 `SC`, 166 `CO`, 30 `SO` (2 stray two-byte blocks at the end of a room are padding, to confirm).
- Inside `RO` (room 1 as the example, 320 x 200): `HD` header (width, height, object count), `CC` colour cycles, `SP` (unknown, 262 bytes), `BX` walk boxes, **`PA` a 256-entry VGA palette** (776 bytes), `SA` scale slots, **`BM` the 256-colour background** (compressed strips, 22 KB for room 1), `OI` one object image per object, `NL`/`SL` counts, `OC` object code and name (21 bytes each here), `EX`/`EN` exit and entry scripts, `LC` count and **`LS` local scripts** (the VGA game keeps its scripts in the room, as EGA does).
- Totals over all rooms: backgrounds (`BM`) 2.0 MB, object images (`OI`) 0.7 MB, local scripts (`LS`) 87 KB, entry scripts 11 KB.
- **`000.LFL`**, the index: plain (not XORed) blocks `RN` room names (room number then a 9-byte name XOR 0xFF), `0R` rooms, `0S` scripts, `0N` sounds, `0C` costumes (each: count u16, then count x (file/room u8, offset u32 LE)), `0O` objects (1000 entries). Script, sound and costume entries give the room and the offset inside that room's `LF`.
- `901`–`904.LFL` are the charsets (not decoded). `LOOM.EXE` is the game's own executable (117 KB).

## What this means for the plan

- Reading the files is straightforward: the `.LEC` XOR and block tree are simple, and the index is small. The **disc image reader** is the easy part (a few dozen lines; the user data of a Mode 1 sector is bytes 16 to 2063).
- **Not decoded yet** (the next steps of the plan, none of it touched): the `BM` strip codec (v4 VGA bitmap, to take from ScummVM `engines/scumm` `gfx.cpp` `drawStripComplex`/`drawStripBasicV` family and compare with the `scummvm/` build), `OI` object images, the v4 script dialect (the EGA `LoomScriptReader` is v3), costumes and sounds (30 `SO`; the music and speech are CD audio on track 2), `SP` and `CC`.
- The Game Status, glossary and replay catalog are EGA-derived and are not reused for VGA (the plan says to rederive, never copy); VGA starts with rooms and backgrounds.

## Step 2: the room background decodes (5 October)

`decode_rooms.py` (Python, scratch-only; the Core port comes next) decodes **all 78 room backgrounds** without an error, checked by eye on the contact sheet [all-rooms-contact-sheet.png](all-rooms-contact-sheet.png) and on [room01.png](room01.png) (the LucasArts logo) and [room02.png](room02.png) (960 x 144 scrolling mountain view).

- **Palette:** `PA` body = `u16 768`, then 256 RGB triples (8-bit, used as they stand), then 6 trailing bytes.
- **`BM`:** the body is `u32` (the data size), then one `u32` offset per 8-pixel-wide strip (width / 8 entries; offsets are from the start of the body), then the strips. A strip begins with a code byte: `0x01` raw (8 columns of height bytes, column by column) or `0x0E`–`0x12` / `0x18`–`0x1C`, the ScummVM "basic" run-less bit codec, vertical (`0x0E`–`0x12`) or horizontal (`0x18`–`0x1C`); the code modulo 10 is the colour bit count (here always 8). Bits: `0` same colour, `10` read a new colour (that many bits), `110` colour += step, `111` reverse the step then colour += step; the step starts at -1. Only codes `0x01`, `0x12` and `0x1C` occur in this game.
- A few rooms show black on the sheet (the dark rooms and the close-up boards); they may be fully dark or use features not decoded yet (`SP`, transparency or object-only rooms). Not checked.
- Room sizes: 320 x 200 for the logo, 320 x 144 for scenes, up to 960 x 144 (scrolling).

## Step 3: the scripts read with a v4 dialect (5 October)

The CD game's bytecode is the **v5 operand set** (ScummVM `ScummEngine_v4` = `ScummEngine_v5` plus a few overrides; `ScummEngine_v3`, the LOOM EGA class, derives from v4 and overrides three opcodes). The existing disassembler already read 742 of 793 scripts without an issue as plain LOOM EGA; a new dialect `ScummScriptDialect.LoomCd` (`ScummScriptDialects.For(GameId.LoomVga)`) brings that to **all 793** (global `SC`, entry `EN`, exit `EX` and local `LS` scripts of the 78 rooms), checked by the test `EveryScriptOfTheCdGameDecodesWithTheV4Dialect`. What differs from the EGA dialect, from ScummVM `script_v5.cpp` (read as reference; the code is our own):

- `roomOps` (0x33): the sub-opcode byte comes first and each operand's variable bit is read from it (the EGA game reads two operand words first). Sub 2 and 4 take two words (small-header blocks), 7 scale, 8 intensity, 9 save, 10 fade, 11 and 12 RGB, 13 and 14 file-name strings, 15 transform, 16 cycle speed.
- 0x30 is `matrixOps` (set box flags, scale, scale slot, create box matrix) instead of `setBoxFlags`; 0x3B is `getActorScale` (a result and an actor) instead of the no-op `waitForActor`; 0x4C is `soundKludge` (a word list) instead of `waitForSentence`.
- `print` sub-opcode 6 has no operand (left); `cursorCommand` 14 takes a word list.
- Scripts are plain blocks, not `00.LFL` resources: `LoomVgaArchive.ReadScripts(room)` returns them and `LoomScriptReader.DecodeCode` reads the bytes.

Decoding without an issue is not the same as understanding the scripts; the executor, glossary and variables of the CD game are untouched.

## Step 4: objects and texts (5 October)

- **`OC` object blocks** (`LoomVgaArchive.ReadObjects`): id at block offset 6, x/8 at 9, y/8 at 10 (bit 7 the parent state), width/8 at 11, parent at 12, walk-to point words at 13 and 15, height and facing at 17, the name's offset at 18, then the verb table (verb byte and word offset, ended by a zero verb; 0xFF is the fallback verb), the zero-ended name and the code. The verb offsets land exactly on the code that follows the name (room 3 object 0x5F: verb 0x37 at 24, name at 23 is empty); room 4's "owl" has verbs 0x36, 0x39 and 0x56.
- **`OI` object images**: the object id (u16), then a `BM`-like body (size u32, one u32 offset per 8-pixel strip counted from the size field, strips); the width is the object's, the height comes from the `OC` block. Not decoded in Core yet. **The index's `0O` table gives every room object the same initial state (owner 0xF, state 0)**, so what a room shows at first visit comes from running its entry script (EN), as on EGA; the backgrounds already carry the permanent scenery.
- **Texts** (`LoomTextExtractor`, CD branch): 1,196 entries from 1,470 scripts (the 793 script blocks plus the object verb scripts): 1,042 spoken lines, 134 object names, 20 stored lines, nothing skipped or unresolved. The Game Texts window opens with them (room pictures from the CD data); `TextGalleryCheck` passes its first three checks (the fourth expects EGA room 3 text). The harness's search term was a hard-coded Spanish word, which is why it failed on every non-Spanish edition; it now takes a word that occurs in several texts.
- The spoken lines start and end with the control character 0x19 (the "keep text up" marker); the EGA extraction treats markers the same way.
