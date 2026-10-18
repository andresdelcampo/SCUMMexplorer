# Maniac Mansion Enhanced S7: source-derived cutscene previews

| | |
|---|---|
| Family / stage | ManiacEnhanced / S7 |
| Fixture and edition | `Games/mmansionE ega es`, enhanced DOS Spanish, SCUMM v2; external root `D:\Juegos\Lucas` |
| Date and author | 2026-10-18, Codex family/shared Core worker |
| Commits | `0bacc96`, `2632e39`, `5efef97`; final evidence commit follows |
| Registry change | Parent integrates Cutscenes None to Partial only after real-window and full-suite acceptance; no hand-validated catalog claim |

## What was done

Used the shared scanner/planner, without a catalog or seed. The original planner produced zero triggers and no verb words. Old `startScript` operands have no trailing argument comma, and old print operands use `text "..."`; the scanner now accepts both old and later syntax. `printEgo` counting is added only for the v1/v2 data source. Native cutscene scope starts now carry their actual script/offset in the trace so unrelated background cutscenes cannot falsely credit a scanned trigger.

`ManiacCutsceneSetup` supplies a fresh-session factory using the existing fingerprinted native menu input recipe. It selects Syd/Bernard/Start and waits through the opening; its assumption appears in `StartNote`. Ordinary Play remains untouched. Existing completed parties are preserved. A menu with delivered or pending user input refuses automatic preparation, preserving partial choices too. Unknown menu fingerprints fail closed.

Direct exploration actions now bind v1/v2 active verb/object1/object2 as `o2_doSentence` sub1 does, before running the object code. This is invocation context, not fabricated story state. Previously the circuit breaker read a stale object and reached the pool-empty branch; `before-active-context.txt` retains that failed evidence. The correct invocation reaches the power-off response.

Readable list text optionally uses the exact reviewed executable font's Unicode meanings. The optional planner transform changes labels/first-text only, never script bytes or game rendering. It requires the full executable SHA256 and leaves all other editions unchanged. See `font-proof.py`; the S6 built-in font mismatch remains open.

## Commands run (through RTK)

Working directory: `.worktrees/maniac-enhanced-s7`.

```powershell
rtk proxy dotnet run --project .verification/maniacenhanced-cutscenes-20261018/Census/Census.csproj -- "D:\Juegos\Lucas\mmansionE ega es"
rtk proxy dotnet run --project .verification/maniacenhanced-cutscenes-20261018/Census/Census.csproj -- "D:\Juegos\Lucas\mmansionE ega es" proof
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummManiacEnhancedCutsceneTests|FullyQualifiedName~LoomCutsceneGroupingTests|FullyQualifiedName~LoomCutsceneTitlesTests|FullyQualifiedName~LoomCdCutsceneTests.TheCutscenesAreDerived" --no-restore
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummManiacEnhancedCutsceneTests.NativeStartupPreparation" --no-restore
rtk proxy python .verification/maniacenhanced-cutscenes-20261018/font-proof.py "D:\Juegos\Lucas\mmansionE ega es\MANIAC.EXE"
```

The initial broad CD test filter accidentally included an unrelated whole-catalog sweep; it was stopped and replaced with the bounded scanner regression above. Initial zero scan, pre-context census, and final census are retained. One complete final rerun was justified by the demonstrated active-object context defect; no unchanged full audit was repeated.

## Results (numbers)

`census-final.txt`: 59 triggers, 27 groups, 25 Scenes / 2 Dialogue and reactions. Seven fresh prepared runs begin an attributable cutscene; twenty do not within the 600-frame trial. Zero caught failures; zero 6000-frame caps. The pre-context run had six begins and the circuit-breaker response was wrong.

The user named no scenes, so three representative triggers were selected from the actual scan. `proofs.txt` retains the source instructions and reached message frames. All three begin from independent fresh prepared sessions, finish their scope and messages, return to the starting room, and restore control:

| Trigger | Source cutscene span | Measured result |
|---|---|---|
| Room 1 object 394 verb 9, doorbell | Script 87 +001F through +00F5 | 187 frames; visits 26; returns 1 |
| Room 8 object 157 verb 5, circuit breaker | Script 47 +0001 through +0288 | 888 frames; visits 31; correct power-off response; returns 8 |
| Room 17 object 190 verb 4, television | Script 112 +0000 through +00E1 | 390 frames; visits 40; Mark Eteer advertisement; returns 17 |

Thirteen focused checks passed, including the enhanced scene tests and LOOM EGA/CD grouping/title checks. The final queued-choice guard passed its additional focused check. Parent reports actual-window doorbell and television playback, matching final census of 7 begun, with screenshots inspected; evidence lives in `../maniaczak-cutscenes-20261018/window-enhanced-final`. Parent owns final solution/full-suite verification and merge.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source inspection 18 October 2026: [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp), `o2_doSentence` lines929â€“969 assigns active verb and objects before `runObjectScript` except the special verbs 250/253/254; `o2_cutscene`/`o2_endCutscene` supply native scopes. The old instruction forms are read by the existing version-specific disassembler. No ScummVM executable was launched. These are engine-source and prepared-session results, not original-game observation or visual/timing equivalence.

ScummVM `charset-fontdata.cpp`'s Spanish replacement map actually uses the same special glyph indices as German. It does not establish this edition's Ã©/Ã¡ meanings. The supplied native `MANIAC.EXE` does:128glyphsÃ—8bytes at64116, located by identical ASCII digit/letter runs. Full executable SHA256 `AF7A7F667789E4FCA76F4C195BB2996F622D50E796E6CF01E64E4D0A1E0D477A`; font SHA256 `336B4DFC32EB17CDF6BA78D9116C7F219605807D9573F185A3CDB0340652813A`. Read-only bitmap inspection establishes `< Â¡`, `= Ã¡`, `> Ã­`, `[ Â¿`, `\ Ã¨`, `] Ã©`, `{ Ã±`, `| Ã³`, `} Ãº`, `~ Ã¼`. The ellipsis-like caret glyph is deliberately left raw. No executable or font asset is committed.

## Differences and uncertainties kept

The list is derived, not complete or hand-validated. Literal calls are followed only to depth 3; dynamic calls, fallback-only triggers, shared helpers, and state-gated branches can be absent. Twenty groups do not begin from the stated fresh preview; no story prerequisites were invented to make them run. Begin alone is not completion or actor/timing fidelity; only the three representative proofs pin returned control and expected dialogue.

Catalog room visits are disclosed state previews; they do not prove a walked story route. Existing S5 original-game visual acceptance, S6 darkness/flashlight and old nearest-box approximation, and native font rendering mismatch remain unresolved. The classic worker found a postscene unsupported `getClosestObjActor`; no zero-unsupported or full-game claim follows from these scene proofs. Rendered Spanish text still uses the earlier built-in font; only the verified list labels are corrected here. No S8/S9 work or shipped seed is included.

## User checks (exact steps and where; none if none)

Open this enhanced Spanish edition. In Advanced, open Play Cutscene and choose `Room 17 · Encender TV`; observe the advertisement in Room 40 (first message 47 frames after launch). From Room 1 use Play Cutscene and choose `Empuja timbre`; observe the Room 26 reaction. Room 8 `Apagar automáticos` should show the power-off complaint, not the pool-empty branch. Timing, sound synchronization and original-game visual comparison remain user checks; no agent execution of ScummVM occurred.

## Where the facts went (links to the script map / engine notes / tracker entries)

Family facts and tracker are parent/classic-worker-owned: [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [support status](../../docs/maniac/SUPPORT_STATUS.md). Parent owns engine documentation, requirement R8.34, registry/matrix, App integration and [checkpoint](../../docs/checkpoints/20261018-S7-ManiacZak.md).
