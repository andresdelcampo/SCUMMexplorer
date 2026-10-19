# Background-first room previews, all games — 19 October 2026

## Plan

The user requests a shared room-selector pipeline: plain backgrounds promptly for all rooms, native first-visit updates on background workers afterwards, and persistent caching only when the complete batch is ready. Selecting a completed room must preserve its initialized thumbnail, including actors. Existing native initialization remains necessary even for complete-looking backgrounds. Baseline clean master `c0823b04`; integration branch `codex/room-previews-background-first` reuses the existing clean integration checkout.

Parent owns `MainWindow.xaml.cs`, a new room-card partial and shared requirements/records. One isolated worker owns only `RoomThumbnailCache.cs` plus its focused cache harness; a second owns an actual-window verification harness; a third reviews lifecycle/actor preservation read-only. No agent edits another owner's file. This explicit all-game request authorizes shared presentation changes; no family-specific engine behavior is changed. No ScummVM, game data, saves, user profile, junction creation or running user-application interference.

Implement two ordered passes with frozen worker-produced images, cancellation and game/generation guards. Preserve native first-visit computation and fallback semantics; keep the selector's completed first-visit image independent of current live-room compositing. Cache whole completed batches with source/build/language identity and safe cancellation/failure behavior. Private-cache tests and serial private-profile WPF checks cover v1–v5 backgrounds, gap-filling/actors, phase order, stale switches, cache completion and warm reopen. Merge current master in integration, preserve concurrent work, regenerate matrices, build and run the full suite with the three required external roots, then fast-forward master and refresh the normal executable. Stop at a clean checkpoint.

## Done

Requirements R-5.1/R-5.2 and new R-5.3b record the user-confirmed contract before implementation. Investigation found the old pipeline awaits LOOM draw batches before any card image and persists each room incrementally. Its setter also replaces the supplied initialized bitmap with the current room's object-only composition, explaining disappearing actors after selecting a card.

Product frozen at `4348e10`: raw backgrounds are decoded/published for all cards before cache reads, draw batches or native first-visit thumbnail work. A monotonic stage guard and removal of both current-room overwrite paths preserve completed artwork. Warm cache display still executes native prewarming. Detached native scenes are shared by card/view within the actual opened GameLibrary, so reopening changed source data does not reuse an older library's process-wide scenes. Existing heavier scene scans start only after the background pass; captured generation/game/token guards reject stale followups and reset previous scan progress.

Completed-cache provider `698028d0` (worker `dd0a934`) atomically replaces one whole ZIP, with exact room manifest, source/build/language metadata and cancellation checks. Failed/cancelled writes preserve the previous valid cache. Its private headless harness passes 32 checks plus a separate-process persistence check; builds have zero warnings/errors. LOOM helpers `4efc0ab0` (worker `2c6f1e8`) include native actor layers using existing masks/classes/extra planes and actual draw coverage. Four native crops pass, with 1,084 actor-layer pixels and 183,236 unchanged scenery pixels. No engine code or native state shortcut is added.

Independent final review of `4348e10` finds no actionable issue. App build zero warnings/errors (4.32 seconds), solution build zero warnings/errors (4.38 seconds), three regenerated-matrix checks pass. The required full suite is running with sound/Indy3 EGA/MT-32 roots set. The window worker's 14-family pure-background census passes all 1,002 rooms against direct source decoders; eight bounded native window batches are in progress, preserving complete source libraries.

## Not done and why

Actual-window and full-suite acceptance are in progress. No support level is promoted by this presentation feature. Native first-visit fallback and existing frame caps remain; this is not original-interpreter visual acceptance or a complete-playthrough claim.

## Decisions made

Use background decoding rather than relying on unmeasured UI-thread speed. Keep native room/session behavior intact. A completed cache can accelerate display but does not remove upfront native state computation. Current-game playback state and selector first-visit artwork are separate.

## User checks

Recorded after actual-window validation; no mid-run approval gate.

## Cost and next run

One cache worker, one window worker, one independent reviewer and parent integration. Stop at the completed checkpoint; no automatic next feature.
