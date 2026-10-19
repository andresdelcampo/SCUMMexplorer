# Maniac/Zak S7: integration and real-window evidence

| | |
|---|---|
| Families / stage | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / S7 |
| Fixtures | `D:/Juegos/Lucas/mmansion ega en`, `mmansionE ega es`, `zak ega en`, `zakE ega en` |
| Date and author | 2026-10-18, Maniac/Zak orchestrator |
| Registry change | Cutscenes None to Partial for all four families; other columns unchanged |

## What was done

The shared scan now follows native v1/v2 bare script-call operands and text syntax; old printEgo counts are version-gated. Native cutscene beginnings carry source labels so fresh-run attribution does not credit an unrelated background scene. Direct old object actions bind the engine's active verb/object arguments before their code runs. This corrects the circuit-breaker response that previously used a stale object value.

Maniac's family factory uses the existing fingerprinted native-input preview setup and discloses Syd/Bernard/Start in the scene note. Ordinary Play retains its party menu. Scene launch preserves queued or delivered player choices and an already completed party. The Spanish scene-label transform is optional and tied to an exact executable fingerprint, using glyphs observed read-only in its font. It changes readable labels only; the inherited game-text font mismatch is retained.

Each worker supplied a complete scan/run census and three source-attributed starts with actual lines and endings. The user named no particular scenes; these are transparently selected examples from the scan, not a handwritten catalog. The parent checks the real room-list and global-picker actions in MainWindow. No seed is shipped.

## Commands run (through RTK)

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/maniaczak-s7`.

```powershell
rtk proxy dotnet build .verification/maniaczak-cutscenes-20261018/WindowCheck/ManiacZakCutsceneWindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy .\Exe\ManiacZakCutsceneWindowCheck\Debug\net10.0-windows\ManiacZakCutsceneWindowCheck.exe "D:\Juegos\Lucas\mmansion ega en" ".verification/maniaczak-cutscenes-20261018/window-classic-final" ".verification/maniaczak-cutscenes-20261018/WindowCheck/maniac-classic.json"
```

The other scenarios use the matching fixture, output directory and JSON beside `Program.cs`. Each window uses private profile/cache directories, waits for background derivation, invokes the same room-list/picker actions as the app, checks the selected script's own cutscene trace and captures the rendered window. After confirming the real timer advances, it drives that timer handler deterministically. Physical mouse mapping, perceived timing and original-interpreter fidelity are separate checks. Captures and logs stay local; the driver/configuration and written evidence are committed.

Final integration commands:

```powershell
rtk git merge master
rtk proxy dotnet build SCUMMexplorer.slnx -p:NuGetAudit=false --verbosity quiet
$env:SCUMM_UPDATE_SUPPORT_MATRIX = '1'
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~SupportMatrixTests --verbosity quiet
$env:SCUMM_UPDATE_SUPPORT_MATRIX = $null
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore -p:NuGetAudit=false --verbosity quiet
$env:SCUMM_SOUND_TEST_ROOT = 'D:\Juegos\Lucas\loom ega mt32 en'
$env:SCUMM_INDY3_EGA_TEST_ROOT = 'D:\Juegos\Lucas\indy3 ega en'
$env:SCUMM_MT32_ROM_ROOT = 'D:\Juegos\Lucas\mt32'
rtk proxy dotnet test SCUMMexplorer.slnx --no-build --no-restore --verbosity quiet --logger "trx;LogFileName=maniaczak-s7-full.trx" --results-directory ".verification/maniaczak-cutscenes-20261018/test-results"
```

The new parent worktree initially had no restored test SDK; its first no-restore matrix command executed no tests and is not counted as validation. After restoring/building the solution, the actual three matrix tests passed and regenerated both tables. Current master 19d2e7d was already integrated. The final solution build has zero warnings/errors. Full suite: **1215 passed, zero failed, five skipped** (1220 total, 8m28s), including LOOM and neighboring-family regressions. Report: `test-results/maniaczak-s7-full.trx` in this integration worktree. Built App: `.worktrees/maniaczak-s7/Exe/Debug/SCUMMexplorer.exe`.

## Results (numbers)

| Family | Triggers | Groups | Static scenes | Begun | Not begun | 6000-frame bounds |
|---|---:|---:|---:|---:|---:|---:|
| ManiacClassic | 59 | 27 | 25 | 6 | 21 | 0 |
| ManiacEnhanced | 59 | 27 | 25 | 7 | 20 | 0 |
| ZakClassic | 130 | 54 | 37 | 13 | 41 | 5 |
| ZakEnhanced | 130 | 54 | 37 | 13 | 41 | 5 |

Both Maniac window censuses match those final headless counts. Classic background derivation takes 12.7s; enhanced 17.5s in the two-window run. The room-list doorbell shows its own dialogue in Room 26; the global-picker TV shows the publisher's artwork and text in Room 40. Captures were inspected. Direct-action fixes are included; the earlier five-start counts are retained in family baseline evidence.

Both Zak window censuses also match: 130 triggers, 54 groups, 37 static scenes and 13 starts per edition. Background derivation takes 132.9s classic and 135.9s enhanced while both windows run. The room list displays Script 140's native intertitle, and the global picker displays Script 39's baker response. Both checks pass, with source trace/message assertions and inspected captures. These windows preserve the five bounded runs in the census rather than calling them completed scenes. Final merged suite results follow at the checkpoint.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source only, read 18 October 2026: ScummVM `script_v2.cpp` (`o2_doSentence`, `o2_cutscene`, `o2_endCutscene`, `o2_beginOverride`), and the existing source contracts in `script.cpp`, `room.cpp` and `verbs.cpp`. No ScummVM executable was run. Family script maps and proof transcripts identify each game's resource, script and instruction. App captures are observations of the explorer, not original-game comparisons.

## Differences and uncertainties kept

Initial Maniac scans found zero triggers and Zak found only seven because of the syntax mismatch. The circuit-breaker proof initially spoke the pool-empty branch due to stale invocation arguments; both failed results are retained. Classic doorbell completion can reach an unsupported background `getClosestObjActor` in Script 174; the family test pins that limitation rather than claiming zero unsupported instructions.

State-gated starts get no invented prerequisites. Normal mode hides entries whose completed fresh trial began nothing; Advanced retains their source notes. Five Zak starts hit the frame bound; a beginning does not prove completion or correct full-story prerequisites. Buy's native word is created after boot, so its scan title retains the raw verb label rather than inventing a name. S5 gaps/camera questions, native darkness/flashlight, walking approximation, Spanish rendered-font mismatch and original-game timing/picture comparison remain open. S8/S9 were not performed.

## User checks (exact steps and where; none if none)

1. Open either Maniac fixture and Room 1. After derivation, use the room's Play Cutscene action for the doorbell. A scene launch from an unanswered menu uses the disclosed preview party; if you begin choosing characters yourself, finish that screen first. In Advanced, open the global picker and play Room 17's TV or Room 8's circuit breaker. Check the matching dialogue, actors, Pause, and Skip when its override is available.
2. Open either Zak fixture. Use Room 8's Entry scene from its room list, then Advanced global picker (All playable filter), Room 3's Push doorbell. Check the secret-room exchange and baker response, Space pause/resume, Escape and sound synchronization.
3. Compare perceived timing, text pacing, PC-speaker/PCjr sound and original-game appearance manually. These are not approval gates for this run.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Checkpoint](../../docs/checkpoints/20261018-S7-ManiacZak.md), [Maniac and Zak support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), [requirements](../../docs/REQUIREMENTS.md). Family records contain the full census and three source proof transcripts.
