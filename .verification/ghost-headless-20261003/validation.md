# Ghost scene: "headless" Bobbin (3 October)

Probe: `GhostLimbs` (runs `sequence-087-098`, prints the ego's limb state, drawn cels and drawn pixels with and without the depth mask).

## Finding

The limbs are **not** the cause. After the costume reset the ego (costume 1) already has limb 0 (body, anim 110) and limb 1 (head, anim 4) set, and `Draw` returns both cels (head 8x9 at -4,-41). The head is hidden by the **depth mask**: the catalog hop to Room 34 chose the arrival of Room 33 object 543 (`walkTo=(64,73)`, the foot of box 0, mask 1). After scene 087, object 565 is in state 1 (and 566), and its mask covers x 32..120, y 0..47, so a head at y 33..42 is clipped (masked pixels 324 of 460). The head returns as soon as he walks east/south (f80: 446 of 446). Scene 087's costume reset and the animation code behave as in ScummVM; no generic limb fix is needed.

The earlier route note says that after 087 Room 34 is reached only through 36→35 (Room 37's exit refuses). That is the arrival from Room 35 object 587 (`walkTo=(330,79)`, box 8, outside object 566's mask: masked 426 of 448 pixels, head drawn). The catalog hop picked Room 33's arrival by a seeded pick because Room 38 (the previous room) has none.

## Change

`sequence-087-098` now hops to Room 35 first (+8 settle frames), then to Room 34, so `PickCatalogArrival` takes "fromPrevious" = Room 35's own code: the arrival the game's route would use. Steps 16 -> 18 unseen (`silentSteps` 18), frames 1158 -> 1180. The hops stay labelled exploration assumptions. Both runs complete (balanced scope, end boundary Script 98 +0x0618, Room 34). Walking west along the cliff still passes behind object 566's mask, as the room's art intends.

Not changed: the depth-mask code. Whether the original shows Bobbin clipped at (64,73) in this state is not established (no ScummVM run of that state was made); it is an open estimate, not a claim.
