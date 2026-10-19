# LOOM room cards: native actors over the existing first visit

| | |
|---|---|
| Scope | Bounded helper item within the user-authorized all-game room-card pipeline |
| Fixtures | `Games/loom ega mt32 en` and `Games/loom vga en`, external root `D:/Juegos/Lucas` |
| Date and author | 2026-10-19, Codex actor-composition worker |
| Baseline / commits | Parent baseline `c0823b04`; isolated merge `719a42f`; helper commit recorded by parent |
| Registry change | Unchanged: fixes thumbnail composition; no new engine/support claim |

## What was done

The baseline generic v1–v5 first-visit card path already calls `ComposeWithActors`. The two LOOM paths previously composed objects only, while their room views draw actors in a separate overlay. A new App partial provides these exact private static methods for the parent's loader:

```csharp
BitmapSource RenderLoomEgaRoomCard(GameLibrary game, GameResource resource,
    RoomImage room, byte[] bytes, ScummIndexedObjectSnapshot indexedSnapshot,
    IReadOnlyDictionary<int, byte> indexedStates, ScummSceneDrawBatches sceneBatches,
    CancellationToken cancellationToken)

BitmapSource RenderLoomCdRoomCard(GameLibrary game, int roomId,
    CancellationToken cancellationToken)
```

Both methods reuse the existing detached first-visit/session, object selection, source-supported artwork completion, camera crop and shadow palette. They overlay the native actor renderer's indexed layer with the same object/room depth masks, walk boxes, classes and extra depth planes. Coverage uses `IsDrawn`, preserving opaque VGA index 255 rather than treating that colour as transparency. Costume failures retain the existing safe reader behavior. Cancellation is checked before native work, costume reads and pixel merging. CD keeps its existing plain-background fallback when the first visit cannot run.

Only the new helper partial is production work here. Parent owns loader publication/cache behavior and the calls into these methods; this isolated proof does not claim the entire new pipeline is wired or window-accepted.

## Commands run (through RTK)

From the isolated `zakclassic-transform-palettes-20261019` worktree:

```powershell
rtk proxy dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet build .verification/room-card-actors-20261019/RoomCardActorProof.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/RoomCardActorProof/Debug/net10.0-windows/RoomCardActorProof.dll
```

The first harness build restored its own assets. Run output is retained locally as `proof-run.txt` via `rtk proxy powershell -NoProfile -Command` with `Tee-Object`. Four indexed native crops are in ignored `out/`; no source image is committed. Private `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR` are set before any MainWindow static method is invoked. The STA harness never constructs a window or starts an Application dispatcher.

## Results (numbers)

App and final harness builds: **0 warnings, 0 errors**. Four native 320×144 crops pass pixel-by-pixel against independently obtained first-visit state and native actor layers:

| Fixture / room | Native actor pixels in crop | Pixels changed from prior actorless card | Unchanged scenery pixels | Camera left |
|---|---:|---:|---:|---:|
| LOOM EGA 02 | 374 | 337 | 45,706 | 640 |
| LOOM EGA 04 | 323 | 305 | 45,757 | 320 |
| LOOM CD 02 | 52 | 49 | 46,028 | 379 |
| LOOM CD 04 | 335 | 335 | 45,745 | 320 |

Every actor-covered pixel matches the native renderer. Every uncovered pixel matches the existing object/background composition. All output palettes match the prior shadow-palette rules exactly, crops retain their native camera and dimensions, and returned bitmaps are frozen Indexed8. The EGA input index's complete state and owner dictionaries remain unchanged. A synthetic copy of the CD library lacking Room 04 scene metadata proves that first-visit refusal still returns the supplied archive's exact plain picture and palette. Both already-canceled helper calls throw cancellation rather than continuing.

These four samples contain zero opaque actor pixels with colour index 255; its preservation is enforced by using the renderer's separate coverage API, not established by a positive index-255 sample here. No broad/full test suite was run for this isolated item.

## Comparison with ScummVM (source files and date; runtime comparison if any)

No ScummVM executable was run and no new engine rule was ported. The helper composes existing source-backed `LoomExplorationSession`, `LoomCdRoomScene`, `RoomObjectSelection` and `ScummActorRenderer` output. The native actor renderer's existing `actor.cpp`/`costume.cpp` semantics, masks, classes and palette handling remain unchanged. This is Explorer/source integration evidence, not original-interpreter audiovisual acceptance.

## Differences and uncertainties kept

The actorless card was caused by two independent omissions: the LOOM thumbnail producers lacked actor composition, and the parent's baseline selected-room setter could overwrite any supplied thumbnail with the live background/object-only control. This item fixes only the producers; parent must remove the second overwrite and validate the full lifecycle.

Source previews retain all existing assumed arrivals, artwork estimates and script bounds. No prerequisite, actor position, costume, palette, inventory owner or story flag is supplied by this harness. The synthetic missing-metadata CD case is explicitly a fallback-boundary check, not a real alternate release or earned scene.

Initial build setup failures are retained in the tool record: the isolated App had no restored `project.assets.json`, so its first `--no-restore` build failed; the first harness build omitted `System.IO`, producing nine import errors. Restore and the missing import corrected them. The final proof run passes without source/pixel exceptions or weakened native expectations.

## User checks (exact steps and where; none if none)

Parent's all-game window worker owns actual-window acceptance and the exclusive UI lease. No window was opened in this item.

## Where the facts went (links to the script map / engine notes / tracker entries)

No new script or engine fact is introduced. Parent integrates the full pipeline evidence and its documentation; the source renderers and existing LOOM first-visit notes remain authoritative.
