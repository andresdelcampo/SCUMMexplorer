# Zak McKracken (classic v1) support status

## Admin Game Status clarification — 18 October 2026

The user's explicit clarification supersedes S8's held-only edit scope: Game Status is an admin editor and must offer the game's inventory items without requiring earned pickups, with source-established recipients and native capacity. Admin choices remain separate from earned gameplay and unrelated ticket/puzzle flags. The old S8 records below preserve their historical scope; they do not establish this expanded acceptance. The explicit catalog/grant/capacity/Undo editor is implemented. Status is rebaselined to **Partial** under the broader all-items requirement while indirect pickup-target coverage remains unresolved. Implementation review and final validation are recorded at the [admin checkpoint](../checkpoints/20261018-Admin-ManiacZak.md).

## Flight preparation and ticket state — 18 October 2026 (current, both editions)

Both supplied editions recover the CashCard through native knife/card input, retain the logical airport number across the alien scene, and use the reservations menu's own destination/Buy/Cancel choices. The demonstrated engine gaps were the v1/v2 object-header class mask and actor logical-room retention, including camera follows between aliases of the same room resource. Source rules are version-specific; no game/room/offset exception or story-state assignment.

Native Buy issues the ticket, the gate consumes it and clears departure, and Script 116 serves peanuts 51, releasing Script 104's wait and returning cabin control within the 2500-frame observation. Object 51 is **peanuts**, not a ticket; actual tickets are 82/83. Cancel refunds the old ticket and leaves no valid replacement; the gate refuses boarding. Wrong-tool and wrong-airport guards remain effective. [Classic record](../../.verification/zakclassic-flight-20261018/README.md), [enhanced record](../../.verification/zakenhanced-flight-20261018/README.md), [independent review](../../.verification/zak-flight-review-20261018/README.md).

Preparation uses disclosed room previews and direct native knife pickup, followed by actual interface input; it does not establish an earned bus/airport journey. Two failed bus-input approaches remain parked. Boarding/safety/meal completion is not destination arrival: the native 28800-jiffy flight timer remains. No production completion cap is raised and no ticket, currency, owner, actor position or automatic choice is supplied. Original-game picture/timing/listening, other destinations/puzzles and alternate releases remain open.

Registry levels are unchanged: Cutscenes Partial, Classic Costumes Partial with 24 derived/11 estimates. Final window/build/matrix/full-suite and integration results are in the [flight checkpoint](../checkpoints/20261018-Flight-ManiacZak.md). The older records retain their dated failures; this section supersedes their ticket-object interpretation.

## Engine fidelity — preceding 18 October 2026 pass, both editions

Native actor-approach distances use v1/v2 units. Two of the five formerly capped starts finish in each edition (646 and 907 frames). The Room 11 fresh previews were unprepared meal-service waits on peanuts 51, misidentified as a ticket in the historical record; the new preparation above releases that wait through native boarding/service. Room 23 deliberately waits for Escape/restart. No prerequisite owner/flag or automatic Escape is supplied. [Classic scenes](../../.verification/zakclassic-fidelity-scenes-20261018/README.md), [Enhanced scenes](../../.verification/zakenhanced-fidelity-20261018/README.md), [retained before/after results](../../.verification/zak-fidelity-20261018/README.md).

Both editions render native darkness and moving flashlight beams. Their own pickup/Switch on code selects the reached 9x7 beam; actual-window bitmap/Play checks pass. The visited dark Room 36 is an ending backdrop: these are disclosed exploration rendering checks, not an earned ending. [Lighting record](../../.verification/maniacenhanced-fidelity-20261018/README.md).

Classic costume changes now preserve the reached dynamic palette, correcting three actual room-context mismatches. Card evidence retains variable palette operands/writers and candidate actors. The card table stays **24 derived / 11 labelled estimates**; the early visible boss context is palette 0 before the later 7, and contextual observations are not promoted to a universal card colour. [Palette record](../../.verification/zakclassic-fidelity-20261018/README.md).

Registry levels remain unchanged: Cutscenes Partial without a hand-validated catalog, Classic Costumes Partial while the estimates remain. Decision: correct version rules and expose provenance, preserving real waits and alternate palettes; no state shortcuts or sampled palette promotion. Revert linked fidelity commits to undo. Original-game picture/timing/listening, walking metric, broader puzzle routes, first-visit image/camera findings and missing alternate releases remain. [Integration checkpoint](../checkpoints/20261018-Fidelity-ManiacZak.md) records final validation; dated sections below are historical.

Family: ZakClassic | Fixture: D:/Juegos/Lucas/zak ega en (TestGames.Root) | Last updated: 2026-10-18 | Owner: Maniac/Zak orchestrator

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | Costumes: 24 of 35 derived, 11 labelled estimates, cell unchanged; Scripts, Texts: done earlier (polish: Script 39) | Costumes ◐, Scripts ✔, Texts ✔ | .verification/v1-costume-colours-20261014; .verification/ClassicBoot | refreshed after corrected v1 instruction decoding and object resource bounds; current estimates: 4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32; no run-time comparison |
| S3 Sound | done (earlier) | Sound ✔ | – | PCjr/Tandy not listened |
| S4b Session | done | unchanged | [.verification/zak-session-20261017](../../.verification/zak-session-20261017/README.md) | Own v1 opening reaches Room 1 in 1804 frames, player control on, no cutscene; stable for 120 more frames. Supersedes the earlier ClassicBoot blocker |
| S5 Objects | source preview implemented; visual acceptance open | Objects ◐ unchanged | [.verification/zakclassic-objects-20261017](../../.verification/zakclassic-objects-20261017/README.md) | 58 rooms open; Room 44 gap flag retained; original-game comparison not performed |
| S6 Play | done (bounded native route) | Objects/Play Full | [.verification/zakclassic-play-20261017](../../.verification/zakclassic-play-20261017/README.md) | Three genuine-input tests and real-window route pass; native Buy choice, reached cutscene skip and returned control; S5 visual limits retained |
| S7 Cutscenes | done (bounded source-derived list) | Cutscenes Partial | [.verification/zakclassic-cutscenes-20261018](../../.verification/zakclassic-cutscenes-20261018/README.md) | 130 triggers, 54 groups, 37 scenes; 13 begin, five hit frame cap; three bounded fresh-start proofs, four tests pass |
| S9 Editions | available-copy pass done; alternate editions blocked | unchanged | [S9 record](../../.verification/zakclassic-editions-20261018/README.md) | One English Classic copy; 58 room structures read, profile/text/font contracts and family conformance pass; no cross-edition claim |
| S8 State | done (bounded inventory editor) | Status Full | [S8 record](../../.verification/zakclassic-state-20261018/README.md) | Own native bill pickup; three tests and actual-window Apply/Undo/Cancel pass. Ordinary player actions block edits; resident special handlers do not. |

Blocked on: the 11 estimated costumes (4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32) retain context-dependent actors, earlier writers and branch/call-order uncertainty. Native palette preservation and representative reached contexts are verified; they do not establish one universal card palette. No original-game comparison was performed.
Next: retain the clean flight-preparation checkpoint. Full timed travel/arrival, the parked earned bus approach and broader story routes remain separate; alternate-release S9 checks need another same-family copy. Preserve card estimates and version-specific palette evidence. No full-game completion is claimed.
Open uncertainties: Room 38 variable actor, control flow/call order, native visual acceptance, and source story prerequisites beyond the bounded routes.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-18 | Admin | Is the held-only editor sufficient, and may special-handler IDs become catalog items? | Keep held-only scope / infer every handler ID / source-resolved grants | Follow the user's admin clarification with 82 named candidates, native capacity and complete Undo; exclude inferred self-targets, retain letter139 classification and Status Partial pending indirect coverage | Undo within the paused window; revert the bounded admin implementation and support rebaseline together to roll back the feature |
| 2026-10-18 | Flight | How to release the recorded owner51 wait and preserve ticket context? | Supply a ticket/owner/room flag / trace actual identity and native preparation | Correct the peanuts/ticket distinction, native header masks and actor logical-room retention; verify Buy/Cancel/boarding/service through source input after disclosed preparation, retaining failed bus attempts and pending flight timer | Revert bounded flight implementation/tests/notes together, preserving historical evidence and unrelated work |
| 2026-10-17 | S5 Classic | Treat source previews as original-game comparison, or alter state to remove the Room 44 detector flag? | claim full acceptance / force a fill / retain limits | retain source state and visual acceptance limits; all 58 rooms open and three object-state tests pass, but native comparison is prohibited and no state fill is justified | complete a later authorized visual comparison; no workaround to remove |
| 2026-10-18 | S7 Classic | Which scenes prove the unnamed request? | invented catalog / source examples | use independently scanned Room 8 entry, bakery doorbell and shop Buy; retain five capped runs and estimated costume colours | remove family tests/record; no seed or catalog |
| 2026-10-18 | S9 Classic | No alternate family copy exists locally | infer from Enhanced / record missing coverage | Enhanced is separate; bounded available-copy contracts pass, S7 reused; alternate comparisons blocked, registry and costume estimates unchanged | add a real alternate fixture in a later authorized pass |
| 2026-10-18 | S8 Classic | Which edits add value without fabricated story state? | arbitrary flags/new items / held inventory across source players | Use source-established player range/names and owner-only held-item choices with guarded Undo; native special handlers remain ambient, ordinary actions block edits | revert the S8 proposals and dependent inventory editor/claim |
| 2026-10-14 | S2 | Which palette belongs to a costume the scripts hand to an actor? | nearest instruction / last in the span / run the script | last palette instruction of the same actor operand after the costume instruction, before the actor's next costume instruction (the palette persists, ScummVM `setActorCostume` leaves it alone), else the closest one before; no run needed | revert the commit |
| 2026-10-14 | S2 | Costumes that depend on what ran before | guess one value / leave them out / label | label as `Estimate`, using what the boot script leaves in the actor's palette (R-1.11) | same |
| 2026-10-14 | S2 | Raise Costumes to ✔? | yes / no | no: 10 costumes are estimates and no comparison with a running ScummVM could be made | one-line commit later |

Facts about the game's scripts: [ZAK_SCRIPT_MAP.md](ZAK_SCRIPT_MAP.md).

## Enhanced v2 — 17 October 2026

Family: ZakEnhanced | Fixture: `D:/Juegos/Lucas/zakE ega en` | Owner: Zak S4b worktree agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S4b Session | done | unchanged | [.verification/zak-session-20261017](../../.verification/zak-session-20261017/README.md) | Adapter reads all 58 rooms / 148 present scripts; own opening reaches Room 1 in 1828 frames, control on, no cutscene; stable for 120 more frames |
| S5 Objects | source preview implemented; visual acceptance open | Objects ◐ unchanged | [.verification/zakenhanced-objects-20261017](../../.verification/zakenhanced-objects-20261017/README.md) | All-room source audit and three settled-state regressions; see record for retained gap flags and comparison limits |
| S6 verb-based Play | done (bounded native route) | Objects/Play Full | [.verification/zakenhanced-play-20261017](../../.verification/zakenhanced-play-20261017/README.md) | Real sentence composition/execution, phone-bill pickup/inventory, room exits, reached alien-scene skip, native Buy choice and shopkeeper response; no forced story state |
| S7 Cutscenes | done (bounded source-derived list) | Cutscenes Partial | [.verification/zakenhanced-cutscenes-20261018](../../.verification/zakenhanced-cutscenes-20261018/README.md) | 130 triggers, 54 groups, 37 scenes; 13 begin, five hit frame cap; three bounded fresh-start proofs, four tests pass; S5/S6 limits retained |
| S9 Editions | available-copy pass done; alternate editions blocked | unchanged | [S9 record](../../.verification/zakenhanced-editions-20261018/README.md) | One English Enhanced copy; 58 room structures read, profile/text/font contracts and family conformance pass; no cross-edition claim |
| S8 State | done (bounded inventory editor) | Status Full | [S8 record](../../.verification/zakenhanced-state-20261018/README.md) | Own native bill pickup; three tests and actual-window Apply/Undo/Cancel pass. Modal pause/restore and source character names verified. |

S4b baseline (historical): both editions run their own source-defined full opening and Script 108 post-dream action; no game variable, room or answer was forced. Random bedroom events and propagated effects remain labelled estimates. No unsupported opcode remains on the bounded acceptance routes. Shared slot dispatch and individual sound timelines are implemented; inventory-held code, active nested object-slot replacement continuation and Player_V2 priority/next-sound arbitration remain explicit limitations if reached. Sentence presentation belongs to S6. No native ScummVM was executed, and no registry cell was raised.

Enhanced S5 acceptance remains open: the requested run prohibits executing ScummVM, so the required ten-room original-game picture comparison was replaced by explicitly labelled source comparisons, not claimed as completed. Residual geometric gap flags are retained; no pixels or story state were filled. See the record for exact results. Shared corrections are version-level, not Zak room exceptions.

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-17 | S5 Enhanced | Treat source comparison as original-game visual acceptance? | claim completion / retain distinction | retain distinction, following R-2.4 and the run's prohibition on launching ScummVM; Objects remains Partial | complete the original-game comparison in a later authorized run |
| 2026-10-17 | S5 Enhanced | Force inactive stored images into flagged rectangles? | fill / preserve source state | preserve reached intrinsic/touch flags; the detector alone does not justify a state change | no state workaround was introduced |
| 2026-10-18 | S7 Enhanced | Which scenes prove the unnamed request? | invented catalog / examples from scan | choose Room 8 entry, bakery doorbell and shop Buy group from the edition's own source; preserve failed and capped runs, boot-unknown verb names and preview assumptions | remove family tests/record; no catalog or seed to unwind |
| 2026-10-18 | S9 Enhanced | No alternate family copy exists locally | infer from Classic / record missing coverage | Classic is separate; run bounded available-copy contracts, reuse S7, block alternate comparisons and leave registry unchanged | add a real alternate fixture in a later authorized pass |
| 2026-10-18 | S8 Enhanced | Which edits add value without fabricated story state? | arbitrary flags/new items / held inventory across source players | Use own-source names/range, explicit held-item ownership, atomic Apply/Cancel/Undo and modal pause; no inferred Give or pickup | revert the S8 proposals and dependent inventory editor/claim |
