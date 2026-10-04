# Room 02 mountain-side depth mask (4 October 2026): matches ScummVM

Question (user): Bobbin disappears on the walk from Room 02 to the village before reaching box 14's end; is the explorer using the wrong costume size, or a wrong mask?

Checked on the **English** release (`Games/loom ega en`) with `.verification/DemoProbe` (`walk 2 377 9 mask`, `adjust`, `objects`, `verbs`, `shot`) and the bundled ScummVM (`scummvm/`, debugger commands `actors`, `box`, `objects`).

- **Size is right.** On the exit walk the ego wears costume 2 in boxes 5–10 and costume 3 from box 12 on (script 045 starts script 017 per box). Costume 3 draws about 24 opaque pixels, and the mask clipping counts the pixels the renderer draws, so the drawn size and the clipped size are the same.
- **Box data is identical.** ScummVM's `box` table equals ours for all 15 boxes (corners, mask, flags). Boxes 0-2 mask 0, boxes 3-14 mask 1; box 14 is a line from (540,108) to (568,114).
- **Rules are ScummVM's.** `getMaskFromBox` (box 255 gives 1 in v3), `drawActorCostume`'s z-plane choice, `setBox`/`kInvalidBox`, `Actor_v3::actorWalkStep`/`calcMovementFactor` and the mask run-length decode were compared with ScummVM's source (read from GitHub); object header walk points are read from the same fields.
- **ScummVM actor table.** `actors` with Bobbin at the stop: actor 1 at (550,110), box 14, costume 3, frame 10, not moving: the same state as ours. A click just right of the exit object's rectangle (object 153, x 376-552) is a plain walk to the click point clipped onto box 14, which stops at about (550,110) and stays visible there; a click on the exit hotspot walks on to the box's end and runs verb 0x38 (`loadRoomWithEgo` Room 03).
- **ScummVM's exit walk, filmed.** Clicking the far-left exit and capturing frames 17 ms apart: Bobbin is visible at x about 550 at 98 ms, gone by 129 ms (one game frame, x about 548), and stays gone until Room 03 appears about 1.5 s later. Our explorer: 17 of 24 pixels at 550, 1 of 24 at 548, 0 from 546 on, and the same until the room change. The mountain pass is a place he walks behind (the red mask in `room2` renders covers the cliff); no carved gap exists on his path.

Result: nothing to change. Object 157 (544,88, 16x32, class 24 in ScummVM) is only the opening's drawing (script 106) and has no part in this.
