# Native room overlays, Maniac/Zak — 19 October 2026

Request: make the existing hotspot, doorway, depth-area and walkbox viewing aids work on the actual v1/v2 native play screen, following LOOM's rules.

## Implementation

The separate native screen hid the old room surface and all its overlays. `MainWindow.PlayOverlays` adds one non-hit-testing Canvas over that screen. Current `MainScreen.Top/Bottom` and camera `ViewLeft` define its projection and clip. The native room/verb boundary is respected; overlays never change runtime state or receive clicks.

`ScummRoomOverlays` uses `ScummRoomScene.IsHotspot`, including live ownership, untouchability, v1/v2 state bit 2, positions and parent requirements. Source-derived doorway objects have only doorway outlines, controlled separately by Never / On hover / Always. Object outlines use the same three choices. Depth tint uses the current selection's composed mask (the same mask used by actors), including additional native planes. Walkbox polygons use current runtime flags, with disabled boxes dashed.

`ScummDoorwayFinder` extracts the existing generic algorithm from `LoomDoorwayFinder`; the latter remains a compatible wrapper. Static destinations remain possible destinations, never promised native outcomes. Backgrounds and object resources are cached by `ScummPlayScreen`; source doorway scanning occurs only on game/room change, and mutable scene captures are obtained only while an aid is active or the room changes.

## Focused evidence

Working directory: `.worktrees/zakclassic-transform-palettes-20261019`, baseline `b1f231c5`.

```powershell
rtk proxy dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --filter "FullyQualifiedName~ScummRoomOverlayTests|FullyQualifiedName~LoomFirstVisitRenderingTests" --logger "trx;LogFileName=overlays.trx" --results-directory .verification/maniaczak-room-overlays-20261019/test-results --verbosity minimal
```

App build: zero warnings/errors. Focused suite: **29 passed, zero failed/skipped**, 23 seconds. Nine new cases cover native screen/camera projection, all four supplied first-room object/depth/walkbox/doorway datasets and live eligibility mutations; the other twenty are existing LOOM first-visit/doorway rendering checks.

Fixtures are `D:\Juegos\Lucas\mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en`. Maniac first-visit tests explicitly use the existing fingerprinted party-input preview; those tests are exploration preparation, not a native earned party route. Each native depth-mask pixel in the visible 320×128 room band is compared with the tint, and the verb/text bands remain transparent. Synthetic eligibility changes exist only in copied test snapshots, never in product runtime.

## Actual-window acceptance, 22 October 2026

Product source is assembled parent `daf75cd6`, preserving concurrent master `ff65ac0b`; worker merge `6f88fe73` adds only the verification harness to that source. `git diff --quiet daf75cd6 -- src tests` exits zero. No product/Core/test changes were needed during these checks. The harness build succeeded with zero warnings/errors in 11.70 seconds; its later harness-only transition observation correction rebuilt in 1.58 seconds.

From this record's working directory:

```powershell
rtk proxy dotnet build .verification/maniaczak-room-overlays-20261019/RoomOverlaysCheck.csproj --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy powershell -NoProfile -Command '& "./Exe/RoomOverlaysCheck/Debug/net10.0-windows/RoomOverlaysCheck.exe" "D:/Juegos/Lucas/mmansion ega en" ".verification/maniaczak-room-overlays-20261019/window-20261022-maniacclassic"; exit $LASTEXITCODE'
```

Repeat that executable for the four fixture/output rows below. It creates an isolated profile and room cache beneath each output directory before constructing the real `MainWindow`. All windows ran serially under one exclusive parent-granted lease. Native clicks/keys call the existing session input API and frames call the actual window's `PlayTimer_Tick`; no actor, flag, object state, input script or placement is assigned by the harness. The room-card background job is canceled after opening to isolate native overlays; this is not thumbnail-cache acceptance.

| Family / fixture | Output directory suffix | Result | Native observation |
|---|---|---|---|
| ManiacClassic / `mmansion ega en` | `window-20261022-maniacclassic` | Pass, 13.21 s | Explicit intro, three native party clicks, native Skip at its reached override, walked Room 44 to 1; camera 352. |
| ManiacEnhanced / `mmansionE ega es` | `window-20261022-maniacenhanced` | Pass, 11.39 s | The edition's native party/intro and walked Room 44 to 1; camera 336. |
| ZakClassic / `zak ega en` | `window-20261022-zakclassic-transition` | Pass, 17.18 s | Explicit native intro reaches Bedroom; native Open/Walk reaches Room 2, then delayed Script 126 reaches Room 16 with its override. |
| ZakEnhanced / `zakE ega en` | `window-20261022-zakenhanced` | Pass, 15.60 s | Same source input sequence, observing the edition's native Room 2 and then Room 16. |

Every successful run checks:

- Opening the folder alone starts no play timer. Explicit intro uses native startup; Maniac's party is selected by three real source input calls.
- Current native room metadata and selected catalog resource follow the route. In Normal mode each selected room card intersects the room selector's visible viewport, including Zak's later Room 16. Entry playback is not replaced by catalog preparation.
- Object hotspots and doorways independently honor Never, Hover and Always; doorway objects are excluded from hotspot candidates. The real radio controls and actual native Canvas are exercised. Hover coordinates are delivered to the existing `UpdatePlayOverlayHover` helper, not by moving the OS pointer; this is helper and rendered-window evidence, not an OS mouse-motion claim.
- The Canvas does not receive hit tests. Advanced depth tint contains native mask pixels; all current walkbox polygons are present. Both Maniacs have 10 walkboxes and both Zaks have 4. Native hotspot shape counts are 7/7/16/15; source doorway-object counts are 3/3/2/2 respectively (not all targets need be visible in the current camera).
- Five pure `PlayPointAt` cases per run cover 640x400, vertical letterboxing and horizontal pillarboxing, including rejection outside the image. Overlay option changes leave the compared native object states, actor records and session-trace count unchanged.

### Retained failed first approach

`window-20261022-zakclassic/result.txt` and `failed-window.png` retain the first run's **16.74-second failure**. All overlay assertions passed at 14.19 seconds. The old harness then waited a fixed 240 frames after the doorway click and asserted Room 2, but the game's delayed Script 126 had already moved to Room 16. The captured real window correctly selected Room 16 and displayed that native cutscene. This was a test observation error, not a product regression. The second distinct approach stops frame by frame at actual Room 2, records that selector, and separately waits for actual Room 16/override. Both assertions pass. No game timer or entry state was altered to make the test pass.

### Captures and closure

Twenty PNGs (about 1.7 MB total) remain locally across five batches: 3 Classic Maniac, 3 Enhanced Maniac, 4 first ZakClassic (including failure), 5 corrected ZakClassic, and 5 ZakEnhanced. Seven were inspected: Classic Maniac Normal hotspots and Advanced depth/walkboxes; Enhanced Maniac Normal doorways; corrected ZakClassic Advanced depth/walkboxes and Room 16; ZakEnhanced Normal hotspots and Room 2. The initial ZakClassic failed-window capture was also inspected, for **eight inspected captures total**. Those eight small images and all five text reports are retained in Git; the other generated images remain local.

Inspection found a low-contrast black Interact expander heading on the dark Normal background. This is reported to the parent owning shared XAML; these overlay proofs predate its separate contrast correction and do not claim visual acceptance for that later fix.

Every harness window closed in `finally`, stopping its own timer/playback and canceling its card job. Final `Get-Process RoomOverlaysCheck` inventory is empty. Exclusive WPF lease released to parent. No ScummVM runtime, game data, saves, user profile or user application was changed.

## Limits

No support cell changes: viewing aids do not resolve prior original-game visual acceptance, palette estimates, other manual-room availability or unproved routes. Native input was used, but this is not an OS end-to-end mouse-input or original-game comparison. Parent owns shared XAML/code-behind, required full regression, support matrix generation and final integration/checkpoint.
