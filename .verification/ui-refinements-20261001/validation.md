# Requested UI refinements, 1 October 2026

## Control placement

Game Status is in the top right immediately before global Play Cutscene. Status remains available in normal/Advanced LOOM; the global picker is Advanced-only. Room Play Cutscene follows Skip line in the playback bar, retaining generated zero/one/multiple memberships and existing fresh-session, busy/pause/departure behavior. Bars wrap within the available width; the room button reserves a stable minimum width for its picker marker.

Fresh normal-clock WPF `RefinementCheck`: **11/11** in ignored `Exe/UiRefinementSession-20261001/placement-1/refinement-check.txt`. Normal, Advanced and minimum900x600 screenshots inspected. Build zero warnings/errors. Synthetic handlers/layout, not physical input or independent listening. No Core/session/catalog changes or user processes stopped.

## Shared original scene names

One `LoomReplayTitle.Format` supplies the existing `Room NN · descriptor` convention to original and generated starts. Original labels are Room01 Opening, Room08 The Elders' trial, Room10 Walk to Room13 and Room36 The firewood scene. Global/room pickers, legacy menu and Game Texts use these shared titles. Generated titles, IDs, start actions and all334 memberships are unchanged; no catalog regeneration.

Fresh `naming-1/refinement-check.txt`: **15/15**, including all334 formatted titles and original Game Texts parity. The actual stopped boot is in69 and frame advancement first reaches01: the opening's scene label is01 while its validated setup/membership retains69. This check distinguishes resource preloads in044 from actual scene-room changes. Build zero warnings/errors.

Reproduce from the main checkout:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/ui-refinements-20261001/run-ui.ps1 -Output Exe/UiRefinementSession-20261001/placement-fresh
```
