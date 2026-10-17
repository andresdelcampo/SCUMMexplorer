# Maniac Mansion and Zak McKracken: S5 checkpoint (17 October 2026)

## Plan

Run S5 for ManiacEnhanced, ManiacClassic, ZakEnhanced and ZakClassic, each in a separate worktree. Three family agents run concurrently; ZakClassic takes the first available slot. ManiacEnhanced owns shared first-visit engine changes; the orchestrator owns App integration and overarching records. ManiacClassic owns the shared Maniac documentation, ZakEnhanced owns Zak documentation until its handoff. Other families and NEXT_SESSION_PROMPT.md remain the parallel orchestrator's scope.

Each item gets one all-room audit, source comparisons for at least ten rooms including script-heavy rooms, and state regression tests for at least three rooms. Two distinct failed attempts at a blocker trigger the documented hard stop; do not fabricate state or fill pixels. No overall token/time budget was requested. Share prerequisite commits early, preserve labelled previews and estimates, and stop at a clean S5 checkpoint without starting S6.

ScummVM execution remains prohibited by the user's coordination rules. Source comparisons and rendered app checks cannot be described as original-game picture comparisons; the missing visual comparison stays explicit. Objects remains Partial until S6 click responses as required by the matrix.

Before integration, merge master in the worktree, preserve both orchestrators' changes, regenerate the matrix with SCUMM_UPDATE_SUPPORT_MATRIX=1, build, and run the full suite with the external sound, Indy3 EGA and MT-32 fixtures. Require zero failures, then fast-forward master; repeat if master moves. Never force, stash, create Games junctions or remove worktrees in this run.

## Done

Pending family delivery.

## Not done and why

S6, S7 and S9 are outside this S5 run; S8 was not requested.

## Decisions made

Exclusive ownership serializes shared engine/UI changes while independent family audits run in parallel.

## Proposed requirements to confirm or reject

None so far.

## User checks

To be recorded from the completed audits. No ScummVM runtime comparison is claimed.

## Cost

Three concurrent family agents and one orchestrator, with the fourth family queued for the next agent slot. No user budget supplied.

## Next run

To be determined from the verified S5 results.
