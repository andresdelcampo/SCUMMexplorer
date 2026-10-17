# Maniac Mansion and Zak McKracken: S5 checkpoint (17 October 2026)

## Plan

Run S5 for ManiacEnhanced, ManiacClassic, ZakEnhanced and ZakClassic, each in a separate worktree. Three family agents run concurrently; ZakClassic takes the first available slot. ManiacEnhanced owns shared first-visit engine changes; the orchestrator owns App integration and overarching records. ManiacClassic owns the shared Maniac documentation, ZakEnhanced owns Zak documentation until its handoff. Other families and NEXT_SESSION_PROMPT.md remain the parallel orchestrator's scope.

Each item gets one all-room audit, source comparisons for at least ten rooms including script-heavy rooms, and state regression tests for at least three rooms. Two distinct failed attempts at a blocker trigger the documented hard stop; do not fabricate state or fill pixels. No overall token/time budget was requested. Share prerequisite commits early, preserve labelled previews and estimates, and stop at a clean S5 checkpoint without starting S6.

ScummVM execution remains prohibited by the user's coordination rules. Source comparisons and rendered app checks cannot be described as original-game picture comparisons; the missing visual comparison stays explicit. Objects remains Partial until S6 click responses as required by the matrix.

Before integration, merge master in the worktree, preserve both orchestrators' changes, regenerate the matrix with SCUMM_UPDATE_SUPPORT_MATRIX=1, build, and run the full suite with the external sound, Indy3 EGA and MT-32 fixtures. Require zero failures, then fast-forward master; repeat if master moves. Never force, stash, create Games junctions or remove worktrees in this run.

## Done

Source first-visit previews are implemented for all four families. All **221 rooms open without audit errors**; 216 have no detector findings. Original-game visual acceptance remains open. Objects stays Partial; no support cells are raised.

| Family | Rooms open | No detector findings | Settled / left room / frame limit | Evidence |
|---|---:|---:|---|---|
| ManiacClassic | 52/52 | 51 | 51 / 0 / 1 | [record](../../.verification/maniacclassic-objects-20261017/README.md) |
| ManiacEnhanced | 53/53 | 53 | 51 / 0 / 2 | [record](../../.verification/maniacenhanced-objects-20261017/README.md) |
| ZakClassic | 58/58 | 57 | 50 / 3 / 5 | [record](../../.verification/zakclassic-objects-20261017/README.md) |
| ZakEnhanced | 58/58 | 55 | 50 / 3 / 5 | [record](../../.verification/zakenhanced-objects-20261017/README.md) |

Each family has at least ten source comparisons and three room-state regressions. Maniac's reviewed Syd/Bernard input recipe lives outside the generic engine and has its own cache. The app's cards, room view and hotspot labels use reached first-visit state; a failed preview falls back to the plain background. Technical explanations remain in Advanced.

Shared source corrections cover intrinsic state bit 8, untouchable bit 2, retained duplicate directory images with last-entry script lookup, hexadecimal pseudo-room operands, native arrival coordinate units, current-room settling, old actor initialization and v1 costume frames. Final review additionally corrected queued duplicate images: queuing the last entry must retain earlier directory images. The 38 focused selection/old-rule tests pass.

All 16 real-window checks pass: Maniac rooms 44, 1, 18 in each edition, and Zak rooms 1, 5, 7, 9, 23 in each edition. Captures exposed an initial head-only actor; the corrected final captures show all three Maniac party members with full bodies. A static pose match alone was insufficient evidence. [Integration commands and results](../../.verification/maniaczak-objects-20261017/README.md).

Family evidence commits in integration: `6ff66f2` (ManiacClassic), `a7e16e9` (ManiacEnhanced), `c8e44b6` (ZakEnhanced), `6adfd7c` (ZakClassic). Shared implementation: `8213210`, `073b5b8`, `8583798`, `ace90b7`, `ff532db`, `fff8d6d`, `6e3287f`; UI: `32bfd04`, `2962084`.

Integration validation against master `9bc3ec8`: merge already current; regenerated matrix unchanged, 3 matrix tests pass; solution build zero warnings/errors; full suite **1150 passed, 0 failed, 5 skipped (1155 total)** in 6 m 3 s with all three required external fixture variables. The five skipped diagnostics do not establish coverage. The full suite includes the final queued-duplicate correction and all four family regressions. Exact command and artifact path are in the integration record. Fast-forward master only after confirming its head has not moved.

## Not done and why

S6, S7 and S9 are outside this S5 run; S8 was not requested.

Native runtime/picture comparisons were not performed because the user prohibited ScummVM execution. Source comparisons and app captures do not establish visual equivalence. Catalog arrivals are labelled unplayed routes; Maniac's party is an explicit assumption. Dark rooms retain the existing shown-lit preview with its Advanced note. Native v1/v2 movement drawing offsets (+8/+16 in `prepareDrawActorCostume`) need further alignment review; full actor bodies do not prove exact placement. Zak camera placement remains uncertain in rooms 3 and 33.

Retained detector findings: classic Maniac Room 18 has six black coin-slot rectangles already present in its background; classic Zak Room 44 and enhanced Zak rooms 13, 16, 44 retain black rectangles under inactive untouchable objects. No script evidence justifies activating those images or filling pixels. These are investigation findings, not approved original-picture matches. Maniac frame bounds: classic Room 30, enhanced rooms 30/45. Both Zak editions leave rooms 8/11/40 and reach the bound in 27/28/32/36/42.

## Decisions made

Exclusive ownership serializes shared engine/UI changes while independent family audits run in parallel.

Keep every reached state and disclosed uncertainty; do not optimize detector counts by inventing state. Preserve ManiacClassic's accepted costume status and ZakClassic's 24 derived / 11 estimated breakdown. No game data, ROMs or runtime captures are committed. No worktrees are removed.

## Proposed requirements to confirm or reject

None.

## User checks

Open the built app at `.worktrees/maniaczak-s5/Exe/Debug/SCUMMexplorer.exe`. Browse the four fixture folders in the family records. Inspect the 16 rooms listed above; inspect retained findings in ManiacClassic 18 and Zak 13/16/44, and Zak camera placement in 3/33. Normal mode should show the room and readable hotspots; Advanced should disclose party/arrival/lighting assumptions. Original-game visual acceptance is still required separately; no ScummVM runtime comparison is claimed.

## Cost

Three worker agents and one orchestrator; the Zak worker handled enhanced and classic items in separate worktrees and commits. Audits were repeated when concrete source corrections changed results. No user budget supplied. A rate-limited review was resumed through a follow-up message.

## Next run

Resolve the disclosed S5 visual/alignment/camera findings, then implement S6 verb-based play per REQUIREMENTS section 7.1 with one family per worktree and exclusive shared-file ownership. Do not infer play acceptance from a successful catalog visit. Continue to S7 and S9 only in their own runs. Read `NEXT_SESSION_PROMPT_MANIAC_ZAK.md`; preserve the other orchestrator's handoff and families.
