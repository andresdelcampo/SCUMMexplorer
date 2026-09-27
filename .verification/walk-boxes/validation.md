# Validation: old-v3 walk boxes (27 September 2026)

Status: **done.** Reader and overlay built; the overlay was checked by eye in Rooms 02, 03, 17 and 30. Builds with 0 warnings; 164/164 tests pass (8 in `LoomWalkBoxTests`). Room audit rerun: 0 gaps in 76 rooms, **0 changed renders** (`compare-png.ps1`).

## Layout (checked against ScummVM source)

`ScummEngine_v3old::resetRoomSubBlocks` (`room.cpp`): the word at room + 0x15 points at a count byte, then `SIZEOF_BOX_V3` = 18-byte boxes (`int16 ulx, uly, urx, ury, lrx, lry, llx, lly; byte mask, flags`), then the box matrix up to the room image (the word at + 0x0A). The matrix holds, per box, triplets `(low, high, next)` ending in 0xFF (`getNextBox`).

Data check (scratch script over the raw files): in **all 76 LOOM rooms** and 87 of 88 Indy3 EGA rooms, the matrix rows end exactly at the image offset. The exception is Indy3 Room 46, one byte short, which is the truncated matrix ScummVM's `getNextBox` workaround names. LOOM uses no other old layout.

What the data shows: LOOM's walkable areas are mostly **line segments** (paths). Room 02 has 15 chained segments, and Room 17 has one real quadrangle (box 8) among segments. Close-up rooms (for example 14, 19 and 53–61) have a single box outside the image. Flags used: 0x01, 0x02, 0x10, 0x11, 0x12, 0x16, 0x81 (Room 05) and 0xA0 (Rooms 09 and 34, invisible + player-only).

## Code

- `Formats/LoomWalkBoxes.cs`: `LoomWalkBoxReader`, `LoomWalkBoxSet` with ports of `checkXYInBoxBounds` (v3: no line-segment tolerance, as ScummVM documents for LOOM DOS EGA), `closestPtOnLine`, `getClosestPtOnBox`, `Actor::adjustXYToBeInBox` (thresholds 30/80/0, 0xFFFF bound, v3 last-valid-point quirk, invisible/player-only skip), `getNextBox`, and `getBoxBaseAddr`'s "one past the last box" workaround. `SetFlags` is ready for `setBoxFlags`.
- The session reads the current room's boxes on entry (`CurrentWalkBoxes`, `LoomScriptRuntimeState.WalkBoxes`).
- App: a **"Show walk boxes"** checkbox draws each box (outlined quadrangle or segment, dashed if invisible) with its number. The tooltip lists the count, boxes outside the image and the flags.

## Overlay check (WalkBoxCheck)

[`WalkBoxCheck`](WalkBoxCheck/Program.cs) drives the real `MainWindow` by reflection: it opens `Games\loom`, browses to each room, turns off **Normal room view** (full panorama) and **Show doorways**, ticks `WalkBoxToggle`, and renders `RoomSceneSurface`. It checks that the overlay shows one outline and one label per box, that every outline point maps back to its box corner in room pixels exactly (same scale and centring as the room image), and that unticking hides the overlay. Synthetic property changes, no physical input.

```powershell
dotnet build .verification\walk-boxes\WalkBoxCheck\WalkBoxCheck.csproj
Start-Process -Wait -NoNewWindow -WorkingDirectory $PWD -FilePath Exe\WalkBoxCheck\Debug\net10.0-windows\WalkBoxCheck.exe -ArgumentList 'Games\loom','.verification\walk-boxes\capture','2','3','17','30'
```

Result: **8/8 PASS, 0 failures** ([report](capture/walk-box-check.txt)).

By eye ([Room 02](capture/room-02-walk-boxes.png), [03](capture/room-03-walk-boxes.png), [17](capture/room-17-walk-boxes.png), [30](capture/room-30-walk-boxes.png)):

- **Room 02** (cliff, 960 px): boxes 0–14 form one path from the tree on the cliff top (box 0, x 811–830, y 60) along the rock edge and down the ridge to x 540. The path lies on the drawn rock throughout; box 4 (x 837–888, y 118), the one script 045 watches, is the ledge below the tree.
- **Room 03** (lake): box 0 is the high path along the cliff on the left, boxes 1–2 cross the middle of the shore, box 4 leads up to the right-hand exit, box 3 goes down to the tents, and boxes 5–6 descend the steps to the jetty. Every segment sits on a drawn path.
- **Room 17**: box 0 runs along the middle row of the graveyard, 1–3 lead to the right edge, box 8 (the one quadrangle) is the foreground floor, and 11 leads off the left edge. Boxes 3–6 and 10 run past x 320, outside the image: exits off the right edge, as the tooltip lists.
- **Room 30** (dark cave, 960 px): segments follow the lit ledges (21–27 along the right ledge, 38–40 along the left one). Box 28 and 48 lie partly outside the image (exits). Box 46 (x 114–121, y 126) is an isolated segment.

No box contradicts the floor it is drawn on. This is a visual check against the room artwork; ScummVM's debugger (`boxes` command) was not used.
