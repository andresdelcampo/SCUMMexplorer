# Maniac Mansion Classic S7: script-derived cutscene list

| | |
|---|---|
| Family / stage | ManiacClassic / S7 |
| Fixture and edition | `D:/Juegos/Lucas/mmansion ega en`, DOS v1 English |
| Date and author | 2026-10-18, Codex family agent |
| Registry change | Eligible for Cutscenes Partial: derived list and three bounded starts; parent integration owns the registry and generated matrix |

## What was done

Scope: one shared scan/run census and three source-established fresh-start trigger proofs, following REQUIREMENTS section 8 and the Indy3/MonkeyFloppy S7 models. The user named no scenes; examples are selected transparently from the scan. No handwritten catalog, shipped seed, original-game run or story-variable preparation is included.

## Commands run (through RTK)

```powershell
rtk proxy dotnet run --project .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj -- "D:\Juegos\Lucas\mmansion ega en" scan
rtk proxy dotnet run --project .verification/maniacclassic-cutscenes-20261018/Census/ManiacClassicCutsceneCensus.csproj -- "D:\Juegos\Lucas\mmansion ega en" script 131
rtk proxy dotnet run --project .verification/maniacclassic-cutscenes-20261018/Census/ManiacClassicCutsceneCensus.csproj -- "D:\Juegos\Lucas\mmansion ega en" raw
rtk proxy dotnet run --project .verification/maniacclassic-cutscenes-20261018/Census/ManiacClassicCutsceneCensus.csproj -- "D:\Juegos\Lucas\mmansion ega en" run
rtk proxy dotnet run --project .verification/maniacclassic-cutscenes-20261018/Census/ManiacClassicCutsceneCensus.csproj -- "D:\Juegos\Lucas\mmansion ega en" proof 1 394 09
rtk proxy dotnet run --project .verification/maniacclassic-cutscenes-20261018/Census/ManiacClassicCutsceneCensus.csproj -- "D:\Juegos\Lucas\mmansion ega en" proof 8 157 05
rtk proxy dotnet run --project .verification/maniacclassic-cutscenes-20261018/Census/ManiacClassicCutsceneCensus.csproj -- "D:\Juegos\Lucas\mmansion ega en" proof 17 190 04
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~ScummManiacClassicCutsceneTests
```

## Results (numbers)

After shared corrections `0bacc96` and `2632e39`, the production family factory `ManiacCutsceneSetup.Planner` finds **59 triggers, 27 groups, 25 static Scenes and two Dialogue/reactions**. The final census runs all 27 groups: **six began, 21 did not, zero caught exceptions, zero 6000-frame limits**, 40.5 seconds including planner creation. This is not an assertion of zero unsupported instructions. The complete list and run outcomes are in [census.tsv](census.tsv). The direct-invocation fix also lets Room 29's water valve begin its response, changing five starts to six.

The three examples were chosen from the scan because they represent a short reaction, a longer exchange and a separate TV presentation. Each uses a fresh native boot, disclosed Syd/Bernard party selection, a state-preview room visit and direct object verb activation. No additional prerequisites are invented. Titles come from the fixture's fifteen native verb names.

| Group representative | Source-attributed beginning | Result |
|---|---|---|
| Room 01 · Push doorbell, object 394 / verb 09 | Script 87 +0x001F | 187 frames, visits Room 26, displays the doorbell/package lines and returns to Room 1 with its cutscene ended |
| Room 08 · Turn off circuit breakers, object 157 / verb 05 | Script 47 +0x0001 | 905 frames, visits Room 31, displays the power-off branch and Fred/Tentacle exchange, returns to Room 8 with its cutscene ended |
| Room 17 · Turn on TV, object 190 / verb 04 | Script 112 +0x0000 | 366 frames, visits Room 40, displays Mark Eteer's publishing offer/stamp reminder, returns to Room 17 with its cutscene ended |

Proof files `proof-1.txt`, `proof-8.txt`, `proof-17.txt` include the selected object's source and the actual trace/messages. **Four focused tests pass, zero fail, zero skip** after the final shared correction and explicit limitation scope. They pin the scan, edition-native words, true source attribution, active-object invocation context, displayed lines, visited rooms and ending. The family harness builds with zero warnings/errors. Parent integration records WPF and full-suite results separately.

Parent actual-window verification passes after the context correction: background derivation takes 12.7 seconds and agrees with 59 triggers/27 groups/25 static scenes/six starts. The room-list doorbell reaches Script 87 in Room 26 with its line, and global-picker TV reaches Script 112 in Room 40 with the company/publishing line visible. Captures were inspected. Evidence directory: [window-classic-final](../maniaczak-cutscenes-20261018/window-classic-final); final integration details remain in the checkpoint.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source execution and app playback are distinct from original-game observation. No ScummVM executable was launched, as required by this run. Upstream ScummVM master [script_v2.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v2.cpp), read 18 October 2026: `o2_cutscene` preserves the user state, cursor, room and camera mode, hides the interface and freezes other scripts; `o2_endCutscene` restores the saved state; `o2_beginOverride` records the script position then skips the following jump. Prior S6 source-derived walking, sound lifetime and visual limitations remain.

## Differences and uncertainties kept

- Baseline production planner at `616d39f`: **zero triggers, zero groups, no verb words**, scan 1812 ms. The scan required a comma after a literal script call and after `text`; v1/v2 disassembly uses bare `startScript 0xNN` and `[text "..."]`. Script 131 demonstrably contains `cutscene` at +0x0006 and `endCutscene` at +0x0233. The shared engine owner received this mismatch; no game-specific scanner was added.
- Native boot waits for party choices. Those choices must be genuine inputs with the assumed party disclosed for unattended scene preparation; the S6 interactive start keeps them under player control.
- The old cutscene branch initially incremented its counter without a source-labelled begin trace. The shared owner received the attribution gap so a background cutscene cannot be mistaken for the selected trigger.
- First corrected-scan census (`0bacc96`, before direct-invocation context correction): 59 triggers, 27 groups, 25 static Scenes/2 Dialogue; five began, 22 did not, no exceptions or end-frame limits, 38.3 seconds. The circuit-breaker proof displayed the pool-empty branch. Its object code uses active object `var[0x009]`, but direct activation had retained a previous value rather than the selected object. Preserved in `census-before-invocation-fix.tsv` and `proof-8-before-invocation-fix.txt`; this is not accepted as the final scene proof.
- The first focused test run was three passes/one failure: the additional zero-unsupported assertion found `Script 174 +0x0000: unsupported v2 getClosestObjActor` after the doorbell scene returns to Room 1 and starts Ed's later background movement. The shared owner retained that opcode gap within the bounded S7 scope. The regression now explicitly permits only this named post-scene limitation; it still requires the selected scene's source beginning, real text, return room and ended scope. The claim is not a complete continuation of Ed's route.
- A listed scene is an exploration action from a stated state preview, not proof of an earned story route. A beginning is not proof of completion, actor correctness, audiovisual timing or original-game fidelity.
- S5/S6 original-game picture comparison, darkness/flashlight, native walking metric and untested parties remain open.

## User checks (exact steps and where; none if none)

Open the classic English fixture, select Room 1 and use its Play Cutscene button to choose `Room 01 · Push doorbell`. In Advanced, the global picker also offers `Room 08 · Turn off circuit breakers` and `Room 17 · Turn on TV`. The first launch derives its list in the background; the preview note discloses Syd/Bernard selection. Watch Ed's doorbell reaction, Fred's power-off response and the TV advert; check physical Pause/Skip, perceived animation, text pacing and sound synchronization. Parent owns actual-window captures. In the headless TV proof the offer appears at frame 47, company line at 148, mailing line at 240 and stamp reminder at 335; these are capture anchors, not original-game timing measurements.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), [S7 checkpoint](../../docs/archive/checkpoints/20261018-S7-ManiacZak.md).
