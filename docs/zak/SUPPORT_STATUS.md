# Zak McKracken (classic v1) support status

Family: ZakClassic | Fixture: D:/Juegos/Lucas/zak ega en (TestGames.Root) | Last updated: 2026-10-17 | Owner: Maniac/Zak orchestrator

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | Costumes: 24 of 35 derived, 11 labelled estimates, cell unchanged; Scripts, Texts: done earlier (polish: Script 39) | Costumes ◐, Scripts ✔, Texts ✔ | .verification/v1-costume-colours-20261014; .verification/ClassicBoot | refreshed after corrected v1 instruction decoding and object resource bounds; current estimates: 4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32; no run-time comparison |
| S3 Sound | done (earlier) | Sound ✔ | – | PCjr/Tandy not listened |
| S4b Session | done | unchanged | [.verification/zak-session-20261017](../../.verification/zak-session-20261017/README.md) | Own v1 opening reaches Room 1 in 1804 frames, player control on, no cutscene; stable for 120 more frames. Supersedes the earlier ClassicBoot blocker |
| S5 Objects | not started | Objects ◐ | – | |
| S6 Play | not started | Play – | – | |
| S7 Cutscenes | not started | Cutscenes – | – | |
| S8 State | not started | Status – | – | requested in a run: no |
| S9 Editions | not started | – | – | |

Blocked on: the 11 currently estimated costumes (4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32) need reached actor/palette state or variable operands resolved. This supersedes the older ten-costume list; costumes 1 and 35 now have script evidence, while costume 13 is estimated. No comparison with a running ScummVM was performed.
Next: S5 objects, S6 verb-based play, S7 cutscenes, S9 editions; re-derive estimated costumes from reached actor state without promoting unresolved colors. No dependent stage is complete.
Open uncertainties: see the record (variable actor in Room 38; control flow inside a costume/palette span ignored).

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-14 | S2 | Which palette belongs to a costume the scripts hand to an actor? | nearest instruction / last in the span / run the script | last palette instruction of the same actor operand after the costume instruction, before the actor's next costume instruction (the palette persists, ScummVM `setActorCostume` leaves it alone), else the closest one before; no run needed | revert the commit |
| 2026-10-14 | S2 | Costumes that depend on what ran before | guess one value / leave them out / label | label as `Estimate`, using what the boot script leaves in the actor's palette (R-1.11) | same |
| 2026-10-14 | S2 | Raise Costumes to ✔? | yes / no | no: 10 costumes are estimates and no comparison with a running ScummVM could be made | one-line commit later |

Facts about the game's scripts: [ZAK_SCRIPT_MAP.md](ZAK_SCRIPT_MAP.md).

## Enhanced v2 — 17 October 2026

Family: ZakEnhanced | Fixture: `D:/Juegos/Lucas/zakE ega en` | Owner: Zak S4b worktree agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S4b Session | done | unchanged | [.verification/zak-session-20261017](../../.verification/zak-session-20261017/README.md) | Adapter reads all 58 rooms / 148 present scripts; own opening reaches Room 1 in 1828 frames, control on, no cutscene; stable for 120 more frames |
| S5 Objects / S6 verb-based Play / S7 Cutscenes / S9 Editions | not started | unchanged | same | Next after bounded S4b acceptance |

Both editions run their own source-defined full opening and Script 108 post-dream action; no game variable, room or answer was forced. Random bedroom events and propagated effects remain labelled estimates. No unsupported opcode remains on the bounded acceptance routes. Shared slot dispatch and individual sound timelines are implemented; inventory-held code, active nested object-slot replacement continuation and Player_V2 priority/next-sound arbitration remain explicit limitations if reached. Sentence presentation belongs to S6. No native ScummVM was executed, and no registry cell was raised.
