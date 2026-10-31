# The scene derivation kept between launches, and split in two phases (31 October 2026)

| | |
|---|---|
| Item | ROADMAP work item "derivation time": the setup search (§8.6) made the background derivation of the large games take minutes |
| Fixture | `D:\Juegos\Lucas\indy4 vga en` (426 scene groups, the largest) |
| Tool | `../scene-setup-20261031/WindowCheck` (real `MainWindow`, private profile and cache folder; it logs when the derivation's room lists are done) |

## Changes

1. **`ScenePlanCache`** (App): each scene's fresh run and setup plan (`ScummCutscenePlanner.ExportScenes`/`ImportScenes`), saved per game folder and language with the same stamp as `CdRoomScenesCache` (the Core build and the game folder's files). A later launch imports it and derives only what is missing (`IsDerived`). The flag that allows the cache is read when the game opens (it is reset before the background work runs; reading it there made the first try load nothing).
2. **Two phases**: every scene's fresh run first, then the room lists are saved and the Play Cutscene buttons are ready; the setup plans follow in the background. A scene picked before its plan exists works it out on the spot.

## Results (Indy 4, real window)

| Launch | Room lists ready |
|---|---|
| First, before | 282 s |
| First, two phases | 58 s |
| Second (kept derivation) | 9 s |

`ScummSceneSetupTests.ADerivationKeptBetweenLaunchesNeedsNoNewRuns` (JSON round trip, no run needed after import). Full suite 1843 passed.
