# English replay catalog (4 October 2026)

Decided with the user: the tests, harnesses and the replay catalog move to the English DOS EGA release (`Games/loom ega mt32 en`); the Spanish catalog is replaced (it stays in git history: `git log -- src/SCUMMexplorer.App/loom-replays.json`).

Pipeline (the one in docs/loom/LOOM_CUTSCENE_DISCOVERY.md, unchanged), run on `Games/loom ega mt32 en`:

1. `CodeDump` -> `code.tsv` (25 138 lines), `discover_replays.py` -> 362 literal object/entry roots, 218 candidate owners, 128 unresolved edges (Spanish: 363 roots).
2. `ReplayAudit` (Release; restored from git history, it had been removed in the 4 October harness review) -> 362 probes in 4 min 13 s.
3. `MembershipAudit` with `established-starts.json` (Room 36 object 600 instead of 599): the four established starts validate (opening; Room 08 object 255 `endCutscene at Script 46 +0x13AA`; Room 10 object 295 `Script 52 +0x0167`; Room 36 object 600 `Script 86 +0x0536`).
4. `export_catalog.py` -> `loom-replays.json`: 83 scene starts and 249 dialogue/reaction previews (332 starts, Spanish 329), 169 of 235 cutscene sites reached (Spanish 163), 51 owners with outstanding sites (Spanish 55), 332 membership rows.

Checks: `LoomReplay*`/catalog unit tests pass; SceneShots on the English folder plays `Room 02 object 154 Verb 0x36` in the real window ("Last leaf of the year."). The hand-built descriptions in `MainWindow.KnownCutscenes` (trial, Walk to Room 13, firewood, opening) now carry the English offsets and object ids, checked against the membership run above.

Not checked: every one of the 332 recipes in the WPF picker; the 83 scene starts were not played one by one.
