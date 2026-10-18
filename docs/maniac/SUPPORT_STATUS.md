# Maniac Mansion (classic v1) support status

Family: ManiacClassic | Fixture: D:/Juegos/Lucas/mmansion ega en (TestGames.Root) | Last updated: 2026-10-18 | Owner: Maniac/Zak orchestrator

| Stage | State (not started / in progress / done / blocked) | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (earlier; no tracker then) | Rooms | – | |
| S2 Reading | Costumes: colours derived, cell unchanged; Scripts, Texts: done earlier | Costumes ✔ (raised 14 Oct on the user's decision), Scripts ✔, Texts ✔ | .verification/v1-costume-colours-20261014 | all 23 costumes derived from scripts + ScummVM tables; no run-time comparison with ScummVM; the user judged the source-derived colours sufficient and raised the cell |
| S3 Sound | done (earlier) | Sound ✔ | – | PCjr/Tandy not listened |
| S4b Session | done (bounded assumed-input route) | – | .verification/maniac-v1-adapter-20261017; .verification/ClassicBoot | Native input selects Syd/Bernard and Start; introduction reaches Room 44 with control, stable another 120 frames. Sound lifetime and seeded-route estimates remain explicit. |
| S5 Objects | source preview implemented; visual acceptance open | Objects ◐ | .verification/maniacclassic-objects-20261017 | All 52 rooms open through the reviewed party recipe; native object flags and live classic actors. Original-game picture comparison was prohibited; six native coin-slot detector findings remain recorded. |
| S6 Play | done (bounded native route) | Play/Objects Full | .verification/maniacclassic-play-20261017 | Genuine party choices, override skip, walked exit, composed sentence, mat/key pickup, rendered inventory and inventory selection; original-game picture/timing acceptance remains open. |
| S7 Cutscenes | done (bounded source-derived list) | Cutscenes Partial; parent owns matrix | .verification/maniacclassic-cutscenes-20261018 | 59 triggers/27 groups; six fresh starts begin; three source-attributed proofs and real-window actions. No complete-story or original-game fidelity claim. |
| S8 State | headless verified; final window acceptance with parent | Status: parent owns final cell | .verification/maniacclassic-state-20261018 | Explicitly requested: selected-party held inventory, guarded owner reassignment, Apply/Cancel/Undo. No item creation or story-state editor. |
| S9 Editions | available-copy checks done; alternate releases blocked | unchanged | .verification/maniacclassic-editions-20261018 | One English classic copy; identity, native startup/play/scene and edition-boundary regressions pass. Enhanced is a separate family. |

Blocked on: S5 original-game picture comparison is unperformed because this run prohibits launching ScummVM. Source-level comparisons and real-window captures do not substitute for that evidence.
Next: parent completes the S8 real-window and integration checkpoint; alternate-release S9 comparison requires another copy of the same family. Retain original-game visual limits and the bounded S7 distinction, including the doorbell continuation query. The selected party, inventory choices and direct scene starts do not establish a complete playthrough.
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
| 2026-10-18 | S7 | No scenes named by the user | invent catalog entries / choose from the source scan | Three distinct source-scanned responses with observable lines and bounded returns; disclose the fingerprinted startup choices and retain the post-doorbell query gap | revert family S7 enablement and dependent preparation, preserving evidence |
| 2026-10-18 | S9 | Only one release per Maniac family is local | Treat classic/enhanced as alternates / record missing data | Keep the families separate; check reference identity and compatibility boundaries, reuse bounded S6/S7 evidence and block unavailable alternate-release comparisons; cells unchanged | Add another same-family fixture and run only its plausible differences |
| 2026-10-18 | S8 | Which characters and state edits add useful scope? | Every named actor and arbitrary flags / current-party held inventory | Derive party bindings from the edition's native ego-switch script; use executed names; permit only explicit ownership reassignment of already held items, preserving slot/code/state and supporting guarded Undo. This adds a cross-character view without inventing story progress | Revert proposed R-12.7–R-12.10 and the dependent dialog/API claims together |

Facts about the game's scripts: [MANIAC_SCRIPT_MAP.md](MANIAC_SCRIPT_MAP.md).

## ManiacClassic and ManiacEnhanced — S8 checkpoint, 18 October 2026

The explicit S8 request adds a held-inventory view across the currently selected party and guarded owner reassignment. Each edition supplies its own actor names and native party-variable bindings; named NPCs and unselected kids are not eligible recipients. Missing names fail closed. Apply changes only ownership; Cancel discards the pending draft, and Undo restores the last batch while its session and state remain current. The window pauses playback; Undo is deliberately refused after any intervening frame. These are explicit exploration choices, not played scenes or a native Give action.

Classic: three new state tests plus one family conformance row pass. The genuine S6 mat/key route reaches key 55 owned by Dave; the explicit edit moves it to Syd, and Undo restores Dave, retaining slot 0 and Room 1 source code. Guards reject room objects, named NPCs, unselected kids, mixed invalid/duplicate batches, stale views, reused undo, startup and queued input. Variables, object state/classes, other owners, actor state, room and scene count remain unchanged. The three tests also pass with the final shared attribution and running-action guard.

Enhanced: three state tests pass. Its own native route reaches the held `llave` after 1553 frames and twelve delivered inputs; the same owner round trip 3→1→3 preserves its slot and Room 1 code. The shared API records explicit edit attribution without pretending a scene ran. Parent owns the actual-window checks, final support cell and full integration suite. Headless checks alone do not establish that final window acceptance.

Records: [classic S8](../../.verification/maniacclassic-state-20261018/README.md), [enhanced S8](../../.verification/maniacenhanced-state-20261018/README.md). Proposed R-12.7–R-12.10 define this bounded inventory feature; no arbitrary puzzle flags, new items, actor placements, party changes, seed or hand-authored catalog are included. Earlier original-game visual/timing/listening and unavailable-edition limitations remain unchanged.

## ManiacClassic and ManiacEnhanced — S9 checkpoint, 18 October 2026

The local inventory contains only English classic and Spanish enhanced Maniac. These are separate families: neither validates another release of the other. Available reference-copy checks are recorded; additional same-family languages, demos and patched copies remain blocked by missing data. Registry levels are unchanged, and the complete-story, original-game visual/timing and listening limitations remain.

Classic content identification agrees independently of the folder label. Its production V1 reader, engine and dialect expose 52 rooms, 23 costumes, 173 scripts and 61 sounds. Reviewed native startup reaches Room 44 with control; its source list still has 59 triggers/27 groups/25 scenes. Fourteen focused checks and one classic conformance row pass. Two new regressions show that unreviewed menu bytes reject automatic inputs without changing the session and that an edition label alone does not select the Spanish preview converter. The parent corrected the obsolete scanner reading limitation; both new tests pass again with that correction.

The enhanced worker independently verifies its Spanish v2 reference (53 rooms, 169 global scripts), its own native inputs and executable-derived font. Four edition tests pass: all 128 text slots match the decoded native font (127 drawable glyphs; slot 64 is natively suppressed), correcting eleven mismatched slots in the earlier built-in fallback. The source list remains 59 triggers/27 groups; the full S7 census is reused. Its [record](../../.verification/maniacenhanced-editions-20261018/README.md) owns the hashes, extraction descriptor and retained failed evidence. This validates the supplied enhanced copy, not another release.

For the exact reviewed Spanish `MANIAC.EXE`, the source decoder and renderer replace the earlier native-font mismatch; the parent's actual-window TV capture shows the accented text. A missing or unreviewed executable keeps the old fallback, so the S7 uncertainty remains for that path. The encoded executable span is not a raw 128-glyph font; the enhanced record retains that failed approach and its correction. No original-game execution or subjective timing comparison is implied.

Records: [classic S9](../../.verification/maniacclassic-editions-20261018/README.md), [enhanced S9](../../.verification/maniacenhanced-editions-20261018/README.md). S8 and authored seeds remain opt-in; another S9 comparison requires another supplied same-family release.

## ManiacClassic and ManiacEnhanced — S7 checkpoint, 18 October 2026

Both editions use the shared source-derived scan with their own native verb names and text. Each has 59 triggers grouped into 27 entries (25 static Scenes, two Dialogue/reactions). After the native direct-action context correction, fresh previews begin six classic entries and seven enhanced entries; 21/20 do not begin within the trial. No census run throws or reaches the 6000-frame end bound. A beginning is not full-story or original-game visual acceptance.

Three selected examples per edition begin and end with source attribution and displayed text: the doorbell, circuit breakers and TV. Classic takes 187/905/366 frames; enhanced 187/888/390. Classic four focused tests pass; enhanced thirteen focused checks including LOOM pass. The parent records final WPF and full-suite results. Classic doorbell and TV playback are verified through the actual room list/global picker, with captures inspected.

The preview factory sends reviewed fingerprinted Syd/Bernard/Start clicks and discloses the assumed party; interactive Play retains the player's choice. Direct object activation binds native active verb/object arguments rather than leaving stale startup arguments. It does not earn the route to the room or invent story prerequisites. Scenes that do not begin remain hidden in Normal mode once known, with their limitation in Advanced. No catalog or seed is authored.

Retained limits: classic doorbell's later movement reaches unsupported Script 174 `getClosestObjActor` after its cutscene returns; classic/enhanced original-picture, darkness/flashlight, walking-metric and perceived timing limits remain. Enhanced native font/title-display differences remain disclosed in its record. S8/S9 were not performed.

Records: [classic S7](../../.verification/maniacclassic-cutscenes-20261018/README.md), [enhanced S7](../../.verification/maniacenhanced-cutscenes-20261018/README.md), [integration checkpoint](../checkpoints/20261018-S7-ManiacZak.md).

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
