# LOOM VGA (CD) feasibility probe, step 1 (5 October 2026)

Read-only. Scripts: `iso_extract.py <Loom.bin> <outdir>` (ISO 9660 over `MODE1/2352` raw sectors: user data at byte 16 of each 2352-byte sector) and `probe.py <outdir>` (block tree). Extracted files go to a scratch folder, never into the repository.

## The two discs

| | `Games/loom vga en` | `Games/loom vga en kixx` |
|---|---|---|
| Image | `Loom.bin` + `Loom.cue` | `LOOM.bin` + `LOOM.cue` (the cue names `LOOMCD.BIN`, so it must be fixed or the cue ignored) |
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
- The Game Status, glossary and replay catalog are EGA-derived and stay EGA-only (user decision); VGA starts with rooms and backgrounds.
