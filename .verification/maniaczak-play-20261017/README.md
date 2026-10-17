# Maniac/Zak S6 integration and real-window checks

| Field | Value |
|---|---|
| Families / stage | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / S6 |
| Date / author | 17 October 2026 / Maniac-Zak orchestrator |
| Fixtures | `D:/Juegos/Lucas/mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` |
| Registry | Objects and Play Full for all four bounded native routes; other cells unchanged |

## What was done

The first Play start retains the old engine's actual opening/menu. It never reuses the browsing preview's assumed party selection or visits a selected catalog room to bypass pending player input. Subsequent explicit room selection keeps the existing state-preview behavior.

Proposed requirements R-7.19/R-7.20 specify the native old sentence/inventory/input interface and player startup choices (`f0c34f0`); R-7.21 specifies a source-established native choice where the early game has no later dialogue menu (`a00790f`). These are proposals, not user-confirmed decisions. Existing click, route, inventory and cutscene-skip checks remain required.

## Commands and results

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/maniaczak-s6`.

```powershell
rtk proxy dotnet build .verification/maniaczak-play-20261017/PlayWindowCheck/PlayWindowCheck.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
```

App/harness compilation: zero warnings and errors. Built application: `.worktrees/maniaczak-s6/Exe/Debug/SCUMMexplorer.exe`. The route harness is `Exe/PlayWindowCheck/Debug/net10.0-windows/PlayWindowCheck.exe`.

All four real-window routes pass; screenshots were inspected. The reproducible action files live beside `PlayWindowCheck/Program.cs`.

| Family | Local capture/log directory | Final window frame | Inputs (plus Escape) | Verified finish |
|---|---|---:|---:|---|
| ManiacClassic | `window-classic-final` | 3310 | 15 | Room 1, key held and selected from displayed inventory |
| ManiacEnhanced | `window-enhanced-final` | 1554 | 13 | Room 1, Spanish sentence and key inventory selection |
| ZakClassic | `window-zakclassic-final` | 2911 | 32 | Room 7, Buy refusal completed, player control and phone bill inventory restored |
| ZakEnhanced | `window-zakenhanced-final` | 2911 | 32 | Same native route and return of control, independently checked |

The Classic Maniac window route uses longer fixed waits than its 1993-frame headless route. Window frame totals include the initial timer frames. All four routes check the composed sentence, pickup, native choice, walked exit and override skip. No physical mouse automation or original-interpreter observation is claimed.

## Real-window method

`PlayWindowCheck <fixture> <output> <selected room> <actions.json>` opens the actual MainWindow with a private `SCUMM_EXPLORER_PROFILE_ROOT`. It selects a room through the tree, checks that the window's timer advances, then drives that same timer handler deterministically. Clicks use the same session API as the image mouse handler after coordinate mapping; text and Esc use the window's own handlers. It checks the Normal-mode note and tooltip, cursor, route assertions and captures, and exits nonzero on failure. It never writes story variables or invokes object verbs directly. This is a window-integration check, not a check of physical mouse mapping or perceived timing. Logs and PNG captures stay local and ignored.

## ScummVM comparison

No ScummVM executable is launched. Engine behavior is compared to source, and each family's route record describes the native path with script/instruction evidence. Original-game picture and timing comparison remain open; app captures are not original-game observations.

## Differences and uncertainties

S5 black-region, camera, lighting and actor-alignment findings remain in the S5 checkpoint until specific evidence closes them. A working bounded route is not a full-game completion claim. Each family record must disclose untested branches and remaining blockers.

Failed evidence kept: the initial native walking implementation changed both Maniac Room 2 first-visit tests from Settled to FrameLimit (21 passed, 2 failed in the 23-test S5 selection). Native direction tables fixed the stationary turning loop; the settled-state assertions were not weakened. The first window attempt also exposed a harness-only missing RoutedEvent on synthetic Escape; the corrected event passed. Zak's shop cleanup exposed the later-engine player-class predicate incorrectly allowing an NPC-only box; the native ego/range predicate fixed both editions. Inventory scrolling resets after actor/ownership changes, following independent review. The closest-point calculation still uses the generic polygon metric in native coordinate units; untested walking paths remain a limitation.

## User checks

Follow the action files/family records in the app and check physical mouse feel, perceived timing, text pacing and PC-speaker/PCjr synchronization. These are manual checks, not an approval gate inside this run.

## Where facts went

[S6 checkpoint](../../docs/checkpoints/20261017-S6-ManiacZak.md), [Maniac tracker](../../docs/maniac/SUPPORT_STATUS.md), [Zak tracker](../../docs/zak/SUPPORT_STATUS.md), [requirements](../../docs/REQUIREMENTS.md).
