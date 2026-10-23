# Room controls and folder audio lifecycle — combined acceptance, 19–23 October 2026

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

Initial assembled solution: zero warnings/errors, 7.38 seconds. Generated matrices: three passed, zero failed/skipped, 20 ms; levels unchanged. The saved 19 October TRX completed with **1,701 passed, zero failed, five expected skips**, 8m07s; all nine new overlay cases Passed. It is retained locally as `test-results/full-suite-premerge.trx`.

Resumed 22 October: merged concurrent master `ff65ac0b` into `daf75cd6`, preserving all sound/speech/globals/play-start work. New solution build zero warnings/errors, 12.98 seconds; regenerated matrices three passed, zero failed/skipped, 21 ms. Repeated the full command above with logger filename **full-suite-20261022.trx**: **1,739 passed, zero failed, five expected skips**, 7m55s. Total 1,744; all nine overlay tests Passed. Core/tests freeze is now `daf75cd6`; later theme, UI row ownership, launch guards and audio-render lifetime corrections are App-only, built with BuildProjectReferences=false while the regression ran.

Final accepted product source is `bde49369`; all three workers verify no production-source difference. Final solution build: zero warnings/errors, 7.52 seconds; regenerated matrix checks: three passed, zero failed/skipped, 21 ms. Core/tests remain identical to `daf75cd6`, so the required 1,739-pass full regression applies without relabeling older windows.

Final 23 October actual windows:

- Actions: MM Classic 24.83s, MM Enhanced 53.67s, Zak Classic 49.88s, Zak Enhanced 49.00s; all pass. Actual scene rows, Normal/Advanced menus, direct native TV/drawer effects with no sentence input, and raw Advanced source/links. Evidence `4ef117ea` retains the failed preparations as well as final runs.
- Overlays: MM Classic 7.53s, MM Enhanced 7.67s, Zak Classic 9.17s, Zak Enhanced 9.25s; all pass. Four inspected final captures confirm native HOTSPOTS/DOORWAYS survive toggles. Evidence `01ceacc2` supplements the earlier native walked-room/selector routes; those earlier captures retain their actual source scope.
- Audio/status: all four pass in 32.82s, including one-click Apply/close/Undo, rejection, Normal Sounds/device changes and a canceled old render completing after replacement-session attachment. Native LOOM Sound 13 → Maniac folder retirement passes in 3.13s. Evidence `57ddc388` and `f9013ef3`; all 14 final captures inspected. No listening claim.

All owned windows and processes are closed, and the exclusive WPF lease is released. The exact unanswered Maniac startup-menu-to-scene window route remains unproved; its narrow existing R-8.34 preparation is retained. Two telescope preparations prove dispatch only, not the guarded scene outcome. Generic readable formatting remains unavailable; raw inspection is accepted. No support levels are promoted.

## Promotion — 23 October

Final integration merge found master unchanged at `ff65ac0b`; master fast-forwarded cleanly to `79b481b8`. Normal executable build on master succeeded with zero warnings/errors in 6.77s (`Exe/Debug/SCUMMexplorer.exe`). The final documentation-only record is committed and fast-forwarded separately. No product changes follow the accepted `bde49369` source; no backend changes follow `daf75cd6`.
