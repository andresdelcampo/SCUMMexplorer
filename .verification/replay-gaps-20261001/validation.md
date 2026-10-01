# Replay coverage checkpoint, 1 October

Investigation only: no new starts promoted or Core behavior changed. All 45 incomplete roots individually rerun to 3000 frames, with live script slots, freeze/delay state, actors, sounds and variables at 0/100/1000/2500/3000 frames. [Per-root audit](audit.md), [compact source contexts](stalls.json); full traces preserved in ignored `Exe/VerificationSession-20261001/replay-gap-probe-1/`. No variable/class/inventory/position/global-script injection.

38 roots poll var0A4; four further roots wait on the same sound helper. Script 040 starts its resolved sound at +0x0038, polls isSoundRunning at +0x003D and clears var0A4 at +0x0047. The executor keeps started sounds active indefinitely, explicitly estimated. These 42 stalls require generic source-derived sound lifetimes, not forced bits/sound IDs or larger probe bounds. Script 054 actually ends at +0x00C4; following 029 waits for 040 at +0x00BE. Script 104 also finishes before its helper stall. Existing AdLib EstimateDuration stops at the first loop boundary; it cannot blindly establish sound lifetime. Distinguish finite/looping/malformed tracks and effects, validate native clocks, preserve device/mute behavior and audit real-clock WPF before promotion.

One incomplete root (Room 38 object 610 look, script 087) polls actor 5's walk box at +0x047C. Two Room 41 roots have unmatched boundary counts after changing rooms. Keep all unresolved.

Source watched-box route findings (fresh catalog arrival, actual game input):

* Room 13 input (112,107), decoded box-2 center, selects the jet and stops in box 3. Watcher `[0x0018]` requires object 325 class24 ABSENT. Input (112,131), below the selectable target, crosses boxes 3→2 and local 201 actually chains 053. At frame 221 only Room 13 was entered; no matching endCutscene recorded. Uncertainty: 053 +0x0107 Room 14 load follows a seeded pick and remains possible, not dispatched. Reached trigger is not completed replay; no room 12/14 membership inferred.
* Direct response 325/0x42 is a labelled explorer prelude, not a played draft: local 202 sets class24 and stops watcher 201. Subsequent box-2 input does not establish 053. Never substitute prerequisite writes.
* Room 36 input (349,99), box-9 center, crosses 12/11/10/8/7 and local 200's short suppression scope +0x00A6..+0x00CA; misses box 9 and leaves object 599 mask 0x00800002. Input (349,123) ends in box 11. Neither establishes actual game-click firewood; existing labelled direct-verb/arrival/click recipe retained.

Route outputs preserved under ignored watched-box-routes-1/-2/-3. RouteProbe's console `scene` means any scope begun, NOT successful completion. Traces/boundaries/uncertainties govern promotion. Both diagnostic executables built successfully with unattended failure policy. Main App/Core unchanged since the completed room task's Release build and 249 Core / 20 room replay / 16 doorway / 19 room-audio / 18 input checks.

Coverage remains 291 starts, not full-game coverage: completed automatic previews reach 139/235 sites, 79 owners have outstanding sites, 123 static edges unresolved. Ending/late-game owners without literal roots still need reached routes. User requested pause at this completed research checkpoint.
