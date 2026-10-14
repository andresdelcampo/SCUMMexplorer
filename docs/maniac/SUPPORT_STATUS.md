# Maniac Mansion (classic v1) support status

Family: ManiacClassic | Fixture: Games/mmansion ega en | Last updated: 2026-10-14 | Owner: S2 costume-colours agent

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | Costumes: colours derived, cell unchanged; Scripts, Texts: done earlier | Costumes ✔ (raised 14 Oct on the user's decision), Scripts ✔, Texts ✔ | .verification/v1-costume-colours-20261014 | all 23 costumes derived from scripts + ScummVM tables; no run-time comparison with ScummVM; the user judged the source-derived colours sufficient and raised the cell |
| S3 Sound | done (earlier) | Sound ✔ | – | PCjr/Tandy not listened |
| S4b Session | not started | – | – | |
| S5 Objects | not started | Objects ◐ | – | |
| S6 Play | not started | Play – | – | |
| S7 Cutscenes | not started | Cutscenes – | – | |
| S8 State | not started | Status – | – | requested in a run: no |
| S9 Editions | not started | – | – | |

Blocked on: nothing for S2.
Next: S4b (needs S4a).
Open uncertainties: the skin/suit tables are ScummVM's data (`v1MMActorPalatte1/2`), not stored in the game files; lights-off colour 11 is not applied.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-14 | S2 | Where does a v1 Maniac costume's colour come from? | (a) keep costume number = actor number as an assumption; (b) read the scripts for who wears which costume | (b): ScummVM `scumm.cpp` starts actor n with costume n and the scripts never give another costume (only costume 0 on a variable actor), so the old estimate is confirmed as the real rule; `ScummV1ActorColors` records it as `InitialState` | revert the commit; the renderer then needs the actor argument back |
| 2026-10-14 | S2 | Raise Costumes to ✔? | raise on source evidence alone / keep ◐ until compared with a running ScummVM | keep ◐ (the stage requires the side-by-side comparison; none could be run) | superseded: the user decided (14 Oct) that Maniac Mansion classic is complete on the source evidence, and the cell is ✔; revert the one registry line to undo |

Facts about the game's scripts: [MANIAC_SCRIPT_MAP.md](MANIAC_SCRIPT_MAP.md).
