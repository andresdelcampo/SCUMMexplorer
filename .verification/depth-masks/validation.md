# Depth masks: actors behind the scenery (29 September 2026)

The user's report (29 Sep): "Bobbin floats on top of the background: no depth". The user picked it next. Checked
against ScummVM's sources (`gfx.cpp`, `actor.cpp`, `boxes.cpp`, `costume.cpp`, `object.cpp`) before changing anything.

## What the evidence says (ScummVM)

- v3 rooms have two mask planes: `_numZBuffer = 2` for `version <= 3` (`gfx.cpp`). Plane 1 is the room's depth mask:
  `getZPlanes` finds it at the image's start plus its first word, and `decodeMask` (old bundle: offsets at
  `stripnr * 2`) decompresses it per strip with `decompressMaskImg`; offset 0 clears the strip. The room decoder
  already read this (`LoomEgaRoomDecoder.DecodeDepthMask`).
- Object images carry a mask in the same place and layout. `drawBitmap` calls `decodeMask` for every drawn object
  strip; EGA strips are never transparent (`drawStrip` returns false for `drawStripEGA`), so the object's mask replaces
  the room's under the object (`dbAllowMaskOr` only ORs on transparent strips). All 76 LOOM rooms' object masks decode
  (unit test).
- `Actor::drawActorCostume` (v3 path): `_zbuf = _forceClip`, else 0 for class NeverClip, else
  `getMaskFromBox(_walkbox)` clamped to `_numZBuffer - 1` = 1. LOOM never sets `forceClip` (no script uses the
  z-clip `actorOps` 0x12/0x13; code.tsv). `getMaskFromBox` returns the box's mask byte (`Box::old.mask`) and 1 for no
  box (255) in v3, a ScummVM workaround for bugs #791/#897.
- The costume decoder skips an actor pixel where the mask plane's bit is set (`byleRLEDecode`:
  `masked = ... (mask[0] & maskbit)`).
- Box masks in LOOM (decoded 29 Sep): many rooms mix 0 and 1, for example Room 02 box 0 is 0 and boxes 3–14 are 1,
  Room 30 has 52 boxes with both. Close-up rooms (53–87) have a single box with mask 0.

## What the user now sees

- In the interactive mode an actor standing in a walk box with mask 1 is hidden where the room's depth mask, or the
  mask of an object drawn there, is set. In Room 02 after a click on the path (box 4), Bobbin walks behind the dark
  foliage beside the rock instead of floating on it ([now](room02-after-click-app.png), [before](room02-after-click-before.png),
  [enlarged](room02-after-click-zoom.png)).
- In boxes with mask 0 (Room 02's box 0 under the tree, close-ups) actors are drawn in front, as before.
- An actor with no box (255), such as Room 02's swans and the last leaf, is drawn behind the mask (ScummVM's v3
  workaround). An actor whose box the session does not know is drawn in front, noted as an estimate on the actor.
- Not checked in ScummVM's running game yet (only against its sources): worth comparing Room 02's path in ScummVM.

## Changes

- `LoomWalkBoxSet.GetMask(box)`: `getMaskFromBox` for v3.
- `RoomObjectImage.Mask` and `LoomRoomObjects.DecodeMask`: each object image's plane-1 mask.
- `RoomObjectSelection.ComposeDepthMask(room)`: the room's mask, then each active image's mask in paint order.
- `LoomActorRenderer.Render(…, depthMask, boxes)` and `LoomCostume.Compose(…, mask)`: an actor with plane 1 skips
  masked pixels; `LoomDrawnActor.ZPlane` records the plane.
- `MainWindow.UpdateActorLayer` passes the composed mask and the session's walk boxes.

## Verification

- Unit tests: `EveryLoomObjectImageCarriesAReadableDepthMask` (all rooms; Room 02 objects 155 and 157 have 1,450 and
  512 mask pixels, Room 19's 380 7,328), `DrawnObjectsReplaceTheRoomDepthMaskUnderThemInPaintOrder` (synthetic),
  `AnActorInAMaskedWalkBoxIsHiddenWhereTheDepthMaskIsSet` (the booted ego: box 0 in front, box 4 fully hidden under a
  full mask, no mask or an unknown box in front). `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 233/233.
- ActorDrawCheck (`.verification/actor-draw/ActorDrawCheck`): 0 failures ([actor-draw-check.txt](actor-draw-check.txt));
  its pixel counts pass with the mask (the ego after the boot is in box 0: 357 pixels, as before).
- Skipped: the audit (room pictures and gaps do not depend on actors), DoorwayClickCheck (no change to clicks or
  room loads), Blockers (no session change), the playback and layout checks.
