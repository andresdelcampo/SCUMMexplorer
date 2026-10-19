# Completed room thumbnail cache

| | |
|---|---|
| Scope | All game formats; App cache only |
| Date / baseline | 19 October 2026 / `c0823b04` |
| Fixtures | Tiny synthetic WPF bitmaps and invented source metadata in a fresh private output directory; no game data or user profile |
| API | `SaveCompleted(gameFolder, thumbnails, language = null, cancellationToken = default)` returns `bool`; `TryLoad` signature retained |

## Contract

The parent supplies every completed final room thumbnail after the full native initialization pass succeeds. A nonempty batch is encoded into a unique staging ZIP in the cache directory on the same volume as its final destination. An exact manifest and source/build stamp are written; the source stamp and cancellation token are checked again immediately before atomic file replacement. Readers and the final commit synchronize in-process. Readers in another process retain an already-open old file or open the new complete file; there is no partially rewritten published archive.

`true` means the complete supplied batch was committed. `false` means cancellation, invalid input or cache/encoding/commit failure; a previously complete cache is retained. `TryLoad` accepts only an exact requested room set matching the manifest, correct version/stamp, every PNG and no extra archive entries. It returns fully loaded frozen bitmaps, or null. The save API does not independently discover the game's expected room list: the parent's all-rooms completion check is the completeness authority. The unchanged load API rejects a mismatched/incomplete supplied game set when the full room list is requested.

WIC PNG codecs require seekable streams, so encoding and decoding use one small per-thumbnail MemoryStream rather than a ZIP entry stream directly. Staging and final cache names include the new v3 version, canonical folder/disc source path and selected language. Old incremental v2 directories are not read or deleted. App/Core module build IDs, binary sizes/timestamps and recursive source file names/sizes/timestamps invalidate the cache. This covers old LFL layouts, LEC archives, v5 data and nested language directories; cue inputs use the shared disc resolver and include the selected image's metadata even when it is outside the cue's folder. Metadata is intentionally conservative and may invalidate on unrelated files in the same source tree. It does not hash multi-gigabyte data contents; the shared cue resolver reads only the image header to identify its sector layout.

No source reparse point is followed: a source tree containing one is refused as a cache source, so a target change cannot leave a falsely valid stamp. A custom cache directory inside a source tree is excluded from its own metadata. Cleanup removes only the exact staging file successfully created by that call; no recursive directory delete, game mutation or profile reset occurs. Private cache failures remain optional optimization failures and do not change native session behavior.

## Reproduce

From `.worktrees/zakclassic-palette-core-20261019`, through RTK:

```powershell
rtk proxy dotnet build .verification/room-preview-cache-20261019/RoomPreviewCacheCheck.csproj --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy dotnet run --project .verification/room-preview-cache-20261019/RoomPreviewCacheCheck.csproj --no-build -- .verification/room-preview-cache-20261019/run-private-frozen
rtk proxy dotnet run --project .verification/room-preview-cache-20261019/RoomPreviewCacheCheck.csproj --no-build -- .verification/room-preview-cache-20261019/run-private-frozen read
```

Use a fresh output path for the first run. The harness refuses to remove an existing directory. It sets `SCUMM_ROOM_CACHE_DIR` before the first cache access, runs STA without creating a WPF window, and links the production cache source directly rather than requiring the parent's concurrent MainWindow edits.

The check covers completed roundtrip/frozen pixels, exact complete ID sets, empty/null bitmap rejection, cancellation before work and after staging begins, failed replacement of a locked destination preserving the prior valid archive byte for byte, missing PNG/stale stamp refusal, folder/language isolation, concurrent readers seeing only complete old/new batches, old LFL/LEC/v5/nested-language metadata and cue/bin companion invalidation. A second process checks disk persistence. Results of the final run are appended below.

Final focused build: **zero warnings/errors**, 0.96 seconds. Frozen headless STA run: **32 checks passed**, process exit 0, 1.28 seconds; this includes the locked-destination and concurrent-reader checks plus external cue-image invalidation. Separate headless STA persistence process: **passed**, exit 0, 1.06 seconds. The 32 checks include successful setup refreshes and are not claimed as 32 independent unit tests. All output stays in ignored fresh private directories. No actual WPF window was created and no game folder, save, ROM or user profile was used.

## Retained failures and limits

The first run refused publication because WIC could not seek a ZIP entry stream. A second diagnostic confirmed that exact exception; the per-image MemoryStream correction then passed 26 checks. Adding replacement-failure and concurrent-reader coverage passed 29 checks. These were cache setup failures with private synthetic data; no partial batch reached the published cache and no real data was used. Local ignored `run-private-1`/`run-private-2` outputs and harness failure logs retain those failures.

This item changes no room background/native-init scheduling, scene cache, game reader, support level or matrix. Parent owns the all-game UI orchestration, actual window, shared requirements/notes, full suite and final promotion. The parent will publish backgrounds first, can apply a valid cache after that pass, still precompute native first visits, and save only after full successful completion.
