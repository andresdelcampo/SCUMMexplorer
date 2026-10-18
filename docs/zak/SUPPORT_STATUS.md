# Zak McKracken (classic v1) support status

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
| S8 State | not started | Status – | – | requested in a run: no |
| S9 Editions | available-copy pass done; alternate editions blocked | unchanged | [S9 record](../../.verification/zakclassic-editions-20261018/README.md) | One English Classic copy; 58 room structures read, profile/text/font contracts and family conformance pass; no cross-edition claim |

Blocked on: the 11 currently estimated costumes (4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32) need reached actor/palette state or variable operands resolved. This supersedes the older ten-costume list; costumes 1 and 35 now have script evidence, while costume 13 is estimated. No comparison with a running ScummVM was performed.
Next: retain the S5/S6/S7 limits while progressing S9 editions; re-derive estimated costumes from reached actor state without promoting unresolved colors. S6's bounded native play route is verified; no full-game completion is claimed.
Open uncertainties: see the record (variable actor in Room 38; control flow inside a costume/palette span ignored).

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-17 | S5 Classic | Treat source previews as original-game comparison, or alter state to remove the Room 44 detector flag? | claim full acceptance / force a fill / retain limits | retain source state and visual acceptance limits; all 58 rooms open and three object-state tests pass, but native comparison is prohibited and no state fill is justified | complete a later authorized visual comparison; no workaround to remove |
| 2026-10-18 | S7 Classic | Which scenes prove the unnamed request? | invented catalog / source examples | use independently scanned Room 8 entry, bakery doorbell and shop Buy; retain five capped runs and estimated costume colours | remove family tests/record; no seed or catalog |
| 2026-10-18 | S9 Classic | No alternate family copy exists locally | infer from Enhanced / record missing coverage | Enhanced is separate; bounded available-copy contracts pass, S7 reused; alternate comparisons blocked, registry and costume estimates unchanged | add a real alternate fixture in a later authorized pass |
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

S4b baseline (historical): both editions run their own source-defined full opening and Script 108 post-dream action; no game variable, room or answer was forced. Random bedroom events and propagated effects remain labelled estimates. No unsupported opcode remains on the bounded acceptance routes. Shared slot dispatch and individual sound timelines are implemented; inventory-held code, active nested object-slot replacement continuation and Player_V2 priority/next-sound arbitration remain explicit limitations if reached. Sentence presentation belongs to S6. No native ScummVM was executed, and no registry cell was raised.

Enhanced S5 acceptance remains open: the requested run prohibits executing ScummVM, so the required ten-room original-game picture comparison was replaced by explicitly labelled source comparisons, not claimed as completed. Residual geometric gap flags are retained; no pixels or story state were filled. See the record for exact results. Shared corrections are version-level, not Zak room exceptions.

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-17 | S5 Enhanced | Treat source comparison as original-game visual acceptance? | claim completion / retain distinction | retain distinction, following R-2.4 and the run's prohibition on launching ScummVM; Objects remains Partial | complete the original-game comparison in a later authorized run |
| 2026-10-17 | S5 Enhanced | Force inactive stored images into flagged rectangles? | fill / preserve source state | preserve reached intrinsic/touch flags; the detector alone does not justify a state change | no state workaround was introduced |
| 2026-10-18 | S7 Enhanced | Which scenes prove the unnamed request? | invented catalog / examples from scan | choose Room 8 entry, bakery doorbell and shop Buy group from the edition's own source; preserve failed and capped runs, boot-unknown verb names and preview assumptions | remove family tests/record; no catalog or seed to unwind |
| 2026-10-18 | S9 Enhanced | No alternate family copy exists locally | infer from Classic / record missing coverage | Classic is separate; run bounded available-copy contracts, reuse S7, block alternate comparisons and leave registry unchanged | add a real alternate fixture in a later authorized pass |
