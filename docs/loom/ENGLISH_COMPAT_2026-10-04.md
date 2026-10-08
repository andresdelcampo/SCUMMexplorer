# English LOOM EGA compatibility audit (4 October 2026)

**Historical migration record; status reconciled 8 October.** English Roland EGA is now the replay/test reference. The initial failure counts and worklist below describe the first migration checkpoint, before subsequent triage and corrections. They are not current unresolved failures. Current results and remaining EGA/VGA comparisons are in [CURRENT_WORK](../CURRENT_WORK.md) and [the plan](../LOOM_EGA_PLAN.md). Spanish comparison files now reside in `Games/ALTERNATIVE LOOMS/loom ega mt32 es`; environment overrides select that edition without changing English defaults.

The user added the English DOS EGA release as `Games/loom ega mt32 en` ("fully compatible" at a glance) and asked that the tests use it by default: the failures are the list of places where the code or the tests assume the Spanish release (`Games/loom`, to be renamed `Games/loom ega mt32 es`). The helper `tests/SCUMMexplorer.Tests/TestGames.cs` picks the folder (`loom ega mt32 en`, then `loom ega mt32 es`, then `loom`); `SCUMM_LOOM_TEST_ROOT` overrides it with a full path (set it to the Spanish folder to get the old baseline: 703 passed, 5 skipped, before the sphere and interaction changes of 4 October).

**Run on English data:** 710 tests, 642 passed, 63 failed, 5 skipped (after Game Status was fixed: 42 failed of 710; triage at the end of this file). Nothing here is triaged beyond the first look below; do not assume a failure is a test problem or an app problem before reading it.

## First look at causes

- **Script byte offsets differ.** English scripts have other lengths, so anything keyed on an offset breaks. Seen: `LoomGameStatusTests` (about 20 tests): "Loaded script 023 +0x0020 does not establish the status contract." This is app behaviour (the Game Status window reads the draft table through fixed offsets), not only a test: **Game Status would not work on the English game** until the contract is found by instruction pattern, not by offset.
- **Charset 0** reads 128 where the test expects 240 (`LoomCharsetTests.CharsetZeroIsReadFrom99Lfl`): the English font has other dimensions or a different glyph count; the text renderer needs a check with English text.
- **Boot / opening:** `LoomBootTests` (4 tests) and `LoomCutsceneTests`, `LoomKeyInputTests` fail on boot and trace expectations (Room 69 local script offsets, the sky description, the intro). Needs reading one by one.
- **Spanish text assumed:** `LoomMessageTests` (sky and leaf descriptions), `LoomGlossaryTests.ObjectNamesComeFromTheGameData`, `LoomRoomObjectsTests.StoredObjectNamesLabelRoom17Hotspots` (object names), `LflResourceChainTests`, and the golden outputs of `LoomScriptPresenterTests` (scripts 6, 17, 91; golden files are Spanish).
- **Scene and catalog logic:** `LoomSceneSetupPlannerTests`, `LoomSceneSetupSceneTests` (cage scenes, El Muerto), `LoomReplaySequenceTests` (firewood, Rusty's ghost), `LoomFirstVisitRenderingTests` (opening frames of Rooms 2, 56, 58, 65, entry states 18/42, class-tested entries 38/46), `LoomRoom03ExitTests`, `LoomRandomPickTests`: probably offset- or text-keyed expectations; the catalog (`loom-replays.json`) and the glossary are generated from the Spanish data, so English runs may also need their own catalog.

## Suggested order

1. Make Game Status work on English: derive the draft-table contract from the instruction pattern of script 023/026 (an app fix), then re-run `LoomGameStatusTests`.
2. Run the app on `Games/loom ega mt32 en`: boot, the room picker, a few rooms, texts (charset), a click and a cutscene; note what looks wrong.
3. Triage the remaining failures into test-only (Spanish text, offsets: make the assertion edition-neutral or give each edition its own golden) and app issues.
4. Decide how the catalog and glossary data are made per edition (generated at open time, or one file per edition).

## Findings and triage (4 October, later the same day)

Evidence: `ScriptDump` (`.verification/ScriptDump`, a disassembler: `ScriptDump <folder> <script ids>` or `ScriptDump <folder> room <room> <local id>`) on both folders, the failing assertions, and UiShots on `Games/loom ega mt32 en` (`.verification/english-compat-20261004/uishots`: rooms, English texts, verbs, "Last leaf of the year." play on the English data).

**The English release is not a translation of the same code.** Same scripts, other lengths, and other numbering:

- The Spanish `bit[0xD62]` ("full animation", set by its boot) does not exist; Spanish bits from 0xD63 on are one lower in English (`bit[0xD6E]` "cutscene running" is 0xD6D; `bit[0xDC5]` is 0xDC4; story bits start at 0xD71, not 0xD72). Bit 0xD61 and all variables (`var[...]`) match. Measured from script 023's learned-draft bit (0xD68 against 0xD69); seen uniform through 0xDC5 in the scripts compared (010, 017, 022, 023, 025, 026, 034, 001 and Room 46 local 204, plus Room 38's entry).
- Object numbers and class numbers shift by one from some object on (Room 46 class tests 0x2AB-0x2AD are 0x2AC-0x2AE; object 599 / 637 / 830 / 885 / 827 of the tests are not those objects). Offsets inside scripts move with the text length.
- Real behaviour differences, not only labels: English script 001 (boot) does not set the Spanish `bit[0xD62]` or `bit[0xD76]`, so Room 03's entry starts script 0x2A there. Charset 0 has 128 glyphs (no accents), the file is half the size.

### Fixed in the app (committed)

1. **Game Status** found the draft contract by byte offset and by Spanish bit numbers; it now finds every instruction by pattern and reads the learned/reverse/last-note bits, the two draft wait bits (used to decide whether the status can be edited) and the staff checkpoint bit from the scripts. All 19 `LoomGameStatusTests` pass on English and on the Spanish folder.
2. **Hard-coded Spanish bit numbers in the app:** the "draft is playing" test in `MainWindow` (three places, bit 0xD66) is now `session.DraftPlaying`; the story-bit range (`LoomStoryEvents.FirstBit`, the planner and `UpdateStoryBit`) starts at `FirstBitFor(game)`.
3. **Glossary bit names** (18 entries written for the Spanish numbering) would have labelled the wrong bit on English. `LoomGlossary.Load(game)` now looks bits from the Spanish 0xD63 on up one lower (the Spanish 0xD62 has no English counterpart) for a shifted edition, and the entry says so and carries the evidence level "reading" (the shift is measured; each name is not re-checked entry by entry).

### Remaining failures (42 of 710 on English after step 1) triaged

Test-only: the assertion names Spanish text, a bit number, an object or class id, or a byte offset. The fix is an edition-neutral assertion (derive the id, bit or offset from the data) or a per-edition expectation; none needs an app change.

| Group | Tests | Why |
|---|---|---|
| Spanish text | LoomMessage x4 (sky, leaf), LoomCharset sky line, Glossary `ObjectNamesComeFromTheGameData`, RoomObjects `StoredObjectNamesLabelRoom17Hotspots`, SceneSetupScene El Muerto lines, ScriptPresenter goldens 6, 17, 91 (also bits and offsets) | "Amanecer.", "hoja", Spanish lines; English texts shown correctly in the app (UiShots) |
| Charset | `CharsetZeroIsReadFrom99Lfl` | English font has 128 glyphs, not 240 |
| Bit numbers | LoomBoot x4, KeyInput `NoteLetters...`, Cutscene `CutsceneRuns...` and `GlobalScript...`, RandomPick Script 42, SceneSetupPlanner `StoryEvents...` and `TheScannerSees...` | D6B/D6E/D66/D7x/D9D/DB6 are one lower |
| Offsets | Boot (`+0x015A` vs `+0x0162`, Script 44 `+0x041B`), KeyInput `EscapeSkipsScript052`, Room 03 doorway (Script 1 `+0x015B`), FirstVisitRendering Rooms 2, 18, 56, 58, 65 (`+0x03A0` is `+0x0395`; `+0x00DA` is `+0x00D5`), UsedBy, ScriptIndex and `LflResourceChain` leftover-script lists | script lengths |
| Object / class ids | FirstVisitRendering Rooms 38, 42, 46, 65, 86; ActorTests (827 is 828); SceneEvidence Room 69 (885); Cutscene `AWalkIntoRoom37sBox8...` (object 599) | ids shift by one |
| Needs a real read | ReplaySequence x2 (firewood, Rusty's ghost: stage 3 waits for Script 85 `+0x05CE`), SceneSetupScene `TheCageScenesStillReachTheirOwnEnd...` (Room 43 `ends=[]`) | likely offsets and ids in the recipe, but not yet read: could hide a real English difference in the scene's start |

Room 03: the test comment says Script 1 sets `bit[0xD62]` (Spanish "full animation") so the entry skips script 0x2A; the English release has no such bit, so on English the entry always starts 0x2A. That is the data, not a defect.

### Catalog and glossary per edition (task 4, decision open)

- `loom-replays.json` lists SHA-256 hashes of the Spanish files and the catalog is not used on another edition (`LoomReplayCatalog` checks them), so the English app has no ready-made cutscene replays yet; everything derived from the scripts (rooms, objects, texts, verbs, Game Status) works.
- The glossary is Spanish-numbered for bits (now handled, see above); variable, script, room, actor and sound names have not been compared with the English data, so those names are unchecked on English.
- Options: (a) generate the catalog at open time from the scripts (large, but removes the hash), (b) a second catalog file made by the same tool for English, (c) leave replays Spanish-only until Indy3 (phase 3) shows what the container needs. Recommendation: (b), because the replay tool already writes the file; ask the user before choosing.

## Migration to English (decided by the user, 4 October, later)

The tests, harness drivers and the replay catalog now use the English release; Spanish stays only as a comparison folder (`Games/loom ega mt32 es`, renamed by the user; ScriptDump `objmap` compares object ids side by side).

- **Tests:** all 711 unit tests pass on English (706 passed, 5 skipped diagnostics). The expectations were rewritten for English text, bits, offsets and object ids; two tests changed meaning: the Room 69 challenge test (the English boot has no copy-protection challenge: Room 69 local 205 only sets the bit, no random picks; the Spanish local 203's "wrong answer" branches are absent) and the script index test (in English every script decodes; scripts 0 and 116 sit off the resource chain inside another resource, Spanish had four).
- **Correction to the first triage:** the English bit layout lacks the Spanish `bit[0xD62]` ("full animation", set by its boot, read by script 046/005 and Room 03's entry) and numbers every Spanish bit from 0xD63 on one lower, not from 0xD65 (script 006 proves it: the Spanish 0xD63/0xD64 pair is 0xD62/0xD63). The glossary shift now starts at the Spanish 0xD63. In English Room 03's entry always starts script 0x2A.
- **Object numbers:** the English Room 33 has one more object (id 557, no name), so every later object id is one higher than in Spanish (599 is 600, 876 is 877, 925 is 926). Class numbers move with them.
- **Catalog:** regenerated with the unchanged pipeline (record in `.verification/english-catalog-20261004/validation.md`): 332 starts, 169/235 sites. `MainWindow.KnownCutscenes` and `FirewoodSteps` carry the English ids and offsets.
- **Not migrated:** about 95 older harness scripts and evidence folders still name `Games\loom` in their text; they are historical records. The living drivers (`run-verify.cmd`, `harness-review-20261004/run-all.ps1`) point to `Games\loom ega mt32 en`. The glossary's variable/script/room/actor names are still unchecked against English beyond the bits. CLAUDE.md still names `Games/loom` (needs approval).

## Glossary and story flags checked against the English data (4 October, night)

Tool: `.verification/GlossaryCheck` (English folder, Spanish reference, output `.verification/glossary-english-20261004/report.md`). It compares each glossary entry's code units across the editions (operation sequence of scripts and room entries, units that mention a variable, bit or sound) and lists who speaks in the scenes the actor names rest on.

- **143 entries after the edit** (135 before): 83 same, 8 near-same (scripts 1, 52 and others, 85-96 % similar), 24 moved, 1 absent, 19 n/a. Every "moved" row is explained by the one-higher object ids from Room 33/34 on, the shifted bit numbers, or a unit that exists in one edition only; none changes what a name says. Real edition differences, kept as notes: Room 18 entry sets `var[0x077]` in Spanish but starts script 0x11 with `var[0x07C]` in English; Room 69 local 203 (the wrong-tries counter, `var[0x063]`) is another code in English (the variable keeps its other six users); Room 35 entry does not read `var[0x070]` in English; English has two scripts the Spanish data does not decode (051, a copy of 052's cutscene; 116).
- **Variables and bits** (names with decoded/engine/reading evidence) all hold on English; the Spanish `bit[0xD62]` has no English counterpart (English `bit[0xD62]` is the Spanish 0xD63, as `LoomGlossary.Bit` maps it).
- **Actors were wrong in the table, not only unchecked.** The lines of script 091 and 093 (the Room 43 cage scene) are spoken by actors 2 and 3, whom Bobbin and the others call "Mandible" and "Cob"; the table named them Elders. Script 098's actor 3 is "Rusty" (Bobbin: "Rusty?"), script 086's actor 5 is "Mr. Stoke", and Room 09's actor 4 (costume 0x82 from Room 09 local 217, lines in local 221 and script 105) is the Dead One, not an Elder. Scoped entries now say so, each with its evidence ("reading": the name comes from the text); the trial's Elders keep the unscoped names. `LoomSpeakerNames.Display(LoomTextEntry)` uses the same scopes in the Game Texts browser. Spanish quotes in the actor sources were replaced by the English lines.
- **LoomEgaStateFlags**: English has 88 story bits set by code (first 0xD71); 23 are named by a scene of the English catalog, 65 show their setter's script. Each named flag matches the Spanish flag one bit higher with the same scene title. No flag name was invented.
- **Goldens per edition**: `RealScriptsMatchTheirGoldenOutput` reads `.verification/readable-scripts/<game folder>/`; the Spanish files moved to `loom ega mt32 es/`, the English ones were generated on the first run (script 91 looked through; 6 and 17 not yet reviewed by hand).
