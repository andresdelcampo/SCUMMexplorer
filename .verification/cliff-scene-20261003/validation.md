# First sight of the Forge: start state and trigger test (3 October)

Probes: `CliffCamera` (runs a catalog sequence and logs camera/ego), `CliffBoxes` (Room 34 box table and next-box matrix).

## Facts established

- Room 34 is 624 px wide, so the camera runs 160..464. The scene (local200 +0035) pans to 464 (the far east edge), waits 300 jiffies, `roomScroll` 160..464, pans back to 376 (0x178), `endCutscene`. The decoded gate needs previous box 6, new box 8 and `464 > camera` (ScummVM `isGreater` jump-if-false; the handoff text "camera>464" had the sign backwards: the scene runs only while the camera is left of 464, which a camera at 376 satisfies).
- The explorer's walker, early state (layout B), clicks to the east: box 0/1 → 2 → 4 → 5 → 7 → 8 → 9, camera 160 → 264 (box 8) → 320 (box 9), settling at ego (355,106), camera 320. Walk box 9 ends at (378,134), where the way down to exit 558 starts; there the follow rule leaves the camera at 320 (ego strip 47 − screen start strip 20 = 27 < right trigger 30). A camera of 376 under the follow rule would need the ego at x ≥ 400, which no box has.
- The next-box matrix (CliffBoxes) has 6→8 = 7, 5→8 = 7, 7→8 = 8: no hop from 6 to 8 without box 7. Box 6 is a vertical line at x 246, y 64..80, invisible (0x80) in layout B, the only layout that starts the watcher (entry +035E needs D7B=0). So the decoded test is still unreachable by walking, as in the earlier validation; ScummVM's `getNextBox`/`walkActor` match the explorer's, so a mis-decoded gate or dead code remain the open possibilities. The user's account (the scene exists, far east end, camera about 376) is not contradicted, but nothing here confirms the trigger spot.

## Change (start state only, no trigger claim)

New labelled setup step `place` (`LoomExplorationSession.PlaceEgo`, replay step kind "place"): stands the ego at (x, y) (adjusted into the boxes) with the camera put on it as `loadRoomWithEgo` does (x rounded to a strip, inside the camera limits) and follow mode on; the camera is marked an estimate and the session lists "Explorer setup (explicit)". It waits for a settled session like the `staff` step.

`bonus-room34-local200` now starts with `place (378,134)`: Bobbin at the east end of box 9, camera 376 (the user's reference), then the direct start of the scene. Run: camera 376 → 464 → back to 376, ego faces east (0xF9) and stays at (378,134), 115 frames, balanced scopes, boundary end +0056. The old direct start (ego at (64,74), camera 160) panned 160 → 464 → 376 and then snapped back to 160. The picker text says the placement is an assumed setup and that the walker's own camera at that end is 320.

Tests: `ThePlaceStepStandsTheEgoAtTheCliffEndWithTheCameraOnIt`, ghost-scene test updated for the Room 35 arrival.

## Revision after user feedback (same day)

- The `place` teleport on the lookout entry is withdrawn (catalog entry back to its direct start, 132 frames): the user wants a generic per-scene start placer later, not a one-off teleport. `PlaceEgo` / step `place` stay in Core as an unused, tested building block for that design.
- The user's memory of Bobbin walking east to the edge and back is **script 098** (the ghost scene): +0x040A starts room script 206 (camera limits 160..464, pan to 464, ego walks to (388,88)); Rusty walks to (366,79); "Caray."; +0x0596 Bobbin walks back to (244,88) and the camera pans to 160. The standalone lookout (local200) may be a different or dead scene.
- The blue frame in the exits view is **object 560**: hotspot (288,0) 32x144, walk point (288,81), no image. Verb 0x37 (walk) starts script 98 while DC7=0; verb 0x36 (look) starts local205 ("Mejor no te vayas muy lejos") while DC7 != 0. In the early layout B it is untouchable (so a click shows "action tried; unresolved"). Probe `CliffHotspot` lists Room 34's directory records. Whether it is also the trigger of the user's "cliff" scene is open: it is the object, not a box watcher.
