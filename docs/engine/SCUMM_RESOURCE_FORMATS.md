# SCUMM resource formats the explorer reads (beyond LOOM and Indy3 EGA)

Version scope is stated on every item. Everything here was checked against ScummVM's `engines/scumm` (resource, room, gfx, costume and palette sources) and against the local game data on 11 October 2026; pictures were checked in the real window (`.verification/generic-v4/README.md`). Only **rooms, costumes and sounds are read** for these games: their scripts, objects and actors are not, so nothing here says what a script does (see [the script reference](SCUMM_SCRIPT_MAP.md) for the scope).

## Index and container layouts

| Layout | Games seen | Files | Index | Code |
|---|---|---|---|---|
| Classic v1 | Maniac Mansion, Zak McKracken (DOS EGA) | `00.LFL`…`NN.LFL`, every byte XOR 0xFF | magic `0x0A31`, one byte per global object, then directories of rooms, costumes, scripts and sounds whose **sizes are fixed per game and not stored** (Maniac 800/55/35/200/100, Zak 775/61/37/155/120); the game is the one whose sizes add up to the file length. A room directory is a skipped byte and a word offset per room; the others a room byte and a word offset per resource, `0xFFFF` = none. | `ScummV1Index` |
| Enhanced v2 | Maniac Mansion Enhanced, Zak McKracken Enhanced | same files, XOR 0xFF | magic `0x0100` (as old-v3), then one byte per object (old-v3 has four), then four directories (count, room bytes, word offsets) that end exactly at the end of the file | `LflIndexReader.DetectObjectEntryBytes` |
| Old v3 | LOOM EGA, Indy3 EGA | as above | magic `0x0100`, four bytes per object | `LflIndexReader` |
| v4, disks | Monkey Island (VGA 4 disks, EGA 8), LOOM CD (1) | `000.LFL`, `DISK01.LEC`…, LEC XOR 0x69 | tagged blocks `0R 0S 0C 0N 0O`; a resource is a room byte plus a dword offset counted from the first block inside that room's `LF` block | `ScummV4Archive`, `ScummV4Disks` |
| v4, room files | Indiana Jones and the Last Crusade, 256 colours | `00.lfl` (no XOR) with the same tagged blocks; `NN.lfl` is the content of one room's `LF` block (RO, SC, SO, CO) | same as v4 disks, offsets counted from the start of the room file | `ScummV4Archive.LoadRoomFiles` |

In every layout a room is the file `NN.LFL` (v1/v2/old-v3/room files) or an `LF` block of a disk (v4); a room of the directory that is not a picture (room 0 of the v2/v1 games is the index file; rooms 98 and 99 of Indy 256 are resource banks) is left out because its picture does not decode.

## Room pictures

| Format | Applies to | Description |
|---|---|---|
| Tiles | v1 | Header bytes 4 and 5 are the tiles across and down (8x8 pixels); word offsets at 10, 12, 14, 16 point at four run-length streams (`decodeV1Gfx`: four common bytes, then run codes): 256 character patterns of eight bytes (four 2-bit pixels, each drawn twice as wide), the character of each tile, a colour byte per tile and a mask character. Three room colours are header bytes 6 to 8, the fourth is the tile's colour byte & 7; the numbers are C64 colours that a per-game table turns into EGA colours. | `ScummV1RoomDecoder` |
| One stream | enhanced v2 | Width, height at bytes 4 and 6, picture at the word offset in byte 10: column by column, top to bottom; code = run of one colour (high nibble count, low nibble colour) or, with bit 7, a run repeating what the column to the left shows (a dither); zero count takes the next byte. Depth masks follow (not read). Height at most 128. | `ScummV2RoomDecoder` |
| Strips, EGA | old v3, v4 EGA (Monkey Island EGA) | The old-v3 strip code (run, copy-left, alternating pair); in v4 EGA the BM body is a length word, a word offset per strip (from the body start) and the strips. No `PA` block: the fixed EGA palette. | `ScummV3EgaRoomDecoder.DecodeStrip` |
| Strips, 256 colours | v4, Indy 256 | Strip codes 0x01 raw, 0x0E-0x12 and 0x18-0x1C bit codes (v4); 0x02 runs, 0x03 four-bit codes, 0x04 local palette and runs, 0x07 stepped colour (older 256-colour games, ScummVM `unkDecode8`-`11`). Palette from the `PA` block. | `ScummV4Archive.DecodeStrip`, `DecodeOld256Strip` |

## Costumes and sounds

- **Costumes.** v2 enhanced uses the old-v3 costume reader unchanged (format `0x58`/`0x59`, one colour byte). v4 costumes are read in the palette of the room that stores them; a costume looks wrong where the game would set actor colours by script (Guybrush's shirt in room 10). v1 costumes (format `0x57`, drawn per actor in 8-pixel limbs with actor-specific colours) are **not read**: the classic games offer no costumes.
- **Sounds.** v4 sounds are `SO` blocks holding a `WA` (PC speaker) and an `AD` (AdLib) block; the 256-colour Indy wraps them in a second `SO` block. v1 and v2 sounds are other formats and are not read.

## Open

Depth masks, objects, scripts, v1 costumes, v2 and v1 sounds, and a pixel comparison of every picture against ScummVM. Monkey Island and Indy 4 folders in other layouts (v5 `.000`/`.001`) are not read at all yet.
