# Background-first room previews, all games — 19 October 2026

## Plan

The user requests a shared room-selector pipeline: plain backgrounds promptly for all rooms, native first-visit updates on background workers afterwards, and persistent caching only when the complete batch is ready. Selecting a completed room must preserve its initialized thumbnail, including actors. Existing native initialization remains necessary even for complete-looking backgrounds. Baseline clean master `c0823b04`; integration branch `codex/room-previews-background-first` reuses the existing clean integration checkout.

Parent owns `MainWindow.xaml.cs`, a new room-card partial and shared requirements/records. One isolated worker owns only `RoomThumbnailCache.cs` plus its focused cache harness; a second owns an actual-window verification harness; a third reviews lifecycle/actor preservation read-only. No agent edits another owner's file. This explicit all-game request authorizes shared presentation changes; no family-specific engine behavior is changed. No ScummVM, game data, saves, user profile, junction creation or running user-application interference.

Implement two ordered passes with frozen worker-produced images, cancellation and game/generation guards. Preserve native first-visit computation and fallback semantics; keep the selector's completed first-visit image independent of current live-room compositing. Cache whole completed batches with source/build/language identity and safe cancellation/failure behavior. Private-cache tests and serial private-profile WPF checks cover v1–v5 backgrounds, gap-filling/actors, phase order, stale switches, cache completion and warm reopen. Merge current master in integration, preserve concurrent work, regenerate matrices, build and run the full suite with the three required external roots, then fast-forward master and refresh the normal executable. Stop at a clean checkpoint.

## Done

Requirements R-5.1/R-5.2 and new R-5.3b record the user-confirmed contract before implementation. Investigation found the old pipeline awaits LOOM draw batches before any card image and persists each room incrementally. Its setter also replaces the supplied initialized bitmap with the current room's object-only composition, explaining disappearing actors after selecting a card.

## Not done and why

Implementation and validation are in progress. No support level is promoted by this presentation feature.

## Decisions made

Use background decoding rather than relying on unmeasured UI-thread speed. Keep native room/session behavior intact. A completed cache can accelerate display but does not remove upfront native state computation. Current-game playback state and selector first-visit artwork are separate.

## User checks

Recorded after actual-window validation; no mid-run approval gate.

## Cost and next run

One cache worker, one window worker, one independent reviewer and parent integration. Stop at the completed checkpoint; no automatic next feature.
