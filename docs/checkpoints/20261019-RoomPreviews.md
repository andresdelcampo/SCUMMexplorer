# Background-first room previews, all games — 19 October 2026

## Plan

The user requests a shared room-selector pipeline: plain backgrounds promptly for all rooms, native first-visit updates on background workers afterwards, and persistent caching only when the complete batch is ready. Selecting a completed room must preserve its initialized thumbnail, including actors. Existing native initialization remains necessary even for complete-looking backgrounds. Baseline clean master `c0823b04`; integration branch `codex/room-previews-background-first` reuses the existing clean integration checkout.

Parent owns `MainWindow.xaml.cs`, a new room-card partial and shared requirements/records. One isolated worker owns only `RoomThumbnailCache.cs` plus its focused cache harness; a second owns an actual-window verification harness; a third reviews lifecycle/actor preservation read-only. No agent edits another owner's file. This explicit all-game request authorizes shared presentation changes; no family-specific engine behavior is changed. No ScummVM, game data, saves, user profile, junction creation or running user-application interference.

Implement two ordered passes with frozen worker-produced images, cancellation and game/generation guards. Preserve native first-visit computation and fallback semantics; keep the selector's completed first-visit image independent of current live-room compositing. Cache whole completed batches with source/build/language identity and safe cancellation/failure behavior. Private-cache tests and serial private-profile WPF checks cover v1–v5 backgrounds, gap-filling/actors, phase order, stale switches, cache completion and warm reopen. Merge current master in integration, preserve concurrent work, regenerate matrices, build and run the full suite with the three required external roots, then fast-forward master and refresh the normal executable. Stop at a clean checkpoint.

## Done

Requirements R-5.1/R-5.2 and new R-5.3b record the user-confirmed contract before implementation. Investigation found the old pipeline awaits LOOM draw batches before any card image and persists each room incrementally. Its setter also replaces the supplied initialized bitmap with the current room's object-only composition, explaining disappearing actors after selecting a card.

Final product source is `4421f0d`: raw backgrounds are decoded/published for all cards before cache reads, draw batches or native first-visit thumbnail work. A monotonic stage guard and removal of both current-room overwrite paths preserve completed artwork. Warm cache display still executes native prewarming. Detached native scenes are shared by card/view within the actual opened GameLibrary, so reopening changed source data does not reuse an older library's process-wide scenes. Existing heavier scene scans start only after the background pass; captured generation/game/token guards reject stale followups and reset previous scan progress.

Completed-cache provider `698028d0` (worker `dd0a934`) atomically replaces one whole ZIP, with exact room manifest, source/build/language metadata and cancellation checks. Failed/cancelled writes preserve the previous valid cache. Its private headless harness passes 32 checks plus a separate-process persistence check; builds have zero warnings/errors. LOOM helpers `4efc0ab0` (worker `2c6f1e8`) include native actor layers using existing masks/classes/extra planes and actual draw coverage. Four native crops pass, with 1,084 actor-layer pixels and 183,236 unchanged scenery pixels. No engine code or native state shortcut is added.

Independent review of both the initial implementation and final `4421f0d` finds no actionable issue. Three regenerated-matrix checks pass; support levels remain unchanged. The 14-family pure-background census passes all 1,002 rooms against direct source decoders. Eight bounded actual-window batches (15 rooms, cold/warm) pass while preserving complete source libraries: both Maniac and Zak editions, Indy3 EGA, Monkey floppy/CD and LOOM EGA. They verify phase order, exact native/cache pixels, stable actor-bearing cards, warm native prewarming, native drawer interaction and cancellation/game-switch isolation.

An initial LOOM window attempt failed the original three-second dispatcher heartbeat bound. The retained diagnostic found a synchronous early-click wait (2,679 ms). Final App-only `4421f0d` awaits the existing draw-analysis task before selected-room setup and removes synchronous waits; unchanged assertions now pass, early click 18 ms, maximum heartbeat gap 2,065 ms. The selected face/mouth details fill correctly. Final Zak early click is 15 ms, maximum heartbeat gap 82 ms. Normal live room setup still has costs; these observations do not promise universal latency. Core/tests source is identical to the initial `4348e10` freeze, and the App-only rebuild used BuildProjectReferences=false while the full regression ran. [Combined evidence](../../.verification/room-previews-final-20261019/README.md), [window packet and retained failures](../../.verification/room-previews-20261019/README.md).

Final required full regression: **1,657 passed, zero failed, five expected skips**, 1,662 total, 17m26s, all three external roots configured. After completion, current master remains `c0823b04`; merge reports already up to date. Final assembled solution build passes with zero warnings/errors (6.57 seconds), and regenerated-matrix checks pass again (3/0/0, 20 ms). Source/tests remain unchanged from `4421f0d`. Only dated records change after acceptance; clean promotion and executable refresh are recorded below.

## Not done and why

No requested implementation or acceptance check remains. No support level is promoted by this presentation feature. Native first-visit fallback and existing frame caps remain; this is not original-interpreter visual acceptance or a complete-playthrough claim. All-family native/click checks, full normal file-dialog startup, and performance guarantees for every room/hardware are outside the bounded window packet.

## Decisions made

Use background decoding rather than relying on unmeasured UI-thread speed. Keep native room/session behavior intact. A completed cache can accelerate display but does not remove upfront native state computation. Current-game playback state and selector first-visit artwork are separate.

## Proposed requirements and offers

None awaiting confirmation. R-5.1/R-5.2/R-5.3b implement the explicit user request. No additional feature or support promotion is bundled into this run.

## User checks

36 captures are retained, including the original failed attempts; the worker inspected seven. Parent inspected final LOOM plain/initialized images and the native Zak drawer image, confirming details fill and completed cards persist. To review manually, open a game and watch backgrounds appear before native details; click an initialized room and confirm its selector picture remains stable. This is optional review, not a pending approval gate.

## Cost and next run

One cache worker, one window worker, one independent reviewer and parent integration. Stop at the completed checkpoint; no automatic next feature.
