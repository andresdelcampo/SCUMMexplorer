# Maniac Mansion and Zak McKracken: S4b checkpoint (17 October 2026)

## Plan

Run S4b ManiacEnhanced (`Games/mmansionE ega es`) and S4b ZakEnhanced (`Games/zakE ega en`), one worktree agent per item. They require the same new v2 data source, dialect and executor partial: Maniac owns that foundation exclusively. Following the user's request for more parallelism, Zak analyzes its scripts and writes its own tests/harness/docs in parallel, then merges the foundation and sends shared implementation needs to its owner. A third worktree agent performs a read-only ManiacClassic v1 layout and boot review. Shared engine implementation remains serialized; independent family work proceeds in parallel.

Scope is the four ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced families. Other families belong to the parallel orchestrator. Shared-file edits remain small and additive. Existing classic costume evidence is preserved: ManiacClassic was accepted on source evidence; ZakClassic retains ten labelled estimates.

Each item stops at its next green commit if a documented hard stop applies. S4b changes no support-matrix cell. After these v2 items, the roadmap continues with classic S4b, S5, S6 using REQUIREMENTS §7.1, S7 and S9. S8 is excluded. This run stops at the S4b checkpoint rather than starting dependent stages without their prerequisites.

Integration: merge current master inside each agent worktree, keep both orchestrators' changes, regenerate README/REQUIREMENTS tables with `SCUMM_UPDATE_SUPPORT_MATRIX=1`, build and run the full suite with sound, Indy3 EGA and MT-32 fixture variables. Require zero failed tests before fast-forwarding master. Repeat if master moves; never force. No Games junctions and no ScummVM executable runs.

## Done

- Added data sources beside the v1/v2 readers, a distinct v1 dialect, retained object-code entry offsets, corrected old operand decoding and an isolated v2 executor partial. Old room/index/image/verb parsing is covered for all four fixtures.
- Added v1/v2 engine-variable roles, fixed input/sentence/entry script dispatch, camera and mouse units, cutscene input state, family-sized actor tables, Zak's engine-defined machine speed, and native PC-speaker lifetime/music-timer support. Object scripts distinguish ordinary, special and background slots.
- Corrected the v2 object-state branch polarity: one real click now produces one party-selection response. The enhanced Maniac diagnostic uses three labelled player clicks, not direct writes to party or story variables.
- The classic adapter and factory work is recorded in [the v1 record](../../.verification/maniac-v1-adapter-20261017/README.md). The shared decoder corrections are in [the decoder record](../../.verification/maniac-decoder-20261017/README.md). Enhanced-family records are [Maniac](../../.verification/maniac-session-20261017/README.md) and [Zak](../../.verification/zak-session-20261017/README.md).
- No support-matrix cells are raised. S4b remains incomplete for all four families; the implemented foundation is retained at a green checkpoint.

## Not done and why

| Family | Bounded endpoint | Remaining S4b work |
|---|---|---|
| ManiacClassic | Room 45, Script 1 +0x01F8 waiting for party selection | Complete its input route and classic cutscene camera restoration; establish stable gameplay control. |
| ManiacEnhanced | Real selection clicks reach the introduction through Rooms 33, 49 and 44, then leave the cutscene | Script 164 +0x0004 requires v2 verb-slot deletion. Native character counts and text normalization were corrected; no stable first playable room is yet claimed. |
| ZakClassic | Script 1 +0x0038, before a room | Implement the v2 verb-slot deletion operation and subsequent reached blockers. |
| ZakEnhanced | Script 1 +0x003B, before a room | Same v2 verb-slot semantics; validate the full introduction selected by the engine's machine-speed value. |

S5, S6 (verb-based play), S7 and S9 remain not started for these families because S4b is incomplete. S2 costume levels are unchanged: ManiacClassic remains accepted; fresh ZakClassic decoding yields 24 derived costumes and 11 labelled estimates (4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32), superseding the old 25/10 breakdown.

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

Three worktree agents plus the orchestrator worked in parallel with exclusive ownership of shared files. This run required a shared v1/v2 foundation before either enhanced family's boot could be accepted; future estimates should budget the message and verb subsystems separately. No token or elapsed-time budget was supplied; exact usage is not available.

## Next run

Continue S4b for ManiacEnhanced and ZakEnhanced through the shared v2 verb-slot operations; classic input/cutscene differences can be reviewed independently. Integrate and validate each bounded endpoint before proceeding to S5, S6 per REQUIREMENTS §7.1, S7 and S9. Read [the dedicated handoff](../../NEXT_SESSION_PROMPT_MANIAC_ZAK.md). Preserve the other orchestrator's families and follow the master-merge/full-suite/fast-forward procedure above.
