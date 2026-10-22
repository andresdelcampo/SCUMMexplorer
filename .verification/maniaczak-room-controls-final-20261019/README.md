# Room controls and folder audio lifecycle — combined acceptance, 19 October 2026

Baseline master `b1f231c5`; integration `codex/room-controls-maniac-zak` in `.worktrees/maniaczak-status-polish`. [Checkpoint](../../docs/checkpoints/20261019-RoomControls-ManiacZak.md). Explicit user requirements are recorded separately in `906966e` before implementation.

## Source and ownership

- Room actions `a8f6e1df` (worker `95fa069`) and App-only follow-ups expose source-backed direct Interact, per-room cutscene lists from static previews and current Advanced scripts/relationships. Native sentence gameplay is unchanged. Interact requires native control in the live displayed room; it never reopens a static preview with entry-scene autoplay.
- Overlays `7c148e06` (worker `a81c1e0`) extract the existing generic doorway analysis while preserving the LOOM wrapper, add native viewport/mask/eligible-object helpers and nine meaningful tests. App-only follow-ups add Advanced doorway/hotspot lists, source-reader isolation and cached room context.
- Audio/status `1245c811` (worker `8c5b0d4`) plus App-only fixes stop/detach old sources before scanning a new folder, provide source-associated generic room sound menus and close Game Status after successful atomic Apply. Undo remains session-bound across reopening.
- Parent owns shared MainWindow hooks, actual native room-card reveal and correct letterbox input mapping. Core/tests freeze at `7c148e06`; remaining implementation/review fixes are App-only. No running Core/test binary is rebuilt during the full regression.

## Focused and window evidence

[Room actions](../maniaczak-room-actions-20261019/README.md), [overlays](../maniaczak-room-overlays-20261019/README.md), and [audio/status](../maniaczak-audio-status-20261019/README.md) retain exact source versions, commands, paths, preparations and results. Overlays: 29 focused cases pass (nine new and 20 existing LOOM rendering cases). Audio/status: 18 focused Core inventory-admin cases pass. Actual windows run serially under one exclusive lease and private profiles. Their final results are recorded when complete; prepared harnesses are not claimed as executed checks.

## Bounds

No support level changes. Scene lists remain derived, not a hand-validated catalog. Advanced retains source candidates and notes; Normal titles omit verb bytes and technical provenance. Direct Interact is an explicit exploration action; it cannot invent startup answers, force native readiness or bypass the retained conservative manual-room fallback. A static room can expose source inspection while actions remain unavailable until native live control exists.

Generic room source inspection exposes raw disassembly and source links. The existing readable presenter is LOOM-specific; reusing it would mislabel v1/v2 actor/variable semantics. No generic readable formatter is claimed. Sound association scans follow literal calls to depth four and source-bound arguments; computed/deeper calls remain unresolved, with bounds in Advanced. Reached native sound events supplement static candidates, and conditional associations are not confirmed soundtracks. Source-proved CD track ids are retained even when the archive has no ordinary sound resource row.

No ScummVM, user profile/game data changes, forced story state, room hacks, junctions or worktree cleanup. Audio cancellation checks do not establish listening/timing parity; optional user listening remains separate.

## Final assembly

```powershell
rtk git merge --no-edit master
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy powershell -NoProfile -Command '$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~SupportMatrixTests --verbosity minimal'
rtk proxy powershell -NoProfile -Command '$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"; $env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"; $env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"; dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore -p:NuGetAudit=false --logger "trx;LogFileName=full-suite.trx" --results-directory .verification/maniaczak-room-controls-final-20261019/test-results --verbosity minimal'
```

Initial assembled solution: zero warnings/errors, 7.38 seconds. Generated matrices: three passed, zero failed/skipped, 20 ms; levels unchanged. Full regression is running with all external roots configured. Final App/build, exact TRX counters and clean promotion follow after completion.
