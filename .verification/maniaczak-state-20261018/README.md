# Maniac/Zak S8: explicit inventory choices and guarded undo

| | |
|---|---|
| Family / stage | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / S8 |
| Fixtures | `mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` under `D:/Juegos/Lucas` |
| Date and author | 2026-10-18, Maniac/Zak orchestrator and three family workers |
| Commits | See checkpoint and family records |
| Registry change | Status None → Full only after four-family edit/undo and window acceptance; other cells unchanged |

## What was done

Proposed R-12.7–R-12.10 in separate commit `d3103ed` define the bounded editor. Game Status shows held inventory across source-established playable characters. The user can explicitly reassign an already-held item, Apply, Undo the last batch within the paused dialog, or Cancel pending choices. There is no item creation, destruction, world drop, puzzle-flag editor, actor placement or costume change. These choices are labelled and are not played scenes or earned pickups.

Core exposes immutable snapshots and opaque session-bound edit/undo tokens. Every batch is validated before owner-only writes. Inventory slots, retained source room/code, object states/classes, other owners, actors and variables are preserved. Frame/signature/revision checks reject stale or cross-session tokens; a playback frame invalidates Undo. Native party-switch assignments or the native player range establish eligibility; reached actor-name instructions provide names. No game/language or party-number table was added in Scumm.

The new App window uses the real header action, source names and ordinary controls. Raw identifiers, source bindings, refusals and edit attribution appear only in Advanced; NPC-held items are hidden in Normal and read-only in Advanced. A modal pause stops the Play clock and sounds, restoring their captured state on close. Apply/Undo refresh the native inventory strip without executing scripts. Existing LOOM status dispatch stays separate; no other family is enabled.

## Commands run (through RTK)

Cwd `.worktrees/maniaczak-s8`. Example window command; repeat for the four configs with their own fixture/output/starting room (44 for Maniac, 1 for Zak):

```powershell
rtk proxy dotnet build .verification/maniaczak-state-20261018/WindowCheck/InventoryStatusWindowCheck.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
rtk proxy Exe/InventoryStatusWindowCheck/Debug/net10.0-windows/InventoryStatusWindowCheck.exe "D:\Juegos\Lucas\mmansion ega en" ".verification/maniaczak-state-20261018/window-maniac-classic-acceptance" 44 ".verification/maniaczak-state-20261018/WindowCheck/maniac-classic.json"
rtk git merge master
$env:SCUMM_UPDATE_SUPPORT_MATRIX="1"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~SupportMatrixTests" --verbosity quiet
Remove-Item Env:SCUMM_UPDATE_SUPPORT_MATRIX
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity quiet
$env:SCUMM_SOUND_TEST_ROOT="D:\Juegos\Lucas\loom ega mt32 en"
$env:SCUMM_INDY3_EGA_TEST_ROOT="D:\Juegos\Lucas\indy3 ega en"
$env:SCUMM_MT32_ROM_ROOT="D:\Juegos\Lucas\mt32"
rtk proxy dotnet test SCUMMexplorer.slnx --no-build --no-restore --verbosity quiet --logger "trx;LogFileName=maniaczak-s8-full.trx" --results-directory ".verification/maniaczak-state-20261018/test-results"
```

## Results (numbers)

All twelve family state tests pass. Integrated focused checks: **39 passed, zero failed/skipped**, covering the four state suites, three native object-slot lifecycle cases, four family conformance rows and LOOM Game Status regressions. Core owner also ran six LOOM EGA/CD boot regressions. Native source acquisition: Maniac key 55 from Dave 3 → Syd 1 → Undo 3, slot 0 and retained Room 1; Zak phone bill 15 from Zak 1 → Annie 2 → Undo 1, slot 2 and retained Room 1. The original Zak Read response after Undo still reaches its own `$1138` line.

All four final window checks pass, including actual header invocation, source-name selection, Apply/Undo, Cancel of a pending choice and restoration of both prior playing/paused modes. No game frame advances while the modal is open, or during Apply/Undo/close. Native inventory and unchanged unrelated state are asserted; captures were inspected. Final directories: `window-maniac-classic-acceptance` (frame 3310), `window-maniac-enhanced-verified` (1556), `window-zak-classic-acceptance` (520), `window-zak-enhanced-acceptance` (521). These use private profiles/cache and real window/timer callbacks, not physical mouse input or timing fidelity. Build: zero warnings/errors; generated matrices: three passed. Full suite pending. Private captures, traces and test reports are retained locally and ignored; harness/config code is committed, rejected JSON retained locally.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official `script_v2.cpp` and `object.cpp`, reviewed 18 October 2026, establish native actor names, player ranges, queued/running sentence work, object-slot flags and separate owner storage. Source references and own-edition offsets are in the family records/maps. The editor performs the owner-table component as a disclosed choice; it does not emulate the native Give action, inventory callback or puzzle effects. No ScummVM executable was launched.

## Differences and uncertainties kept

Failed evidence is retained. Initial window checks passed behavior but exposed a ComboBox record-string label in captures; source-name presentation and a matching harness assertion corrected it. The first enhanced Maniac harness config lacked its final owner assertion; an attempted PowerShell append produced a nested array, rejected before Play. The invalid config and harness failure log remain; the final config is regenerated from the existing native route with valid JSON.

A strict all-pending-object guard initially rejected both Zak editions forever: the integrated focused run had four failures/32 passes. A 3000-frame wait did not help. Source inspection showed resident `0xFD` special handlers (including Room 1 objects 95/101/107), not unfinished input/sentence work. The corrected guard uses native special/background flags while continuing to block executing code, input, sentence and ordinary foreground object actions. The regression checks coexisting special/foreground slots, foreground stop and executing-special refusal. No room exception or fabricated state was used. Family records retain the failed wait and final direct readiness result.

Undo is local to the paused window and expires after any game frame; it is not a save-state system. Arbitrary assignments are not validated as full-game puzzle outcomes. S5 visual/darkness, walking/timing/audio, Classic Maniac Script 174, Zak costume estimates/capped scenes and S9 missing alternate copies remain unchanged. No catalog, seed or game/profile data mutation is included.

## User checks (exact steps and where; none if none)

Open a checked Maniac copy, make native party choices, lift the mat and take the key; open Game Status, select Syd, Apply and Undo before closing. For Zak, open the bedroom drawer and take the phone bill; open Game Status, select Annie, Apply and Undo. Select another pending recipient and Cancel to discard it. The inventory strip should return to the original character after Undo; characters and room stay put. This is a short optional UI look, not a repeated timing/listening request. Exact automated native routes are in `WindowCheck/*.json` and family records.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Checkpoint](../../docs/checkpoints/20261018-S8-ManiacZak.md), [Maniac map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [Zak map](../../docs/zak/ZAK_SCRIPT_MAP.md), family trackers, engine object-state notes and Proposed REQUIREMENTS §12.1. Four family evidence records use the fixed template.
