# Room audit

Renders every LOOM room's first-visit state the way the exploration view does: a fresh `LoomExplorationSession` visits the room, `RoomObjectSelection` is built from the snapshot (states, positions, room records, background, index states, draw queue), and when the snapshot is not faithful `FillGaps` runs with the room's `LoomSceneDrawBatches` and seed = room number, followed by `ShowUnresolvedArtwork` (same batches and seed) when nothing is visible. It then reports the colour-0 gaps that remain (`RoomObjectSelection.VisibleGaps`, the same test `FillGaps` uses) and the doorways `LoomDoorwayFinder` returns for each room's object verbs.

It uses Core only; no WPF window is opened. `run-verify.cmd` builds and runs it automatically.

```powershell
dotnet build .verification/RoomAudit/RoomAudit.csproj
dotnet Exe/RoomAudit/Debug/net10.0-windows/RoomAudit.dll "$PWD\Games\loom" "$PWD\.verification\room-audit" [room numbers for image/batch detail]
```

Output: `room-audit.txt` (per room: paint order, draw queue, estimates with their source, fallback picks with their source, remaining gaps, doorways), `room-audit.tsv` (one summary row per room) and `room-NN.bmp` renders.
