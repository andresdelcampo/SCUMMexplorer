# Maniac Mansion (classic v1) support status

Family: ManiacClassic | Fixture: D:/Juegos/Lucas/mmansion ega en (TestGames.Root) | Last updated: 2026-10-17 | Owner: Maniac/Zak orchestrator

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | Costumes: colours derived, cell unchanged; Scripts, Texts: done earlier | Costumes ✔ (raised 14 Oct on the user's decision), Scripts ✔, Texts ✔ | .verification/v1-costume-colours-20261014 | all 23 costumes derived from scripts + ScummVM tables; no run-time comparison with ScummVM; the user judged the source-derived colours sufficient and raised the cell |
| S3 Sound | done (earlier) | Sound ✔ | – | PCjr/Tandy not listened |
| S4b Session | done (bounded assumed-input route) | – | .verification/maniac-v1-adapter-20261017; .verification/ClassicBoot | Native input selects Syd/Bernard and Start; introduction reaches Room 44 with control, stable another 120 frames. Sound lifetime and seeded-route estimates remain explicit. |
| S5 Objects | source preview implemented; visual acceptance open | Objects ◐ | .verification/maniacclassic-objects-20261017 | All 52 rooms open through the reviewed party recipe; native object flags and live classic actors. Original-game picture comparison was prohibited; six native coin-slot detector findings remain recorded. |
| S6 Play | done (bounded native route) | Play/Objects Full | .verification/maniacclassic-play-20261017 | Genuine party choices, override skip, walked exit, composed sentence, mat/key pickup, rendered inventory and inventory selection; original-game picture/timing acceptance remains open. |
| S7 Cutscenes | not started | Cutscenes – | – | |
| S8 State | not started | Status – | – | requested in a run: no |
| S9 Editions | not started | – | – | |

Blocked on: S5 original-game picture comparison is unperformed because this run prohibits launching ScummVM. Source-level comparisons and real-window captures do not substitute for that evidence.
Next: retain the S5 original-game visual acceptance distinction; S7 cutscenes and S9 edition checks remain separate, unstarted items. The bounded native S6 route does not establish every party or a complete playthrough.
Open uncertainties: the skin/suit tables are ScummVM's data (`v1MMActorPalatte1/2`), not stored in the game files; lights-off colour 11 is not applied.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 2026-10-14 | S2 | Where does a v1 Maniac costume's colour come from? | (a) keep costume number = actor number as an assumption; (b) read the scripts for who wears which costume | (b): ScummVM `scumm.cpp` starts actor n with costume n and the scripts never give another costume (only costume 0 on a variable actor), so the old estimate is confirmed as the real rule; `ScummV1ActorColors` records it as `InitialState` | revert the commit; the renderer then needs the actor argument back |
| 2026-10-14 | S2 | Raise Costumes to ✔? | raise on source evidence alone / keep ◐ until compared with a running ScummVM | keep ◐ (the stage requires the side-by-side comparison; none could be run) | superseded: the user decided (14 Oct) that Maniac Mansion classic is complete on the source evidence, and the cell is ✔; revert the one registry line to undo |
| 2026-10-17 | S5 | How to leave the party menu for a first-visit preview? | assign story variables / send reviewed native inputs | Fingerprint the edition's menu, send the S4b Syd/Bernard/Start clicks and label the assumed choice; fail closed on an unreviewed menu | revert the `ManiacFirstVisit` recipe/provider and restore the plain-background fallback |
| 2026-10-17 | S5 | How to handle Room 18's six detector findings? | fill them / preserve source pixels and report the detector | Preserve: each rectangle is already 58/64 black pixels in the background and no stored fill is usable; source composition equals the background | keep the regression/evidence when changing the detector; never write story state to silence it |
| 2026-10-17 | S5 | Can source audit satisfy original-game visual acceptance? | claim completion / separate the implemented preview from missing evidence | Separate: source preview implemented, original-game picture acceptance open; Objects remains Partial and S6 is unstarted | add actual comparison evidence in an explicitly authorized run |
| 2026-10-17 | S6 | How to check a player choice in a game without Talk to? | invent a dialogue / use the native party menu | The user chooses Syd/Bernard/Start through real input; proposed R-7.21 preserves the game's interface. Sentence, exit, pickup, displayed inventory and override skip remain required | revert the S6 proposal and its dependent route claims together |
| 2026-10-17 | S6 | Classic and enhanced text fonts differ | reuse later external charsets / use the native built-in tables | Use ScummVM's 8-by-8 font data and the source-defined language substitutions; reviewed resource hashes select language, never folder names | revert the old-interface charset integration; keep the source evidence |

Facts about the game's scripts: [MANIAC_SCRIPT_MAP.md](MANIAC_SCRIPT_MAP.md).

## ManiacEnhanced v2 — S4b checkpoint, 17 October 2026

S4b is done for this enhanced fixture and explicitly selected route; no support cell changes. Genuine assumed party clicks select Syd and Bernard. Route45→0→0→33→49→0→44 completes the opening and Script164 installs the verb slots. Player control returns after 1490 post-constructor frames (6490 including the 5000-frame menu wait), stable for 120 additional frames. The exact endpoint is covered by `ScummManiacEnhancedSessionTests`. Native ego initialization is retained; seeded execution and sound timing retain their estimate labels. No ScummVM executable comparison was made.

Evidence and reproducible inputs: [.verification/maniac-session-20261017](../../.verification/maniac-session-20261017/README.md). Existing classic costume-colour acceptance is unchanged.

## ManiacEnhanced v2 — S5 checkpoint, 17 October 2026

**Source preview implemented; visual acceptance open.** All 53 rooms open, all 53 have no detector gaps, and no run failed. Fifty-one rooms settle; Rooms 30 and 45 reach the 2400-frame limit and remain explicitly labelled state previews. Eleven source samples include the ten heaviest entries and boot Room 44. The reviewed menu recipe sends genuine assumed Syd/Bernard/Start clicks without story-variable writes. Three complete actor bodies in Room 44 are verified in the headless scene and the real window.

At the S5 checkpoint Objects remained Partial: click responses were outside that stage and no original ScummVM picture comparison was performed. Rooms 5, 8, 9, 15, 22 and 23 have the source lights-off flag but remain shown lit, with the limitation in Advanced. Evidence: [.verification/maniacenhanced-objects-20261017](../../.verification/maniacenhanced-objects-20261017/README.md). The following S6 work preserves these visual limitations; S7 and S9 remain unstarted.

## ManiacClassic and ManiacEnhanced — S6 checkpoint, 17 October 2026

Both fixtures have continuous source-driven input routes from their native party screen: select Syd and Bernard, Start, skip the outdoor introduction at its actual override, walk through the starting exit, move the camera, compose the Pick up sentence, lift the mat, take the key and click its displayed inventory entry. No route assigns story variables, visits a catalog room or directly activates an object verb.

Classic English: 1993 post-constructor frames, fifteen queued clicks/keys plus Escape; `Pick up door mat` and then `Walk to key`, with key owner 3 and 63 painted inventory pixels. Eight final focused tests pass (two play, four retained S5 cases, two charset). Enhanced Spanish: 1554 frames, thirteen delivered inputs plus Escape; `Coger felpudo`, `Coger llave` and `Ir a llave`, key owner 3. Each edition supplies its own positions and text; the enhanced route uses two leftward walks where classic needs four.

Records: [classic S6](../../.verification/maniacclassic-play-20261017/README.md), [enhanced S6](../../.verification/maniacenhanced-play-20261017/README.md). The parent checkpoint records the real-window results and final registry levels. Original-game execution/comparison was prohibited; source execution, WPF evidence and perceived audiovisual fidelity remain distinct. Dark rooms/flashlight behavior and the earlier S5 image limitations remain explicit. S7, S8 and S9 were not performed by this S6 item.
