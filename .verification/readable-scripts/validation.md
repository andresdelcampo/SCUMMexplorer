# Readable script view (3 October 2026)

Step 3 of `docs/READABLE_SCRIPTS_PLAN.md`: `LoomScriptPresenter` (Core) renders a decoded script as plain language, with names from `LoomGlossary` and `if / else / while / repeat-until` blocks recovered from the jumps.

- Golden outputs: `script-091.txt` (cage scene, Room 43), `script-017.txt` (ego costume by size), `script-006.txt` (click handler). `LoomScriptPresenterTests.RealScriptsMatchTheirGoldenOutput` compares against them; regenerate with `SCUMM_UPDATE_GOLDEN=1` and review by hand.
- Conditions follow ScummVM: a SCUMM conditional jumps when its condition is false, so a block holds the code that runs when it is true (`isGreater var, v` falls through while `var < v`). Backward conditional jumps become `repeat ... until` (a lone `breakHere` becomes `wait until`).
- Names carry evidence (`decoded`, `engine`, `reading`, `play`); the end of each readable view lists every name used with its evidence and source. Unnamed things keep their raw form (`bit[0xD96]`, `var[0x0D1]`).
- Raw stays one click away: the Advanced script view has a Readable checkbox (default on, remembered in `view-options.txt` as `readable=`); the bytecode column belongs to the raw view.
- On screen: `UiShots` (`script-readable.png`, `report.txt`): tree label, page title and subtitle, event rows with names, Readable/Raw switch, bytecode toggle disabled while Readable.

## Story bits named by catalog scenes (3 October 2026)

`LoomEgaStateFlags.BitLabels` (the same catalog titles Game Status uses) feeds `LoomGlossary.StoryBitLabel` after a one-second background scan (`MainWindow.NameStoryBitsAsync`). A bit gets a name only when a catalog scene sets it; the entry is `reading` ("set by the scene: ..."). Bits no catalog scene sets stay `bit[0x...]`. The Assumptions lines get the name after each `bit[0x...]` (`LoomResourceNames.NameBits`; the line stays the tooltip).
- On screen: `UiShots` report, `ui2/report.txt` ("readable head"): script 91 shows `if set by the scene: Room 43 · Opening at jaula / Room 44 · Look at Mandible` for `bit[0xD96]`; `bit[0xD68]` (engine range, no scene) stays raw.
- Filtered tests: `LoomGlossary`, `LoomScriptPresenter` (13 passed).
