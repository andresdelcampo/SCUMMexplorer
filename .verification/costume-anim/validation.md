# M2a: costume animation (Core), 28 September

## What changed

`src/SCUMMexplorer.Core/Rendering/LoomCostumeAnimation.cs` adds `LoomCostume` and `LoomCostumeAnimationState`, following ScummVM `costume.cpp`/`base-costume.cpp` (fetched on 28 Sep):

- `DecodeAnimation` = `ClassicCostumeLoader::costumeDecodeData` for v3: animation `direction + frame * 4` (nothing when it is beyond `_numAnim` or its offset is 0). Each limb in the mask reads a start command byte (0xFF: unused limb) and an extra byte: length `extra & 0x7F`, hold flag `extra & 0x80`. A start command of 0x79 stops the limb, 0x7A shows it again. `useMask` limits which limbs change (`setDirection` uses one limb at a time).
- `IncreaseAnimation(s)` = `increaseAnim`: without the hold flag the index runs start..end and loops; with it, it stops at the end. 0x7C (animation counter) and 0x78 (sound counter) are skipped unless the run is one command long; a command with 0x80 counts the sound counter (v3).
- `Draw` = `drawCostume`/`drawLimb` for GF_OLD_BUNDLE: x/y move start at (−72, −100) and accumulate over limbs 0–15; code 0x7B draws nothing; direction 0 (left) is drawn mirrored so the cel ends at the actor's x, unless the costume has the mirror flag (header bit 0x80). Unscaled placement as `paintCelByleRLECommon`.
- `MapColor`/`Compose` = `ClassicCostumeRenderer::setPalette` (old bundle, lights on): colour c draws the actor's palette entry c, the costume's palette byte draws entry 0; colour 0 is transparent.
- `LoomCostume.Load(game, id)` reads an indexed costume. `NewDirToOldDir`/`OldDirToNewDir` are ScummVM `util.cpp`.

The static pose gallery (`LoomCostumeRenderer`) is unchanged.

## The ego's costume (decoded)

Script 001 +0x0189 sets `var[0x077]` = 1; script 017 +0x009B gives actor 1 `costume local[1]` (= `var[0x077]` for argument 0) with init 1, walk 2, stand 3, talk 4/5 (8, 9, 10, 14, 15 when `bit[0xD61]` is set). Costume 1: animations 0..63, no mirror flag, palette byte 1.

| Frame | Left (anim 4f) | Right | Down | Up |
| --- | --- | --- | --- | --- |
| 1 init / 3 stand | limb 0 cmd 0, head cmd 1 (mirrored) | cmd 2, head 1 | cmd 3, head 4 | cmd 5, head 6 |
| 2 walk | 7.. loop | 15.. loop | 23..28 loop | 29.. loop |
| 4 talk start | head only: 35.. | 35.. | 52..59.. | 68.. |
| 5 talk stop | head 1 | head 1 | head 4 | head 6 |

So talk animates only limb 1 (the hood/face) and leaves the body as it was.

## Evidence

- `CostumeAnimRender` (this folder) steps each (frame, direction) 8 times and writes `renders/costume001-frame<f>-<dir>.png` (step 0 is the started frame; a red cross marks the actor's point) and `renders/costume001.txt` (limb positions and cel boxes per step).
- Looked at by eye: `frame2-left` is Bobbin walking left with his staff over 8 poses; `frame2-down` walks towards the viewer over 6 poses and repeats; `frame3-*` hold still.
- Tests: `LoomCostumeAnimationTests` (5): stand holds, walk loops 23..28, left is mirrored around the point, an animation past the costume changes nothing, palette mapping and transparency. Suite 212/212.

## What the user sees

Nothing new yet: this is the Core model that M2b (actor state) and M2c (drawing actors) use. No render, session or UI code changed, so the RoomAudit and the WPF harnesses were not rerun (skipped: nothing they test changed).
