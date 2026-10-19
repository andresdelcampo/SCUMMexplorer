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

## Limits and remaining evidence

Parent integrates the additive MainWindow hooks and owns shared XAML/code-behind. Actual-window evidence will follow integration under one exclusive window lease. No support cell changes: viewing aids do not resolve prior original-game visual acceptance, palette estimates or unproved routes. No ScummVM runtime, game data, saves, user profile or user application was changed.
