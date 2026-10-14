# Zak McKracken (classic v1) support status

Family: ZakClassic | Fixture: Games/zak ega en | Last updated: 2026-10-14 | Owner: S2 costume-colours agent

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | Costumes: 25 of 35 derived, 10 labelled estimates, cell unchanged; Scripts, Texts: done earlier (polish: Script 39) | Costumes ◐, Scripts ✔, Texts ✔ | .verification/v1-costume-colours-20261014 | see the record for the list |
| S3 Sound | done (earlier) | Sound ✔ | – | PCjr/Tandy not listened |
| S4b Session | not started | – | – | |
| S5 Objects | not started | Objects ◐ | – | |
| S6 Play | not started | Play – | – | |
| S7 Cutscenes | not started | Cutscenes – | – | |
| S8 State | not started | Status – | – | requested in a run: no |
| S9 Editions | not started | – | – | |

Blocked on: the 10 estimated costumes (1, 4, 5, 8, 9, 16, 17, 18, 32, 35) need a running script session (S4/S6) to know the palette in effect, or the variable-operand instructions resolved; the comparison with a running ScummVM is also missing.
Next: S4b (needs S4a); then re-derive the estimated costumes from the session's real actor state.
Open uncertainties: see the record (variable actor in Room 38; control flow inside a costume/palette span ignored).

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-14 | S2 | Which palette belongs to a costume the scripts hand to an actor? | nearest instruction / last in the span / run the script | last palette instruction of the same actor operand after the costume instruction, before the actor's next costume instruction (the palette persists, ScummVM `setActorCostume` leaves it alone), else the closest one before; no run needed | revert the commit |
| 2026-10-14 | S2 | Costumes that depend on what ran before | guess one value / leave them out / label | label as `Estimate`, using what the boot script leaves in the actor's palette (R-1.11) | same |
| 2026-10-14 | S2 | Raise Costumes to ✔? | yes / no | no: 10 costumes are estimates and no comparison with a running ScummVM could be made | one-line commit later |

Facts about the game's scripts: [ZAK_SCRIPT_MAP.md](ZAK_SCRIPT_MAP.md).
