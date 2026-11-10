# GameContext: a clean slate per game (10 November 2026, roadmap step 1)

187 of MainWindow's fields (play, LOOM's session, the room view) moved into the nested `GameContext` (`MainWindow.GameContext.cs`); `OpenGameFolder` replaces the object after a successful scan. The static caches keyed by folder (`ScummDoorwayFinder.WalkVerbs`, `LoomDemoLooks`, `LoomEgoArrivals`, `ManiacFirstVisit`, `ScummV1ActorColors`, the window's three object-state caches) take `GameLibrary.Key`.

Left on the window on purpose: widgets, view options, sound devices, generation counters and in-flight counts (they must keep counting across games).

Checks, before and after on the same data:
- `scene-setup-20261031/WindowCheck` Indy 3 `r:1`, `G:` LOOM `r:2`, `G:` Maniac Mansion `r:1`: same end states (`before-window`, `after-window`; timing of the samples differs); the pictures show nothing of the earlier games.
- `game-module-20261026/Check`, nine games: reports identical apart from the object note and status text that vary between runs (`before-check`, `after-check`).

Both harnesses now look a field up in `_ctx` when the window no longer has it; other harnesses that read moved fields by reflection need the same lookup. The unit test suite was not run for this change.
