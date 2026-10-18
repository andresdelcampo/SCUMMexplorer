# Maniac/Zak S9 checkpoint — 18 October 2026

## Plan

Scope: S9 edition checks for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced, widening the completed S4b–S7 evidence only where local copies exist. Inventory the available data before claiming alternate-release coverage. The current root has one folder per family: classic English Maniac, enhanced Spanish Maniac, classic English Zak and enhanced English Zak. Separate families do not count as alternate editions of one family. Missing copies are recorded as blocked checks, never inferred successes.

Three workers run concurrently in separate worktrees, one family per item. ManiacClassic and ManiacEnhanced own separate tests/evidence; Classic coordinates Maniac tracker changes. ZakEnhanced completes its item before taking ZakClassic in a separate worktree, coordinating Zak tracker changes. Parent owns shared engine/App files, registry/matrices, checkpoint, CURRENT_WORK, CHANGELOG and dedicated handoff. Workers report a demonstrated engine issue before editing shared files. No other orchestrator families, ScummVM execution, game-data mutation, Games junctions or worktree removal.

Use the relevant edition checks from `.verification/editions-20261005`: content identification, resource/script/text/font differences, own-data boot/play/scene assumptions and fail-closed preview behavior. Reuse S7 evidence rather than repeat broad audits without a plausible difference. One bounded family harness and meaningful regression per new compatibility claim; log two failed approaches and move on. No user budget was supplied; stop at one complete bounded pass per available family and record missing copies honestly.

Integration protocol: merge master in the integration worktree, preserve both orchestrators' changes, regenerate README/REQUIREMENTS using SCUMM_UPDATE_SUPPORT_MATRIX=1, build and run the full suite with the three external fixture variables, require zero failures, then fast-forward master. Repeat if master moves. S8 and seeds remain opt-in.

## Done

Pending.

## Not done and why

Pending.

## Decisions made

Pending.

## Proposed requirements to confirm or reject

No new proposal planned. Existing S6/S7 proposals remain in force.

## User checks

Pending.

## Offers

S8 state design and shipped seeds only on explicit request.

## Cost

Pending.

## Next run

Pending S9 results.
