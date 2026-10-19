# Actual-window room preview checks — 19 October 2026

This packet verifies the background-first room selector with read-only supplied game data. Product source is parent integration `4348e10`, merged into this isolated worktree as `0523aac786bd411f66e9da045dc0fb11eb3d52aa`. Only this packet's harness/evidence files are owned here.

## Scope and method

The census invokes App's pure `RenderRoomCardBackground` for **every selector scene of all 14 available families**, then independently expands the direct Core decoder's background palette indices and compares dimensions and every BGRA pixel (SHA-256 includes dimensions). It does not create a session or MainWindow. LOOM resource-bank rooms are excluded exactly as the selector's `game.Scenes` excludes them.

The actual-window cases instantiate the real App/MainWindow, with private `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR`, and mute the scene sound player. They preserve the complete original GameLibrary and resources. To avoid repeating every family's full native census, the harness calls actual `ShowGame`, cancels/awaits its automatic card loader, retains the selected original room buttons, and invokes the actual private loader with the bounded room list. This tests real loader/publication/cache/selection paths; it is **not a full normal Open Game Folder/native-room census**. No source flags, actors, owners, palette state, story variables, saves or game files are assigned. No ScummVM or OS input is used.

Assertions cover:

- Every plain background is published before the loader's first native card. Intermediate cold ZIP cache must be absent; committed cache must match all final card pixels.
- Dispatcher heartbeat during decoding/native work, including an actual early card click, with a maximum 3-second gap assertion.
- Actual late room selection and a deliberately late Background-stage publication cannot replace the InitialState card. Generic cards contain actors/objects and differ from plain backgrounds in all 13 sampled generic rooms.
- Warm reopening uses a new complete GameLibrary. All requested native `RoomPreviewScenes` lazies must be absent before loading and created afterward; exact warm card/cache pixels are preserved. LOOM additionally rebuilds its draw batches before completion.
- ZakClassic's real Open/drawer92 verb/object/sentence input changes live object state while the selector remains fixed. A partial ZakEnhanced native pass is then canceled before switching to ManiacEnhanced Room1; old cards/cache cannot leak across the shared room ID.

The initial two Zak captures show the combo's old AdLib label while the harness had already muted the actual scene player. Subsequent harness code also synchronizes the selector to No Sound and asserts actual mode None. This is a harness display correction, not a product/source change.

## Commands

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer\.worktrees\zakclassic-mars-palettes-20261019`.

```powershell
rtk git merge 4348e10
rtk proxy dotnet build .verification/room-previews-20261019/WindowCheck/RoomPreviewsWindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy .\Exe\RoomPreviewsWindowCheck\Debug\net10.0-windows\RoomPreviewsWindowCheck.exe census 'D:\Juegos\Lucas' '.verification\room-previews-20261019\census-all-14'
rtk proxy .\Exe\RoomPreviewsWindowCheck\Debug\net10.0-windows\RoomPreviewsWindowCheck.exe batch 'D:\Juegos\Lucas\zak ega en' '.verification\room-previews-20261019\window-zak-classic-attempt1' '1,5,7' actions
```

The other seven commands use the same executable and `batch <fixture> <output> <rooms>` with the exact fixture/output/room rows below. All processes run serially under the parent's exclusive WPF lease, with a process inventory before the first launch and after completion. No other/user app is stopped. Build against frozen source: **0 warnings, 0 errors, 7.63 seconds**; later harness-only rebuilds also green.

## Results

Complete plain-background census: **PASS — 14 families, 1,002 exact backgrounds**, about 15 seconds between first/final progress records.

| Family | Supplied folder | Selector backgrounds |
|---|---|---:|
| LoomEgaReference | loom ega mt32 en | 72 |
| LoomEga | loom ega es | 72 |
| LoomEgaDemo | loom demo short ega en | 14 |
| LoomCd | loom vga en | 78 |
| Indy3Ega | indy3 ega en | 88 |
| Indy3Vga | indy3 vga en | 87 |
| MonkeyFloppy | monkey1 ega en | 81 |
| MonkeyCd | monkey1 vga en | 83 |
| Monkey2 | mi2 talkie en | 110 |
| Indy4 | indy4 vga en | 96 |
| ManiacEnhanced | mmansionE ega es | 53 |
| ManiacClassic | mmansion ega en | 52 |
| ZakEnhanced | zakE ega en | 58 |
| ZakClassic | zak ega en | 58 |

Actual-window cases sample **15 rooms in eight families**, each with cold and warm passes. Cold measurements include the early click and capture overhead; they are observations on this machine, not performance guarantees.

| Family / fixture | Rooms | Ignored output packet | Cold seconds | Heartbeat ticks / max gap | Result |
|---|---|---|---:|---:|---|
| ZakClassic / zak ega en | 1,5,7 | window-zak-classic-attempt1 | 12.96 | 770 / 78 ms | PASS; native drawer and canceled-game switch also pass |
| ZakEnhanced / zakE ega en | 1,5 | window-zak-enhanced | 9.22 | 556 / 79 ms | PASS |
| ManiacClassic / mmansion ega en | 1 | window-maniac-classic | 4.32 | 264 / 53 ms | PASS |
| ManiacEnhanced / mmansionE ega es | 1 | window-maniac-enhanced | 9.78 | 609 / 66 ms | PASS |
| Indy3Ega / indy3 ega en | 1,22 | window-indy3-ega | 8.05 | 491 / 93 ms | PASS |
| MonkeyFloppy / monkey1 ega en | 33,37 | window-monkey-floppy | 3.63 | 212 / 68 ms | PASS |
| MonkeyCd / monkey1 vga en | 33,37 | window-monkey-cd | 2.85 | 167 / 80 ms | PASS |
| LoomEgaReference / loom ega mt32 en | 56,58 | window-loom-ega-diagnostic2 | 5.79 | 179 / 2,728 ms | PASS assertion; synchronous early-click stall retained below |

The eight accepted packets contain **26 captures**; two additional captures are retained with the first failed LOOM attempt (**28 total**). Inspected ZakClassic initialized, ManiacClassic actor-bearing initialized, MonkeyCd initialized, and LOOM plain/initialized captures. LOOM's plain card holes are filled by the native phase, including Room56 face/mouth and Room58 image; initialized cards persist through selection/warm reopening.

## Retained failures and limits

Initial scaffolding compilation found an inaccessible internal `LoomReplayCatalog` and a `game.Scenes.Count` method-group expression. Harness-only fixes use reflection for the real catalog loader and `Count()`; no product source was edited.

`window-loom-ega` retains a genuine first-run failure of the original maximum-3-second heartbeat assertion, including failed capture and stack. A second bounded diagnostic attempt retained every assertion and logged the actual early click: **2,679 ms synchronous dispatch while loader phase remained Backgrounds**, maximum heartbeat gap **2,728 ms**. It passed the numerical bound on that attempt, but demonstrates an existing synchronous room-selection wait. The likely source is `EnsureSceneBatches().GetAwaiter().GetResult()` on the selection path; this is a diagnosis, not a timing guarantee or a uniformly responsive LOOM acceptance claim.

The second LOOM initialized screenshot also showed a transient unfilled main Room56 image while the selector card was correctly filled. Main-room pixel equivalence is outside this selector packet's assertions; the observation and captures were sent to the parent for source review. The selector's exact background/native/cache/stability assertions remain independent of that main-room observation.

All supplied families have exact plain-background coverage. Full native/click/cache checks are limited to the listed representative batches; untested full-family native runs, other editions' full scans, normal file-dialog startup, and timing under other hardware/load remain unclaimed. The parent owns product changes, broad tests, shared documentation and master promotion.

## Final async room-opening acceptance — 19 October 2026

Parent App fix **`4421f0d`**, merged here as `e806344529ba224d608689cbf955a931fd189c50`, awaits LOOM draw-batch analysis before room setup and removes synchronous `EnsureSceneBatches` waits. Core and tests were unchanged. The same harness/assertions were rebuilt against this source: **0 warnings, 0 errors, 11.80 seconds**. Only read-only LOOM live-session diagnostics were added. The preceding failure and diagnostic packets remain untouched.

After an empty verification-window process inventory, the parent granted a fresh exclusive lease. These two actual-window processes ran serially, with separate private profile/cache directories and actual sound mode None:

```powershell
rtk git merge 4421f0d
rtk proxy dotnet build .verification/room-previews-20261019/WindowCheck/RoomPreviewsWindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy .\Exe\RoomPreviewsWindowCheck\Debug\net10.0-windows\RoomPreviewsWindowCheck.exe batch 'D:\Juegos\Lucas\loom ega mt32 en' '.verification\room-previews-20261019\window-loom-ega-async-final' '56,58'
rtk proxy .\Exe\RoomPreviewsWindowCheck\Debug\net10.0-windows\RoomPreviewsWindowCheck.exe batch 'D:\Juegos\Lucas\zak ega en' '.verification\room-previews-20261019\window-zak-classic-async-final' '1,5,7' actions
```

Final LOOM: **PASS**, cold **4.39 seconds**, **137 heartbeat ticks**, maximum gap **2,065 ms**; the early click returns in **18 ms**, versus the retained preceding 2,679 ms synchronous dispatch. Background ordering, exact native/cache pixels, no stage downgrade, and warm completion assertions are unchanged. Three new captures are retained; the initialized capture was inspected and both the selected Room56 and canonical selector now show the filled face/mouth. The read-only current exploration snapshot at that capture was `SessionModeToggle=True`, Room56, `IsFaithful=False`, `PendingScripts=2`, object780/783 states both0. This records the live session's state without claiming equivalence to detached canonical first-visit state or assigning either to match. Remaining heartbeat cost includes real room setup; the measurement is not a universal latency guarantee.

Final ZakClassic: **PASS**; early click **15 ms**, **1,068 heartbeat ticks**, maximum gap **82 ms**, cold **18.60 seconds**. Its actual native drawer action preserves the initialized card; new-library warm native prewarm and partial ZakEnhanced cancellation/switch to ManiacEnhanced both pass unchanged assertions. Five new captures are retained, and the final replacement-game isolation capture was inspected. Together the two final packets add **8 captures** (**36 total**, including the original failures), with **7 captures inspected** across the complete packet. A final process inventory was empty and the exclusive WPF lease was released. No all-family census or other native family batch was repeated for this App-only fix.
