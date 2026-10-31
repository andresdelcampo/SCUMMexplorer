# Maniac Mansion and Zak McKracken: S4b checkpoint (17 October 2026)

## Plan

Start with S4b ManiacEnhanced (`D:/Juegos/Lucas/mmansionE ega es`) and ZakEnhanced (`D:/Juegos/Lucas/zakE ega en`), one worktree agent per item. They require the same new v2 data source, dialect and executor partial: Maniac owns that foundation exclusively. Following the user's request for more parallelism, Zak analyzes its scripts and writes its own tests/harness/docs in parallel, then merges the foundation and sends shared implementation needs to its owner. A third worktree agent reviews the v1 layout, then implements the classic adapter, cutscene restoration and boot checks in separately owned files. Shared-file changes are serialized; independent family work proceeds in parallel.

Scope is the four ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced families. Other families belong to the parallel orchestrator. Shared-file edits remain small and additive. Existing classic costume levels are preserved; corrected decoding refreshes Zak's derived/estimated breakdown below.

Each item advances through green commits. The shared foundation also enables both classic S4b checks in this run. S4b changes no support-matrix cell. This run stops at the S4b checkpoint; the next stages are S5, S6 using REQUIREMENTS §7.1, S7 and S9. S8 is excluded.

Integration: merge current master inside each agent worktree, keep both orchestrators' changes, regenerate README/REQUIREMENTS tables with `SCUMM_UPDATE_SUPPORT_MATRIX=1`, build and run the full suite with sound, Indy3 EGA and MT-32 fixture variables. Require zero failed tests before fast-forwarding master. Repeat if master moves; never force. No Games junctions and no ScummVM executable runs.

## Done

- Added data sources beside the v1/v2 readers, a distinct v1 dialect, retained object-code entry offsets, corrected old operand decoding and an isolated v2 executor partial. Old room/index/image/verb parsing is covered for all four fixtures.
- Added v1/v2 engine-variable roles, fixed input/sentence/entry script dispatch, camera and mouse units, cutscene input state, family-sized actor tables, Zak's engine-defined machine speed, and native PC-speaker lifetime/music-timer support. Object scripts distinguish ordinary, special and background slots.
- Corrected the v2 object-state branch polarity: one real click now produces one party-selection response. The enhanced Maniac diagnostic uses three labelled player clicks, not direct writes to party or story variables.
- The classic adapter and factory work is recorded in [the v1 record](../../../.verification/maniac-v1-adapter-20261017/README.md). The shared decoder corrections are in [the decoder record](../../../.verification/maniac-decoder-20261017/README.md). Enhanced-family records are [Maniac](../../../.verification/maniac-session-20261017/README.md) and [Zak](../../../.verification/zak-session-20261017/README.md).
- No support-matrix cells are raised. All four families complete S4b for the checked fixtures and routes, with player control retained for 120 additional frames. The Maniac route explicitly selects Syd and Bernard through three labelled player clicks.

| Family | Bounded endpoint | S4b evidence |
|---|---|---|
| ManiacClassic | Room 44, control restored after 1567 post-constructor frames and stable for 120 more | S4b complete for the selected party. The real 200-tile Room 49 is now accepted by the v1 reader. |
| ManiacEnhanced | Room 44, control restored after 1490 post-constructor frames and stable for 120 more | S4b complete for the selected party. Native text counting, verb slots and actor operand normalization were corrected. |
| ZakClassic | Room 1, control restored after 1804 constructor frames and stable for 120 more | Full opening, no assumed player input, no unsupported or unresolved operation in the checked trace. |
| ZakEnhanced | Room 1, control restored after 1828 constructor frames and stable for 120 more | Full opening selected by the engine's machine-speed value; no assumed input, unsupported or unresolved operation in the checked trace. |

Both Maniac constructors first wait 5000 frames at the party menu. Those frames are reported separately from the explicit input route. Seeded random choices and their derived sound/movement effects remain labelled estimates in all families.

Integration milestones: `d7236c7` and `9937aa6` record ManiacEnhanced acceptance and its final evidence; `f3a0a56` records ManiacClassic acceptance, with native actor defaults in `022d4c0`; `b21c3b5` records both Zak editions' stable first playable room. Shared source corrections include `e423953` (indirect-read estimate provenance). Merge `05d2c59` preserves the parallel orchestrator's master work through `ea101d8`. See [the integration record](../../../.verification/maniaczak-integration-20261017/README.md) for commands and regression results.

## Validation

Against integrated source `b21c3b5` and master `ea101d8`: matrix regeneration 3 passed, 0 failed; solution build 0 warnings, 0 errors; full suite **1125 passed, 0 failed, 5 skipped (1130 total)** with the required sound, Indy3 EGA and MT-32 fixture variables. The suite reported 5 m 7 s. Skipped tests do not establish coverage. Exact commands, artifacts and the initial dependency-restore note are in the integration record above. No original ScummVM run or visual/timing comparison is claimed.

## Not done and why

S5, S6 (verb-based play), S7 and S9 remain not started in this S4b run. S2 costume levels are unchanged: ManiacClassic remains accepted; fresh ZakClassic decoding yields 24 derived costumes and 11 labelled estimates (4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32), superseding the old 25/10 breakdown.

## Decisions made

- Divide implementation by exclusive file ownership: the Maniac agent owns the v2 adapter/executor; the Zak agent owns the native PC-speaker clock and Zak evidence/tests; the classic agent owns decoder corrections and actor capacity; the orchestrator owns session variable roles, hooks and cutscene/input plumbing. Family boot validation follows integration of those prerequisites.
- Keep the supplied ScummVM executable untouched; distinguish source evidence from runtime observation.
- Preserve the other orchestrator's family records and `NEXT_SESSION_PROMPT.md`; use `NEXT_SESSION_PROMPT_MANIAC_ZAK.md` for this work.

## Proposed requirements to confirm or reject

None.

## User checks

None required to unblock this checkpoint. No original-game run, listening, visual or timing comparison is claimed. Sound priority/queued restart, held-object code and executing-object-slot replacement remain explicit limits where applicable; see the family records. Do not request a user play check until the boot is executable through its first playable room.

## Offers

S8 remains outside this run. No shipped cutscene seed requested.

## Cost

Three worktree agents plus the orchestrator worked in parallel with exclusive ownership of shared files. Most work established the shared v1/v2 foundation; all four fixture boots could then be checked. No token or elapsed-time budget was supplied; exact usage is not available.

## Next run

Continue with S5 for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced, one family per worktree agent and one owner per shared engine file. Audit first-visit objects against each fixture's own scripts, retaining unresolved branches and estimates. Then proceed in order to S6 verb-based play per REQUIREMENTS §7.1, S7 and S9. Read [the dedicated handoff](../../../NEXT_SESSION_PROMPT_MANIAC_ZAK.md). Preserve the other orchestrator's families and follow the master-merge/full-suite/fast-forward procedure above.
