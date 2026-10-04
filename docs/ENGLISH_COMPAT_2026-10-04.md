# English LOOM EGA compatibility worklist (4 October 2026)

The user added the English DOS EGA release as `Games/loom ega en` ("fully compatible" at a glance) and asked that the tests use it by default: the failures are the list of places where the code or the tests assume the Spanish release (`Games/loom`, to be renamed `Games/loom ega es`). The helper `tests/SCUMMexplorer.Tests/TestGames.cs` picks the folder (`loom ega en`, then `loom ega es`, then `loom`); `SCUMM_LOOM_TEST_ROOT` overrides it with a full path (set it to the Spanish folder to get the old baseline: 703 passed, 5 skipped, before the sphere and interaction changes of 4 October).

**Run on English data:** 710 tests, 642 passed, 63 failed, 5 skipped. Nothing here is triaged beyond the first look below; do not assume a failure is a test problem or an app problem before reading it.

## First look at causes

- **Script byte offsets differ.** English scripts have other lengths, so anything keyed on an offset breaks. Seen: `LoomGameStatusTests` (about 20 tests): "Loaded script 023 +0x0020 does not establish the status contract." This is app behaviour (the Game Status window reads the draft table through fixed offsets), not only a test: **Game Status would not work on the English game** until the contract is found by instruction pattern, not by offset.
- **Charset 0** reads 128 where the test expects 240 (`LoomCharsetTests.CharsetZeroIsReadFrom99Lfl`): the English font has other dimensions or a different glyph count; the text renderer needs a check with English text.
- **Boot / opening:** `LoomBootTests` (4 tests) and `LoomCutsceneTests`, `LoomKeyInputTests` fail on boot and trace expectations (Room 69 local script offsets, the sky description, the intro). Needs reading one by one.
- **Spanish text assumed:** `LoomMessageTests` (sky and leaf descriptions), `LoomGlossaryTests.ObjectNamesComeFromTheGameData`, `LoomRoomObjectsTests.StoredObjectNamesLabelRoom17Hotspots` (object names), `LflResourceChainTests`, and the golden outputs of `LoomScriptPresenterTests` (scripts 6, 17, 91; golden files are Spanish).
- **Scene and catalog logic:** `LoomSceneSetupPlannerTests`, `LoomSceneSetupSceneTests` (cage scenes, El Muerto), `LoomReplaySequenceTests` (firewood, Rusty's ghost), `LoomFirstVisitRenderingTests` (opening frames of Rooms 2, 56, 58, 65, entry states 18/42, class-tested entries 38/46), `LoomRoom03ExitTests`, `LoomRandomPickTests`: probably offset- or text-keyed expectations; the catalog (`loom-replays.json`) and the glossary are generated from the Spanish data, so English runs may also need their own catalog.

## Suggested order

1. Make Game Status work on English: derive the draft-table contract from the instruction pattern of script 023/026 (an app fix), then re-run `LoomGameStatusTests`.
2. Run the app on `Games/loom ega en`: boot, the room picker, a few rooms, texts (charset), a click and a cutscene; note what looks wrong.
3. Triage the remaining failures into test-only (Spanish text, offsets: make the assertion edition-neutral or give each edition its own golden) and app issues.
4. Decide how the catalog and glossary data are made per edition (generated at open time, or one file per edition).
