# Maniac/Zak S5: shared UI and integration checks

| | |
|---|---|
| Family / stage | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / S5 |
| Fixture and edition | `D:/Juegos/Lucas/mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` |
| Date and author | 2026-10-17, Maniac/Zak orchestrator |
| Commits | `32bfd04`, `2962084` (UI); family and shared engine commits in the checkpoint |
| Registry change | unchanged: Objects remains Partial until S6 responses; this record does not claim original-game comparison |

## What was done

The room view and room cards share the prepared first-visit scene. The Maniac provider uses fingerprinted, labelled Syd/Bernard party clicks, separately from the generic engine. Old-format hotspot labels now use the session's object eligibility and reached positions, not initial index visibility. A failed first visit shows the plain background, with its explanation only in Advanced.

The isolated WindowCheck harness opens the actual MainWindow, checks that its displayed object selection matches the session, verifies hotspot membership and positions, rejects technical object notes in Normal mode, and saves rendered captures. It creates a private profile under each output folder and closes its own window. The user's existing profile and applications are untouched.

## Commands run (through RTK)

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer\.worktrees\maniaczak-s5`.

```powershell
rtk proxy dotnet build SCUMMexplorer.slnx --ignore-failed-sources -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet build .verification/maniaczak-objects-20261017/WindowCheck/WindowCheck.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\mmansionE ega es" ".verification/maniaczak-objects-20261017/window-maniac-enhanced" 44 1 18
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\mmansion ega en" ".verification/maniaczak-objects-20261017/window-maniac-classic" 44 1 18
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\zakE ega en" ".verification/maniaczak-objects-20261017/window-zak-enhanced" 1 5 7 9 23
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\zak ega en" ".verification/maniaczak-objects-20261017/window-zak-classic" 1 5 7 9 23
```

## Results (numbers)

Initial solution and harness builds: zero warnings and errors. Initial ManiacEnhanced real-window check passed all three rooms (44, 1, 18). Room 44's capture exposed a head-only Syd and all three scenes initially reached the frame limit; those findings were sent to the shared owner rather than treated as visual acceptance. Final results follow integration of the corrections.

At source head `6e3287f`, **all 16 final real-window room checks passed**: three rooms per Maniac edition and five per Zak edition as listed above. Each scene settled. The classic Maniac Room 18 detector still reports its six native black coin-slot rectangles; the other window samples report zero gaps. Final Room 44 captures show all three party members with full bodies in both Maniac editions; the classic Zak bedroom and enhanced Zak pseudo-room 23 captures also show their actors. Initial enhanced captures remain locally in `window-maniac-enhanced-initial/`. Captures and per-window `result.txt` files are local ignored artifacts, not original-game comparisons.

Built app: `D:\Documents\Personal\Code Projects\SCUMMexplorer\.worktrees\maniaczak-s5\Exe\Debug\SCUMMexplorer.exe`. Harness: `Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll`.

Final review corrected queued duplicate-image redraws: retain earlier directory entries and queue only the last matching image. All 38 focused old-rule/selection tests passed. Read-only inspection of both Zak Room 19 scenes found DrawQueue `[381]`, PaintOrder `[381]`: duplicate 384 is neither active nor queued, so this correction does not change the family audit results. Across both Zak editions, the only duplicate records are Room 19 object 384, local indices 10/11, both with no parent. Differing parent conditions on duplicate IDs remain a general unverified case, not a finding in these fixtures.

Final integration build: zero warnings and errors. Matrix tests: 3 passed, no cell changes. Full-suite command (same working directory; all three external fixture variables set):

```powershell
$env:SCUMM_SOUND_TEST_ROOT = 'D:\Juegos\Lucas\loom ega mt32 en'
$env:SCUMM_INDY3_EGA_TEST_ROOT = 'D:\Juegos\Lucas\indy3 ega en'
$env:SCUMM_MT32_ROM_ROOT = 'D:\Juegos\Lucas\mt32'
$env:SCUMM_UPDATE_SUPPORT_MATRIX = '1'
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
```

Full suite: **1150 passed, 0 failed, 5 skipped, 1155 total**, 6 m 3 s. The five skips are existing diagnostic dumps, not verified behavior. Local log: `full-suite.txt`. This includes all family commits through `a7e16e9` and the final queued-duplicate correction. Master base `9bc3ec8` was merged before the final build/suite; matrix regeneration changed no support cells. No ScummVM process was launched.

## Comparison with ScummVM (source files and date; runtime comparison if any)

No ScummVM executable was run. Family evidence records carry source comparisons. The captured window is this app, not the original game.

## Differences and uncertainties kept

Catalog visits are labelled state previews from boot, not played routes. Maniac's party is an explicit input assumption. Unsupported recipes fall back to the background. All-room counts and unresolved behavior belong in each family's record; neither zero detected gaps nor a passing UI assertion proves native visual equivalence.

## User checks (exact steps and where; none if none)

See the final checkpoint for the room samples and remaining comparisons.

## Where the facts went (links to the script map / engine notes / tracker entries)

[S5 checkpoint](../../docs/archive/checkpoints/20261017-S5-ManiacZak.md), [Maniac and Zak support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries).
