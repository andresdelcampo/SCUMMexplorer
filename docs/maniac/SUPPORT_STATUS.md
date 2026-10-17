# Maniac Mansion (classic v1) support status

Family: ManiacClassic | Fixture: D:/Juegos/Lucas/mmansion ega en (TestGames.Root) | Last updated: 2026-10-17 | Owner: Maniac/Zak orchestrator

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | Costumes: colours derived, cell unchanged; Scripts, Texts: done earlier | Costumes ✔ (raised 14 Oct on the user's decision), Scripts ✔, Texts ✔ | .verification/v1-costume-colours-20261014 | all 23 costumes derived from scripts + ScummVM tables; no run-time comparison with ScummVM; the user judged the source-derived colours sufficient and raised the cell |
| S3 Sound | done (earlier) | Sound ✔ | – | PCjr/Tandy not listened |
| S4b Session | in progress | – | .verification/maniac-v1-adapter-20261017; .verification/ClassicBoot | V1 data source and factory dispatch implemented; bounded boot reaches Room 45 party selection, not a completed boot |
| S5 Objects | not started | Objects ◐ | – | |
| S6 Play | not started | Play – | – | |
| S7 Cutscenes | not started | Cutscenes – | – | |
| S8 State | not started | Status – | – | requested in a run: no |
| S9 Editions | not started | – | – | |

Blocked on: nothing for S2.
Next: complete S4b: select the party through assumed input, resolve reached classic engine semantics, and validate the resulting boot endpoint before S5–S7.
Open uncertainties: the skin/suit tables are ScummVM's data (`v1MMActorPalatte1/2`), not stored in the game files; lights-off colour 11 is not applied.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-14 | S2 | Where does a v1 Maniac costume's colour come from? | (a) keep costume number = actor number as an assumption; (b) read the scripts for who wears which costume | (b): ScummVM `scumm.cpp` starts actor n with costume n and the scripts never give another costume (only costume 0 on a variable actor), so the old estimate is confirmed as the real rule; `ScummV1ActorColors` records it as `InitialState` | revert the commit; the renderer then needs the actor argument back |
| 2026-10-14 | S2 | Raise Costumes to ✔? | raise on source evidence alone / keep ◐ until compared with a running ScummVM | keep ◐ (the stage requires the side-by-side comparison; none could be run) | superseded: the user decided (14 Oct) that Maniac Mansion classic is complete on the source evidence, and the cell is ✔; revert the one registry line to undo |

Facts about the game's scripts: [MANIAC_SCRIPT_MAP.md](MANIAC_SCRIPT_MAP.md).

## ManiacEnhanced v2 — S4b checkpoint, 17 October 2026

S4b is done for this enhanced fixture and explicitly selected route; no support cell changes. Genuine assumed party clicks select Syd and Bernard. Route45→0→33→49→44 completes the opening and Script164 installs the verb slots. Player control returns after1490 post-constructor frames (6490 including the5000-frame menu wait), stable for120 additional frames. The exact endpoint is covered by `ScummManiacEnhancedSessionTests`. Initial menu endCutscene lacks established ego room/position and is reported explicitly; seeded execution and sound timing retain their estimate labels. No ScummVM executable comparison was made.

Evidence and reproducible inputs: [.verification/maniac-session-20261017](../../.verification/maniac-session-20261017/README.md). Next: S5 first-visit object acceptance, then S6 verb play and S7 cutscenes. S5–S7/S9 remain unattempted. Existing classic costume-colour acceptance is unchanged.
