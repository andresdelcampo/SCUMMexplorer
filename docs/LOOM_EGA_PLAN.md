# LOOM exploration plan

Reconciled 8 October 2026. Current priorities are below. Earlier implementation steps, M5 specifications and completed VGA tasks are preserved in [the historical plan](archive/LOOM_EGA_PLAN_BEFORE_RECONCILIATION_2026-10-08.md) and [changelog](CHANGELOG.md); they are not current instructions.

## Current capability

Explore DOS LOOM EGA and CD/VGA through original rooms, objects, actors, scripts, drafts, text and audio. English Roland EGA (`Games/loom ega mt32 en`) remains the fingerprinted replay reference. Other EGA editions are comparisons; demos use their own source. VGA disc reading, live interaction, v4 walking, scene starts, staff/Game Status, Readable scripts and CD audio are implemented.

Normal mode leads with artwork/game behavior; source evidence remains in Advanced. Follow [CURRENT_WORK](CURRENT_WORK.md) for earliest-state, running-session, evidence, preservation and workflow rules.

## What remains active

| Priority | Work | Result / closure boundary |
| --- | --- | --- |
| 1 | Original-game actor/staff comparison | Room 43 staff scenes, El Muerto/Room 17 sizes and actor 4 placement need matching native state/visible bounds. Existing VGA alignment is source-confirmed; no speculative layout patch remains. [Staff evidence](../.verification/staff-layout-20261008/README.md). |
| 2 | Source-earned casting routes and physical input | Six prepared Room 04 note-input casts pass across three editions, plus two prepared VGA sky casts after the source opening. Original-game earned route/animation/physical input remains; the failed sky catalog-arrival packets are preserved. [Draft evidence](../.verification/draft-input-20261008/README.md), [sky evidence](../.verification/sky-input-20261008/README.md). |
| 3 | Listening/timing | Compare representative CD picture/voice/music and any specific Roland discrepancy using matching edition, state and device. Playback and overture are implemented and user-confirmed; broad audiovisual fidelity is unproven. |
| 4 | Evidence-bound naming/engine work | Silence's own-source chain is established; Shaping's canonical label, exact reverse names and unidentified roles remain uncertain. The 11/11/8 dynamic call sites are classified, with input-dependent/incomplete-data limits. Extend names/opcodes only when own-source evidence and a route justify a visible correction. [Source checkpoint](../.verification/source-gaps-20261008/README.md). |

[The dossier](loom/FINAL_GAPS_HANDOFF.md) records exact comparison phases, triggers, measurements and limits. Available source checks now establish Silence's chain, classify nonliteral calls and qualify a prepared VGA sky arrival. Next useful comparisons are a matched Room 43 actor/staff picture and the VGA sky physical casting route, when the user chooses to resume native work. A passing cutscene end alone does not establish correct story state or actors.

## Parked

- **Room 34 local-200 +0035 standalone fortress reveal:** unresolved and parked by the user. Save/console/close assists fulfilled; local-206's ghost pan is separate. No native route pending in current scope. Physical Escape stopped Computer Use; direct human authorization is needed to resume. [Native history](../.verification/room34-state-20261008/NATIVE_SESSION.md).
- **M5/secrets:** shelved 5 October; no guide built. Old specification/spoiler decisions remain in the historical plan.
- **Indy expansion/later games:** deferred. Indy EGA has backgrounds, texts and a standalone conversation exporter, not interactive gameplay.
- **Cleanup deletion/worktrees, demo carrier rooms and broad performance:** [review](CLEANUP_REVIEW.md) is proposals, not deletion authorization.
- Optional General MIDI fallback/card-specific archaeology is outside active scope.

## Completed evidence

- [Fresh English EGA catalog return](../.verification/catalog-return-20261008/README.md), `9de8193`: the user approved source prerequisites on 8 October. The Room 46 recipe now runs missing real scenes, keeps existing ghost/restored state and cancels on failure. The exact fresh 46 → 34 conversation shows ghost costume 120 in the real WPF window; early closed portals remain closed. 32 focused Core checks, 18 profile-isolated WPF assertions and clean build. Recipe operands are bound to the English source fingerprint; other editions are not guessed.

- [Correctness audit](../.verification/correctness-20261008/README.md): 328 English EGA recipes and 222/223 VGA groups normal/Escape; historical 833-test/five-skip baseline, 201 WPF assertions.
- [Final gaps](../.verification/final-gaps-20261008/README.md): Room 70 operands corrected, interior entries excluded from reviewed titles, residual raw callbacks classified.
- [Native walking](../.verification/native-walk-20261008/README.md): VGA v4 corrected/EGA v3 retained; eleven native C++ comparisons, focused checks and warranted VGA rerun.
- [Room 34 state](../.verification/room34-state-20261008/README.md): three-edition guards/writers/prepared transitions; 24 diagnostics. No proof the parked crossing is reachable or dead.
- [Closed/conditional portals](../.verification/portal-state-20261008/README.md), `ebdf3b2`: 41 source/WPF plus 30 navigation assertions; prepared ghost/restoration verified. Fresh catalog history is now corrected by `9de8193` above.
- [Staff/source comparison](../.verification/staff-layout-20261008/README.md): existing alignment confirmed; experimental edit withdrawn; 18 assertions and restored buffer hashes.
- [Draft/source checkpoint](../.verification/draft-input-20261008/README.md), `0243a5a`: six real-note casts, Blunting direction, three profile-free renders, clean build.

Historical percentages describe their dated tests. They do not establish complete original-game fidelity or full Spanish/other-edition replay parity.
