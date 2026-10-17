# Monkey Island 2 support status

Family: Monkey2 | Fixture: `Games/mi2 talkie en` ( `mi2 mt32 en` and `mi2 sb16 en` for the Roland and Sound Blaster data) | Last updated: 17 October 2026 | Owner: S4b/S5 Monkey2 agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (before this file) | Rooms | | |
| S2 Reading | done (before this file) | Costumes, Scripts, Texts | | polish: ~3% of v5 script blocks unresolved |
| S3 Sound | in progress, **partial** | Sound | [v5-sound-leftovers-20261014](../../.verification/v5-sound-leftovers-20261014/README.md) | listening check listed in the run report: yes; result: pending |
| S4b Session | **done (17 Oct)** | – (unchanged: S4b changes no cell) | [monkey2-session-20261017](../../.verification/monkey2-session-20261017/README.md) | boots to the campfire (Room 4) in 3409 frames, player in control, unsupported 0, one assumed click (the full game); `ScummMonkey2SessionTests` |
| S5 Objects | **done (17 Oct)** | Objects (partial, unchanged until S6) | [monkey2-objects-20261017](../../.verification/monkey2-objects-20261017/README.md) | 107 of 110 rooms without a gap (104–106: image-holder rooms), 0 not followed; Rooms 39, 55, 79 pinned (`ScummMonkey2FirstVisitTests`); ScummVM picture comparison not done (user check) |
| S6 Play | not started | Play | | the script-driven sound hooks wait for this |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | mi2 classic es, mi2 talkie es are local |

Blocked on: S6 for the hooks that wait for a script id (jump hooks with id other than 0, part on/off 50, volume 51, program 52, transposes 49/53), script loop control, markers, `isSoundRunning` and music-timer waits, concurrent-sound special cases (`startSound_internal`: sounds 100, 107, 113, 115, 118).
Next: S6 (a Play requirement for this verb game; the play screen needs the state images and transparency; the iMUSE player commands and markers soundKludge steers). S4b/S5 done 17 Oct.
Open uncertainties: S4b: soundKludge player commands, markers and the trigger queue are answered 0 as labelled estimates; VAR_MUSIC_TIMER is not driven by the iMUSE song; the boot's end (Room 4) not compared with the original. S3: loops are taken at the from-point to the tick (ScummVM: first 250 Hz timer step at or after it); where a looping song stops in the wave (3 minutes) is ours.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 14 Oct | S3 | Loop sysex 0x7D 80 | keep "end here" / port `setLoop` | port: ScummVM sets a loop (count, to, from) and jumps at the from-point; "end here" rendered some songs (sound 120) as a tenth of a second | `ScummImuseTimeline` case 80 |
| 14 Oct | S3 | Hooks 49 to 53 with id 0 were ignored | keep / act at once | act at once as `maybe_*` do; changes what is heard (parts switched off by a hook stay off in the default path) | remove the cases 49 to 53 in the three drivers |
| 14 Oct | S3 | Loop length | one pass / 3 minutes | 3 minutes at the first jump back at or after it, like the preview limit of the other devices | `MinLoopSeconds` |
| 2026-10-14 | S3 | Raise Sound to Full? | keep partial until S6 hooks / raise now | raised: the user listened and accepted the v5 sounds (loops, hooks with id 0, MI1 CD track list); the script-driven hooks stay a known limit for S6 | revert the one registry line |
| 2026-10-17 | S4b | Fixture | the conformance fixture `mi2 talkie en`; the plain `mi2 mt32 en`/`mi2 sb16 en` | `mi2 talkie en`, as `SupportConformanceTests` names it (brief); it is the fan "Talkie CD 0.2 beta" patch of the DOS release (Script 1 +0x01C5), whose scripts differ from the plain copies (S9 item) | change the folder in the two test classes |
| 2026-10-17 | S4b | The Lite / full choice (Script 130 +0x034A waits for `var[0x06A]` = -2) | Lite; full; leave the boot waiting | full ("Monkey Island 2", object 0x484, Script 131 +0x0067), §4.5 and the brief; one click through the game's input script, labelled assumed input (`ScummBootInputPlan.ClickObject`, R-15.5/R-15.7) | `ClickObject: 0x485` takes Lite; null leaves the boot at the frame limit |
| 2026-10-17 | S4b | Copy protection | answer the recipe wheel; none needed | none: with boot parameter 0 (ScummVM's for DOS MI2) Script 128 +0x0000 and Script 130 +0x005F skip it in this edition; the other copies may differ (S9) | – |
| 2026-10-17 | S4b | `soundKludge` (817 flushes, ~1200 commands in the scripts) | report unsupported; follow iMUSE fully; answer by the doCommand table with labelled estimates | the doCommand table (imuse.cpp): start/stop/stop-all are sound events, status from the sound clock, player commands for a silent song -1, the rest 0 labelled "not followed" | remove `ScummBoundedScriptExecutor.SoundKludge.cs` and its two call sites |
| 2026-10-17 | S4b | How long a v5 sound plays for the session | unknown (the boot waits for ever); its iMUSE AdLib part unrolled by S3's timeline | the timeline: loops never end, the rest end with their last event; estimates where scripts steer the song | drop `ImuseLifetime` in `ScummGameSession` |
| 2026-10-17 | S4b | v5 objects with an image per state (Room 108: 15 images for object 1154) threw in the views | number the states as the browser does; one image per object chosen by state | by state (`ReadObjectImages(room, states)`), as `drawObject` reads `IMnn`; the plain call gives IM01 | revert 18bd3d3 |
| 2026-10-17 | S4b | Where the boot ends (Script 1 ends in Room 87) | when Script 1 ends; when the player has control | when the player has control, as both Monkey Island 1 families (`BootEndsWhenPlayerHasControl`) | drop Monkey2 from the property |
| 2026-10-17 | S5 | First-visit scenes for which v5 games | drop the family term (Indy 4 too, whose S4b runs in parallel); add Monkey2 | add Monkey2: Indy 4's boot is not followed yet here | remove Monkey2 from `HasFirstVisitScenes` |
| 2026-10-17 | S5 | Rooms 104–106 keep colour-0 rectangles | fill; leave and write down | write down: no background or script of their own (image holders), R-1.9 forbids a fill | – |
| 2026-10-17 | S5 | Object transparency (Room 55's guests in magenta boxes; not a counted gap) | leave for S6; implement gdi.cpp's transparent codecs | implement now (data: the strip's codec and the room's TRNS), the first visit must look right | revert e75ab60 |
| 2026-10-17 | S4b/S5 | Reconcile with S4b/S5 Indy4 (merged to master in parallel; same features written twice) | keep both; keep one of each | one of each: Indy4's `SoundKludgeEvent` (start 8, stop 9, stop-all 10/11 as sound events) and its iMUSE AdLib-timeline lifetimes (`ScummGameSession.ImuseLifetime`, with its `ScummSoundClock.Status` labels; Monkey2's ROL fallback dropped); Indy4's `ReadObjectImages(room, states)` (a state with no image of its own draws nothing), its `State` field, sentence wait and `getActorScale`; Monkey2 keeps `ApplySoundKludge` (`VAR_SOUNDRESULT`, -1 for a song not playing, labelled estimates), the transparent strips, the click boot input and the v5 `setPalColor` | re-add the dropped Monkey2 versions from 9c47419, 18bd3d3, cbafbd5, 9e1b0f4 |
