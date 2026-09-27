# Validation: old-v3 walk boxes (27 September 2026)

Status: **reader done; overlay built but not yet checked by eye.** Builds with 0 warnings; 164/164 tests pass (8 new in `LoomWalkBoxTests`).

## Layout (checked against ScummVM source)

`ScummEngine_v3old::resetRoomSubBlocks` (`room.cpp`): the word at room + 0x15 points at a count byte, then `SIZEOF_BOX_V3` = 18-byte boxes (`int16 ulx, uly, urx, ury, lrx, lry, llx, lly; byte mask, flags`), then the box matrix up to the room image (the word at + 0x0A). The matrix holds, per box, triplets `(low, high, next)` ending in 0xFF (`getNextBox`).

Data check (scratch script over the raw files): in **all 76 LOOM rooms** and 87 of 88 Indy3 EGA rooms, the matrix rows end exactly at the image offset. The exception is Indy3 Room 46, one byte short, which is the truncated matrix ScummVM's `getNextBox` workaround names. LOOM uses no other old layout.

What the data shows: LOOM's walkable areas are mostly **line segments** (paths). Room 02 has 15 chained segments, and Room 17 has one real quadrangle (box 8) among segments. Close-up rooms (for example 14, 19 and 53–61) have a single box outside the image. Flags used: 0x01, 0x02, 0x10, 0x11, 0x12, 0x16, 0x81 (Room 05) and 0xA0 (Rooms 09 and 34, invisible + player-only).

## Code

- `Formats/LoomWalkBoxes.cs`: `LoomWalkBoxReader`, `LoomWalkBoxSet` with ports of `checkXYInBoxBounds` (v3: no line-segment tolerance, as ScummVM documents for LOOM DOS EGA), `closestPtOnLine`, `getClosestPtOnBox`, `Actor::adjustXYToBeInBox` (thresholds 30/80/0, 0xFFFF bound, v3 last-valid-point quirk, invisible/player-only skip), `getNextBox`, and `getBoxBaseAddr`'s "one past the last box" workaround. `SetFlags` is ready for `setBoxFlags`.
- The session reads the current room's boxes on entry (`CurrentWalkBoxes`, `LoomScriptRuntimeState.WalkBoxes`). Nothing uses them yet; no behaviour or render changed.
- App: a **"Show walk boxes"** checkbox under "Show depth areas" draws each box (outlined quadrangle or segment, dashed if invisible) with its number. The tooltip lists the count, boxes outside the image and the flags.

## Not done yet

- **By-eye check of the overlay**: extend a WPF harness (like `hotspot-highlight/HighlightCheck`) to tick `WalkBoxToggle` and capture Rooms 02, 03, 17 and 30, then compare with each room's floor. ScummVM's debugger was not used.
- The room audit was not rerun. The change is additive, so no render should change.
