# Changelog

Recent changes: 18–19 October 2026. Current support and acceptance limits belong in [REQUIREMENTS](REQUIREMENTS.md); current work and handoff are in [CURRENT_WORK](CURRENT_WORK.md). [Earlier entries through 17 October](archive/CHANGELOG_THROUGH_2026-10-17.md) are an optional historical archive, not startup reading. At a checkpoint, retain roughly the latest two calendar days here and archive older entries with their links.

## 19–20 October: talkie speech for Monkey Island 2 and mouth sync (Indy 4, MI2 talkie)

- Monkey Island 2's fan talkie (`mi2 talkie en`/`es`) stores its speech as Indy 4 does: `monster.sou` holds 6808 `VCTL` + Creative Voice File samples, addressed by the texts' four code-10 operands. The generic speech path already read it; its lines now have tests on the S6 route (the sign 0x00A485E0, Largo's choice 0x00B40C6E, each lasting as its sample) and a window check of two scenes from the list.
- Mouth sync (ScummVM `startTalkSound`/`processSfxQueues`/`isMouthSyncOff`, sound.cpp read 19 October 2026): while a talkie line plays, the talking actor's mouth follows the sample's `VCTL` times and closes when the voice ends. Indy 4 has real times (first line: closed +24, open +30, closed +36 jiffies); the MI2 patch gives one placeholder word (4095) per sample, so its mouths stay open until the voice ends (plain talk animation, labelled). The boot and the Indy 4 / MI2 cutscene lists are unchanged.
- Record: [talkie-speech-20261019](../.verification/talkie-speech-20261019/README.md). Listening and looking checks are listed there for the user.

## 19 October: stable selectors and deliberate cutscenes (all games)

- Refined the background-first feature: only LOOM EGA fills selector artwork after background loading. Other cards retain their plain backgrounds on cold and warm loads; native first-visit preparation and complete caching still run upfront.
- Manual catalog navigation cancels the departing scene, queued continuation and audio even without native Skip. Entry playback belongs to native doorway travel or explicit scene launches. Game loading no longer starts the intro; established opening actions remain in Room 01 or a Normal-mode Play Game Intro button, with Play Demo Preview for demos. Requirements R-8.5a–c and R-7.20 preserve native startup choices.
- Private-profile windows verify Maniac's real party selection, intro restart and no-override telescope cancellation; LOOM's gap filling and existing intro deduplication; and explicit demo playback/cancellation. Monkey CD's native roomless intro reaches control without supplied answers; its selected doorway route remains unproved. Unresolved manual starts stay static, with no invented story flags or input.
- Required full suite: **1,684 passed, zero failed, five expected skips**, including all 12 new policy cases. Final solution build zero warnings/errors; support levels are unchanged. Retained failed approaches, precise window coverage, regenerated matrices and clean promotion: [checkpoint](checkpoints/20261019-ManualRooms-ManiacZak.md).

## 19 October: Monkey Island 1 CD S9 for the FRENCH, GERMAN and ITALIAN folders

- The disc's FRENCH, GERMAN and ITALIAN folders each pass boot (rooms 0, 10, 0, 0, 38, unsupported 0), the room audit (82 of 83, every per-room column equal to ENGLISH), the R-7.18 route and right click with their own texts, disc music ids, the cutscene scan (286 triggers, 160 entries, 62 scenes, equal per entry; 101 of 160 begin, equal per entry) and accents (charsets 1, 2 and 6 draw é è ê à ç ô û, ä ö ü ß, à è é ì ò ù). One look per language through the real window's language list passes. No code changed; the registry level stays.
- Differences are data: each copy's texts (`RouteTexts` per copy folder), German Script 12's sentence words " an "/" mit " (script map row), charset 3's missing accents. Tests: `ScummMonkeyCd*` theories over every folder (93 passed with the language-choice, support and architecture tests). Record: [monkeycd-editions-20261019](../.verification/monkeycd-editions-20261019/README.md).

## 19 October: background-first room previews (all games)

- Every room selector receives plain backgrounds before native first-visit details are prepared on background workers. Native initialization remains upfront even when the background already looks complete; heavier scene scans start after the backgrounds. Requirements R-5.1/R-5.2 and new R-5.3b record the requested order.
- Completed selector artwork stays independent of current-room play, fixing characters disappearing after selection. LOOM previews include native actor layers and retain gap-filling, masks and camera framing. Early LOOM selection awaits analysis asynchronously.
- A source/build/language-specific cache commits one complete batch atomically at the end. Cancellation, stale game switches and failed writes preserve the preceding complete cache; warm display still performs native prewarming.
- All 1,002 plain backgrounds across 14 families match source decoders. Eight representative native window batches pass cold/warm ordering, exact pixels, actor preservation and isolation checks; cache and LOOM actor harnesses pass. Support levels remain unchanged. Full regression, retained failures, bounds and clean promotion are recorded at the [checkpoint](checkpoints/20261019-RoomPreviews.md).
- Final required suite: **1,657 passed, zero failed, five expected skips**; final solution build zero warnings/errors and three regenerated-matrix checks pass. The normal executable is refreshed at clean promotion.

## 19 October: consolidate Maniac/Zak support notes and keep recent history

- Current Maniac/Zak acceptance limits now live in [REQUIREMENTS](REQUIREMENTS.md#maniac-and-zak-support-boundaries), with completed work in this changelog and its dated evidence. The two redundant support-status trackers are removed; 39 affected Markdown files now link to the existing requirements section. Other families remain with their owners.
- Corrected the stale ten-estimate note to eleven and removed the closed walking-approximation gap. ZakClassic Costumes remains Partial, 24 derived / 11 estimates; contextual variants alone do not clear that cell. Classic Maniac's source-evidence acceptance is retained explicitly.
- Kept 18–19 October active and moved 208 earlier entries to the [optional archive](archive/CHANGELOG_THROUGH_2026-10-17.md), adjusting moved relative links. The playbook now requires no whole-changelog or archive reading and creates no new per-game support trackers.

## 19 October: transformation and Mars palette contexts (S2 follow-up, ZakClassic)

- Traced indirect transformation costumes, retained animal palettes and the distinction between stored and current Mars clothing colours. Two blocked native character-switch preparations are retained without a prerequisite shortcut.
- Costume details offer display-only colour choices from separately attributed source/opening/room-preview contexts. The existing default is preserved; seven estimate cards gain contextual records while all eleven global estimates and Costumes Partial remain. Source conditions and estimates are Advanced.
- Asynchronous progress, cancellation, stale-request guards and window-close cancellation keep context discovery separate from the live game. Actual-window review fixed a plain-label fallback and preserved genuine unused artwork-channel equality. Three family windows and a further ZakClassic regression after the shared language-choice merge pass, preserving native pixels and live state; Classic Maniac and enhanced Zak are unchanged.
- All concurrent master updates are preserved; matrices regenerated, solution build zero warnings/errors. Full-suite counters, clean promotion and remaining boundaries: [checkpoint](checkpoints/20261019-Palettes-ManiacZak.md).
- Final required suite: **1,657 passed, zero failed, five expected skips**; all 46 new cases pass. Source/tests frozen at `d600d53b`; subsequent changes are records and the requested documentation consolidation.

## 19 October: script executor allocations and the cutscene derivation

- **Fewer copies per script run and per frame** (generic, `Core/Scumm/Scripts` and `Session`): a run keeps copies of the object states and positions only for a result the session keeps (`LastEntryRun`, `LastAction`); the variables dictionary comes from a per-thread pool; prepared instructions are made once per disassembly; `ScummGameSession.StepFrame` plays a frame without the snapshot the cutscene runs never read; getActorFromPos reuses its drawing buffers; the v1/v2 path records what its preparation writes instead of copying every variable before each instruction. Allocation per frame: Indy 4 981 → 194 KB, Monkey Island 2 351 → 30 KB, Monkey CD 674 → 61 KB, Zak 3.37 MB → 159 KB, Maniac 236 → 12 KB.
- **Same results.** The twelve `CutsceneList` lists hash the same before and after (on master `fb3da87b`), and so do the eight-worker run lines of the ten generic families; `ScummExecutorAllocationTests` (8) compare `StepFrame` with `AdvanceFrame` frame by frame.
- **Faster.** Eight workers: Zak 55 → 23 s, Zak enhanced 53 → 16 s, Maniac enhanced 45 → 18 s, Indy3 EGA 14 → 10 s, Monkey floppy and CD 14 → 9–10 s, Indy 4 87 → 75 s; Monkey Island 2 unchanged at eight workers. Runs slowed per worker beyond four on this 16-core machine, so the App now derives disk/archive games on at most four workers. Real window, first launch, from play start: Monkey Island 2 55 → **29 s**, Indy 4 109 → **68 s** (under a minute not reached for Indy 4: its 6000-frame runs and the per-run variable sync remain). No GC setting changed. [Record](../.verification/executor-alloc-20261019/README.md).
- Tests: full suite on the merged tree (S9 MonkeyCd FR/DE/IT included) **1680 passed, 0 failed, 5 skipped** (`SCUMM_SOUND_TEST_ROOT` = `loom ega mt32 en`, `SCUMM_INDY3_EGA_TEST_ROOT`, `SCUMM_MT32_ROM_ROOT`); solution build zero warnings, zero errors.
## 19 October: choosing the language of a game that holds several (R-3.16)

- Requirement R-3.16 (Rule, confirmed by the user 19 Oct). A survey of every local game by its own data found one game with several languages: the Monkey Island 1 CD (`ENGLISH`, `FRENCH`, `GERMAN`, `ITALIAN`, `SPANISH`, each `MONKEY.000/.001`); the Indy 4 disc's demos and the LOOM CD are not languages.
- Discovery lists the languages (`GameFolderScanner.ListLanguages`, `GameLibrary.Languages`/`Language`/`Key`), labelled by the disc's folder names; the default stays ENGLISH; a missing folder falls back and the note says so.
- The window shows a language list under the game's name only for such a game; choosing one reopens the game in it and remembers it per folder (`game-languages.txt`). Play, thumbnails, CD room lists, first-visit scenes and ego arrivals are keyed by folder and language.
- Tests: `GameLanguageChoiceTests` (4) pass; discovery/MonkeyCd/support/architecture 78 passed; LOOM 836 passed (5 skipped); WPF `LanguageWindowCheck` all PASS (Spanish verbs and `Mirar cartel` in play). [Record](../.verification/language-choice-20261019/README.md).

## 19 October: cutscene lists from one boot (cutscene snapshot, all generic families)

- **One boot per game.** `ScummBootSnapshot` keeps a deep copy of a booted session (variables, script slots, actors, objects, room, camera, verbs, sound timelines, seeded picks, caches; the game's readers shared) and every cutscene run starts from a copy of it; disk and archive games use a pool of at most one boot per worker (`ScummBootSnapshotPool`). The App hands entries to its workers one at a time, and `getActorFromPos` keeps the costumes it has read (an Indy 4 frame allocated 4.2 MB, now 0.96 MB).
- **Same results.** The twelve `CutsceneList` lists (LOOM EGA/CD, Indy3 EGA/VGA, Monkey floppy/CD, Monkey2, Indy4, Maniac and Zak both editions) and the eight-worker runs hash the same before and after; `ScummBootSnapshotTests` compare runs from a copy with runs after their own boot, trace line for line.
- **Faster.** Eight workers: Monkey Island 2 about 100 s → 55 s, Indy 4 126 s → 70 s, Monkey CD 32 → 12 s, Monkey floppy 33 → 11 s, Indy3 41 → 10 s; real window on first launch: Monkey Island 2 140–174 s → 70 s, Indy 4 185 s → 129 s (machine shared with other runs; under a minute not reached for the window). Remaining cost: the executor's per-script result copies. [Record](../.verification/cutscene-snapshot-20261018/README.md).

## 19 October: source item qualifiers and resumed fidelity (Maniac/Zak)

- Zak's repeated CashCards, helmets, tickets and tokens use native character qualifiers inferred from each copy's scripts. Shards keep neutral fallback. Current ownership stays separate, and qualifiers survive admin transfers/Undo; catalogs remain 70 items. No character-name or object-ID label table is added.
- Native flight cruising expires normally and automatically disembarks at the purchased airport. The shared executor now binds a known variable room to the scene event target while preserving unknown/estimated provenance. Both source boundaries and real-window routes pass without timer edits or extra arrival input.
- V1/v2 walking uses the native row search, metric, box priority and destination recalculation. Four-family source-reference coordinates and native routes pass. Both Zak copies also complete a source-supported paid bus segment after disclosed preparation.
- Requirements/support notes and source attribution are updated. Levels stay unchanged; Classic Zak's 11 palette estimates, original-runtime comparisons and unavailable same-family editions remain. [Checkpoint](checkpoints/20261019-LabelsFidelity-ManiacZak.md).
- Final solution build: zero warnings/errors; three regenerated-matrix checks; full suite **1538 passed, zero failed, five expected skips**, all 64 new cases passed. Six serial actual-window routes pass. Final clean integration and normal executable refresh are recorded at the checkpoint.

## 18 October: Spanish editions of Indiana Jones 4 and Monkey Island 1 CD (S9, Indy4 and MonkeyCd)

- **Indy4.** `indy4 talkie vga es` (Spanish texts over the English voices: the same speech offsets and `MONSTER.SOU` size) and `indy4 classic vga es` (Spanish floppy) boot, pass the room audit (96/96 without a gap), the R-7.18 route with keys and speech, the cutscene scan (666/426/182, 210 begin, equal to English per entry) and the accent check. Record: [indy4-editions-20261018](../.verification/indy4-editions-20261018/README.md).
- **The floppy's stone disk (shared Core, additive).** Room 81's answering script did nothing with local 0 = 0, so the boot stood still. `ScummSessionBoot.NextStartArgument`: after 0 fails, the answering script is started with the next literal the room's own code passes (the submit's 1), labelled assumed input; other games' boots unchanged.
- **Monkey Island 1 CD (shared Core, additive).** `monkey1 vga es` is the Spanish four-disk floppy (MonkeyFloppy, checked there: only Enter is assumed, its quiz does not wait). The Spanish CD is the reference disc's own `SPANISH/` folder: `GameFolderScanner.DiscFolder` reads it (the app still opens ENGLISH); it passes boot, room audit, the route with right click and disc music, the scan (286/160/62) and accents. Record: [monkeycd-editions-20261018](../.verification/monkeycd-editions-20261018/README.md).
- **Tests.** Spanish cases in `ScummIndy4PlayTests`, `ScummIndy4SessionTests`, `ScummIndy4CutsceneTests`, `ScummMonkeyCdPlayTests`, `ScummMonkeyCdSessionTests`, `ScummMonkeyCdCutsceneTests` and the MonkeyFloppy classes (`vga es`); charset coverage per copy (`ScummCopyTextBytes`).

## 18 October: the other Monkey Island 2 copies (S9, Monkey2)

- **All four other copies pass** (`mi2 mt32 en`, `mi2 sb16 en`, `mi2 classic es`, `mi2 talkie es` against `mi2 talkie en`): boot to the campfire, room audit (107 of 110 rooms without a gap, states equal but two patched rooms), the play route with right click, LeChuck's iMUSE beat, the cutscene scan and runs (floppies 190 groups / 97 begin, talkies 260 / 165), and the accents. Record: [monkey2-editions-20261018](../.verification/monkey2-editions-20261018/README.md).
- **Floppy copy protection (shared Core, small and additive).** The floppy releases ask the recipe copy protection before the full/Lite choice; the boot presses Enter, types the four digits Script 130 itself derived into `var[0x206]` (labelled assumed input, `ScummBootInputPlan.AnswerDigits`), then clicks the full game once it is touchable. Before, the floppies stood at Room 108 until the frame limit.
- **Cutscene titles** drop the '@' filler the Spanish talkie pads its verb names with (`ScummCutscenePlanner.VerbsOf`).
- **Tests.** The Monkey2 session, first-visit, play and cutscene tests run over the five copies, with route texts per copy (the two Spanish copies differ: "Mirar a" / "Mirar"); a glyph check per copy. One look through the real window at the Spanish floppy: Spanish verbs, "Mirar a rótulo", dialogue lines with ¿ and ¡. Registry unchanged.

## 18 October: Game Status pictures, party and logical item choices, Maniac/Zak

- Maniac waits for native Start and offers the selected three kids. Every Maniac/Zak recipient uses a native full-body sprite; private frame initialization supplies off-room Zak characters without changing live state.
- Zak's previous 76 indexed candidates included six source-proven scene aliases. Both editions now offer 70 logical inventory items. Separate cards, shards, helmets, tickets and tokens retain distinct choices with stable numbered labels. No name-based merging or room/item ID exception is added.
- Item, recipient and Add controls are compact and bottom aligned. Admin grants, native capacity, atomic Undo and modal pause remain. Requirements record the user's clarification; all four Status cells remain Full. [Checkpoint](checkpoints/20261018-StatusPolish-ManiacZak.md).

## 18 October: Monkey Island 2 cutscenes from its scripts, with a song's beat (S7, Monkey2)

- **The list.** The shared scan gives 536 triggers and 260 entries (76 scenes) with the game's verb names after the boot (Open … Walk to); 165 begin from a fresh start; eight-worker runs take 153 s in the harness and 140–174 s in the window on first launch (each run boots MI2 in about 0.9 s).
- **A song's beat (shared Core).** `soundKludge` player command 0 (`Player::getParam`) 7 and 8, the beat and the tick in the beat, are answered from the playing song's AdLib timeline (estimate). LeChuck's arrival (Script 76) waited for beat 9 for ever; it now plays through to its dialogue. The play boot reaches the campfire after 2841 frames (3409) since the campfire story follows its song.
- **First texts.** The talkie's speech controls no longer show (code 10's operands; the same change as S7 Indy4).
- **Registry.** Monkey2 Cutscenes is ◐; the matrix is regenerated. `ScummMonkey2CutsceneTests` (3): Talk to barkeeper (Room 9), Largo's room (Room 13 entry, on to Room 12), the voodoo key (Room 74, Part 3) with its song 176 and the beat. Window check, decisions and uncertainties (Largo at the bridge needs a walk): [record](../.verification/monkey2-cutscenes-20261018/README.md).

## 18 October: complete admin inventory catalogs, Maniac/Zak

- Own-edition source censuses establish 48 inventory items per Maniac edition and 76 per Zak edition. Zak nest53 is added; seven shop display headers and Maniac's bank35 are removed as false positives. Letter139 is a scene container; native Read/Pickup gives card27.
- Generic source flow preserves finite alternate targets, caller arguments and special-handler identities, with native comparison/alias/mutation semantics. Explicit Zak recording-loop bounds have independent finite inventory-coverage proofs. No room/item engine exception or earned prerequisite is introduced.
- All-item grant/retained-verb/exact-Undo and native capacity/stale checks support Status Full for all four families. Four sequential windows retain all seven Maniac recipient pictures and correct catalog choices. Both generated matrices follow only those four cell changes.
- Final solution build zero warnings/errors; three matrix checks; required full suite **1,422 passed, zero failed, five skipped**, all **65 new cases passed**. Five sequential window processes pass, with 46 captures and 13 visual inspections; source-analysis limits remain explicit. [Checkpoint](checkpoints/20261018-Catalog-ManiacZak.md), [integration evidence](../.verification/maniaczak-catalog-20261018/README.md).

## 18 October: admin Game Status clarification and shared playback controls, Maniac/Zak

- Requirements now permit explicit unearned inventory grants, pictured Maniac recipients and future native icons for graphical inventory games. The old held-only restriction is superseded; grants never silently complete unrelated puzzles/conversations.
- Source catalogs offer 49 named items per Maniac edition and 82 per Zak edition, all seven pictured Maniac kids, native 80-slot capacity, atomic Apply Changes/Undo and pending-choice Close. Status is Partial under the broader all-items requirement while indirect targets, including Zak's letter 139, remain unclassified. Both generated matrices follow the registry.
- Normal sound selection is restored through supported capabilities. Generic scene locks, immediate transport/caption refresh and paused Skip resume match LOOM, with v1/v2 game variables preserved across device changes. Pause/current native Skip/room launch availability are explicitly independent.
- Final solution build zero warnings/errors; required full suite **1357 passed, zero failed, five skipped**, all 18 new cases executed; scoped and real-window checks are recorded separately. One concurrent window assertion did not reproduce and is retained without claiming a production fix. [Checkpoint](checkpoints/20261018-Admin-ManiacZak.md), [integration evidence](../.verification/maniaczak-admin-20261018/README.md).

## 18 October: Zak native CashCard, ticket choices and boarding, both editions

- Old object-header class masks now permit the native knife/card combination. Actor logical-room identity survives mapped resources, scene changes and same-resource camera follows. Later engines retain their behavior; no story-state shortcut or object-specific engine rule.
- Both supplied editions use native Buy/Cancel choices and ticket guards, board, consume the ticket, serve peanuts and release Script104's wait. The old "ticket object51" interpretation was wrong: it is peanuts; actual tickets are82/83.
- Preparation is disclosed room previews/direct knife pickup. Two failed bus-input approaches remain parked; the native 28800-jiffy cruising timer and destination arrival remain separate. Matrices retain their levels. Own-edition tests, review, actual windows and final full-suite/build result: [flight checkpoint](checkpoints/20261018-Flight-ManiacZak.md).

## 18 October: Fate of Atlantis cutscenes from its scripts, with their speech (S7, Indy4)

- **The list.** The shared scan gives 666 triggers and 426 entries (182 scenes). 210 begin from a fresh start (the credits); the eight-worker runs match the single-threaded ones.
- **Verb names after the credits (shared).** The play boot shows no verb, so `ScummCutscenePlanner` runs such a boot on with the game's own Esc skips until a verb is on: two skips, New York, Script 19's names. Titles read "Use fire escape", "Talk to ticket taker"; only `verb 0x50` keeps its number (4 entries, 187 before).
- **Spoken first lines (shared).** `ScummTextDecoder.DecodeLine` reads code 10's operands (the voice's offset and size) as spaces, so first texts no longer start with stray characters. Only code 10 changed, and the LOOM, Indy3 and Monkey lists stay byte-identical.
- **Speech in scenes from the list (shared App).** A room-entry scene played in a room opened for it speaks its lines from the visit (`TalkieSpeechPlayer.Attach(from)`).
- **Registry.** Indy4 Cutscenes is ◐; the matrix is regenerated. `ScummIndy4CutsceneTests` (3) covers Smith's scene (Room 1), Sophia's lecture (Room 11, then backstage) and the diving suit (Room 82, then Room 42) from a fresh start, plus the lecture's spoken line. Window check, decisions and uncertainties: [record](../.verification/indy4-cutscenes-20261018/README.md).

## 18 October: native Maniac battery/flashlight Use-with, both editions

- Native object-header prepositions now work for v1/v2 room and retained inventory code. Classic's automatic sentence preposition and enhanced's explicit opcode both use the source bits; pickup preserves a changed header. No object-specific engine exception or input-mode assignment.
- Both supplied Maniac editions compose their own two-object sentence, insert and consume the batteries through native inventory input, and switch on the flashlight. Wrong-target and missing-battery checks preserve the native prerequisite. Preparation is a labelled exploration action, not an earned story route.
- Support levels remain unchanged. Own-edition tests, independent review, actual-window evidence and final suite/build results: [Use-with checkpoint](checkpoints/20261018-UseWith-ManiacZak.md). Original-game visual/timing comparison, walking approximation, other puzzles/parties and alternate same-family releases remain open.

## 18 October: Fate of Atlantis plays through its own interface, with its spoken lines (S6, Indy4)

- **The route.** The acceptance route of R-7.18 runs from Barnett College (`ScummIndy4PlayTests`):
  - a click in the playable credits (Script 78);
  - the game's own Esc skip (Room 5 local 214's override);
  - "Look at marquee" in New York;
  - "Pick up newspaper", which gets an inventory icon from Room 98;
  - a dialogue with the ticket taker: lines as verbs 0x78 up, and Indy says the chosen line with its voice;
  - three crate pushes and "Use fire escape", whose climb cutscene Esc skips into Room 12.
- **Speech.** Talkie text codes start spoken lines from `MONSTER.SOU`. After the boot, a line stays until its sample ends (ScummVM `processSfxQueues`). The play window speaks them (`TalkieSpeechPlayer`).
- **Shared fixes, each from ScummVM.** Code 7 strings in prints and in verb names said through code 5 (v5); `loadString` on a seeded-pick path; a dialogue line's speech codes no longer draw as glyphs; a deferred camera follow does not enter the current room twice.
- **Registry.** Indy4 Objects and Play are ✔, and the matrix is regenerated. Real-window check, decisions and uncertainties: [record](../.verification/indy4-play-20261017/README.md). Current-state object images and iMUSE music in play come from S6 Monkey2's shared additions (merged).
## 17–18 October: play through Monkey Island 2's own interface, with iMUSE music (S6, Monkey2)

- The R-7.18 route runs on `mi2 talkie en` through its own verbs, inventory icons and dialogue lists. From Woodtick (Room 7), where the game goes on to by itself after the campfire, it looks at the sign and skips that cutscene, then picks up the sign (the shovel, shown as its Room 94 icon). It holds Largo's two dialogue lists (input scripts 14 and Room 7's 206), then leaves through the dinky door to Room 8. A right click runs the default verb (`var[0x09C]`). **Objects ✔ and Play ✔** for Monkey2.
- Shared play additions (also for Indy4):
  - The play screen draws each v5 object in its current state's image (9b28623).
  - iMUSE songs in play loop from their loop point on the chosen device's part (`ScummImuseMusic`, `SceneSoundPlayer.Loop`; c3b1c5b).
- Engine:
  - A jump hook a script arms for a playing iMUSE song (`setHook` class 0, including a jump to another MIDI track) sets the song's length on the session's clock (4a950b5). Without it, Room 4's cutscene waited for ever.
  - Text verbs skip the two bytes of codes 9, 10, 12, 13 and 14 (d5af7d3).
- Validation: `ScummMonkey2PlayTests` (3), `ScummPlayScreenStateImageTests`, `ScummImuseMusicTests`, `V5ImuseLoopTests`; the play, sound, Monkey, Indy, cutscene and conformance filter; the LOOM and Scumm regression; one WPF check through the real window. Record: [monkey2-play-20261017](../.verification/monkey2-play-20261017/README.md).

## 18 October: native Maniac/Zak engine fidelity (all four families)

- Classic Maniac continues through Script 174 after a genuine boot-to-doorbell route. Native closest-query edges/ties and v1/v2 actor-approach units are implemented; no actor/room workaround.
- Native darkness, flashlight geometry/defaults and dark actor palettes are rendered in Play, room previews and thumbnails. Four actual-window bitmap comparisons and both Zak moving beams pass; captures inspected. Version rules preserve later-family behavior.
- V1 costume changes now retain dynamic palettes. ZakClassic context evidence includes early boss palette 0, later 7, variable writers and alternate actors; the 24-derived/11-estimate card table stays Partial.
- Two previously capped starts complete per Zak edition (646/907 frames). Three retained caps are classified native ticket-state waits or the terminal Escape/restart loop; no prerequisites/input fabricated. Cutscenes stays Partial. Matrix regeneration passes three checks; final integrated validation is in the [checkpoint](checkpoints/20261018-Fidelity-ManiacZak.md).
- Final validation against master `9430cb5`: **1286 passed, zero failed, five skipped** (7m37s); build zero warnings/errors; 65 integrated focused checks, three matrix checks and four actual-window processes passed. Maniac battery Use-with insertion remains unresolved after two retained attempts; native acquisition succeeds, no successful battery beam is claimed.

## 18 October: explicit Maniac/Zak inventory status and undo (S8, all four families)

- Game Status shows held inventory across the source-established playable characters, with explicit reassignment, Apply/Cancel and guarded Undo. The window pauses Play and restores its prior state. Status None → Full for this bounded editor; README/REQUIREMENTS matrices regenerated. No new items, puzzle flags, party changes or actor placement edits.
- Native party bindings/player ranges and reached actor names supply identities per edition. Ownership-only writes retain slots, source room/code, state/classes and unrelated session values. Opaque session/frame/signature/revision tokens reject stale or cross-session edits; any game frame invalidates Undo. Choices carry explicit attribution and are not played scenes.
- Twelve family tests and 39 integrated focused checks pass; all four real-window Apply/Undo/Cancel routes pass with inspected captures and prior pause-state restoration. A native-slot regression fixes the initial false refusal on Zak's resident special handlers without allowing real foreground actions. Failed guard/config/display checks are retained. Existing visual, sound/timing, walking, costume and alternate-copy limits remain.
- Proposed R-12.7–R-12.10 are separate. [Checkpoint](checkpoints/20261018-S8-ManiacZak.md) and [integration evidence](../.verification/maniaczak-state-20261018/README.md) hold exact commands, final build/suite numbers and family links.
- Final validation against master `cb17c44`: 1240 passed, zero failed, five skipped; solution build zero warnings/errors; three matrix checks and all four real-window checks passed.

## 18 October: bounded Maniac/Zak edition checks and native Spanish font (S9, all four families)

- Content inventory finds one local copy per family. Available-copy checks pass; additional-language/revision/demo comparisons are blocked by missing data. Classic/enhanced are separate families. README/REQUIREMENTS edition notes are explicit; feature levels remain unchanged and matrix checks pass.
- The supplied enhanced Spanish executable provides its actual font through a reviewed descriptor and source-backed decoding. All 128 text slots pass (127 drawable glyphs and the existing marker suppression), correcting 11 localized glyphs; real-window TV text displays `músicos y diseñadores`. Missing/unreviewed executables retain the previous fallback. The rejected raw-slice attempt was reverted and its failed evidence retained. No font/executable asset is shipped.
- Two obsolete v1/v2 detection notes and Maniac's documented classic dialect are corrected. Independent review found no actionable issue. Existing original-game visual/timing, darkness/flashlight, walking, Classic Maniac Script 174 and Zak estimate/frame-bound limits remain open; no ScummVM executable, S8, catalog or seed work.
- [Checkpoint](checkpoints/20261018-S9-ManiacZak.md) and [integration record](../.verification/maniaczak-editions-20261018/README.md) link family records, exact commands and final validation.
- Final validation: 1227 passed, zero failed, five skipped; solution build zero warnings/errors; three matrix checks and the targeted real-window font check pass.

## 18 October: Monkey Island CD lists its cutscenes from its scripts and plays them (S7, MonkeyCd)

- The shared scan reads the CD's v5 resource file (through its disc image) unchanged: 286 triggers, 160 entries (62 scenes), 101 begin from a fresh start; titles use the CD's own verb names after the boot (2 Open … 8 Look at, 0x0A Talk to, 0x0B Walk to); `verb 0x50` and `verb 0x3D` stay raw because the game names neither.
- Acceptance: `Room 33 · Look at poster` (object 429 verb 8), `Room 37 · Entry` (Script 60) and `Room 19 · Entry` (the ship, local script 200, on to Room 7; it starts its disc track, sound 102). Background derivation: 8 workers, each with its own copy read from the disc image; results identical to one thread; 51 s in the window on first launch. App fix (shared): an entry played from the picker in another room no longer runs the entry twice (the ship played nothing).
- Registry: MonkeyCd Cutscenes – → ◐. Validation: `ScummMonkeyCdCutsceneTests`; support, cutscene and play tests; LOOM regression; LOOM, Indy3 and Monkey floppy lists byte-identical. Record: [monkeycd-cutscenes-20261017](../.verification/monkeycd-cutscenes-20261017/README.md).

## 18 October: script-derived Maniac Mansion and Zak McKracken cutscenes (S7, all four families)

- Cutscenes None to Partial for ManiacClassic, ManiacEnhanced, ZakClassic and ZakEnhanced. Three source-attributed fresh-start examples per family complete; room-list and global-picker actions pass in four actual windows. Maniac lists 27 groups (6/7 starts); Zak 54 (13 per edition, five bounded runs).
- Old script-call/text syntax, source-labelled begins and direct-action argument binding are supported. Maniac uses a disclosed, fingerprinted native startup preview while preserving queued/partial/completed player choices. Spanish readable labels follow verified executable glyphs; rendered text is unchanged.
- State prerequisites are not invented. Classic post-doorbell Script 174, five Zak frame bounds, Spanish rendered-font mismatch and S5/S6 visual/walking limits remain recorded. Proposed R-8.34 is separate; no shipped seed, hand catalog, S8 or S9 work.
- [Checkpoint](checkpoints/20261018-S7-ManiacZak.md) and [integration evidence](../.verification/maniaczak-cutscenes-20261018/README.md) hold exact counts, source proof links, window commands and final validation.
- Final validation on current master plus S7: 1215 passed, zero failed, five skipped; build zero warnings/errors; three matrix tests and four real-window checks passed.
