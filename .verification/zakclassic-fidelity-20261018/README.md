# ZakClassic fidelity: reached costume palette contexts

| | |
|---|---|
| Family / stage | ZakClassic / S2 fidelity follow-up |
| Fixture and edition | `Games/zak ega en` (DOS classic v1, English), external root `D:/Juegos/Lucas` |
| Date and author | 2026-10-18, Codex palette worktree agent |
| Commits | See integration checkpoint; shared runtime correction owned separately by parent |
| Registry change | Unchanged: Costumes Partial. 24 Script / 11 labelled Estimate before and after. No original-game comparison. |

## What was done

Read all 111 costume/palette settings (19 variable operands), kept the exact value operand in `ScummV1ActorSetting`, and enriched each estimate's evidence with the possible actors, their literal palette writes and unresolved variable-write sites. This exposes, for example, both actors sharing costume 7 and actor 4's variable palette, and both actor contexts for costume 6. Neither a static candidate nor one observed execution promotes an uncertain palette.

The bounded native opening was observed through copied pre-instruction observations. A separate read-only timeline starts at the source-defined engine palette zero, resolves the reached operand values, applies only native costume/palette semantics, and compares the result against every actor's final palette. Explicit room selections are separately labelled state previews: they run the selected room's own code but do not establish its story prerequisites or claim to have played its route. No variable, costume, palette, room or inventory value is written by the harness.

This exposed an engine defect: `CostumeChanged` was resetting all palette slots to their indices even in v1, although native v1 costume changes retain the actor's palette. Family regressions fail before the separately owned shared correction: Room 22 changes Zak's reached palette 13 to 1; Rooms 31 and 41 change actor 5's palette 0 to 5 despite no palette instruction. The production correction is in the parent's shared engine commit; this item adds its own-data regression coverage and contextual evidence.

## Commands run (through RTK)

From `.worktrees/zak-classic-fidelity`:

```powershell
rtk proxy dotnet build .verification/zakclassic-fidelity-20261018/PaletteCheck.csproj --verbosity quiet -p:NuGetAudit=false
rtk proxy dotnet Exe/PaletteCheck/Debug/net10.0/PaletteCheck.dll "D:\Juegos\Lucas\zak ega en"
rtk proxy dotnet Exe/PaletteCheck/Debug/net10.0/PaletteCheck.dll "D:\Juegos\Lucas\zak ega en" reached
rtk proxy dotnet Exe/PaletteCheck/Debug/net10.0/PaletteCheck.dll "D:\Juegos\Lucas\zak ega en" boss
rtk proxy dotnet Exe/PaletteCheck/Debug/net10.0/PaletteCheck.dll "D:\Juegos\Lucas\zak ega en" 22 reached
rtk proxy dotnet Exe/PaletteCheck/Debug/net10.0/PaletteCheck.dll "D:\Juegos\Lucas\zak ega en" 31 reached
rtk proxy dotnet Exe/PaletteCheck/Debug/net10.0/PaletteCheck.dll "D:\Juegos\Lucas\zak ega en" 41 reached
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummV1ActorColorsTests" --verbosity quiet -p:NuGetAudit=false
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummV1ActorColorsTests|FullyQualifiedName~ScummV1PalettePersistenceTests|FullyQualifiedName~ScummSessionArchitectureTests.TheGenericSessionBootsALoomEgaGameWithNoLoomCode" --verbosity quiet -p:NuGetAudit=false --logger "trx;LogFileName=palette-final-tests.trx" --results-directory .verification/zakclassic-fidelity-20261018/test-results
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~LoomBootTests|FullyQualifiedName~LoomVgaTests.TheCdIntroPlaysFromTheSkillScreenThroughTheOvertureToTheGameStart" --verbosity quiet
```

`all` is an optional final argument for local full source inspection. That large raw instruction dump and verbose observation JSON remain ignored local evidence, not committed game resources. The supplied `before-table.txt` and final table contain only the 35 palette decisions/evidence, not resource bytes. Output files are captured with `rtk proxy powershell -NoProfile -Command` and `Set-Content -Encoding UTF8`.

## Results (numbers)

Baseline and final: **35 costumes; 24 Script, 11 Estimate; 111 settings; 19 variable operands**. All displayed suit values and bases are unchanged. Estimate evidence now preserves alternate actor contexts and the variable operands rather than discarding them.

The full source-defined opening completes in **1784 frames**, at Room 1 with player control, outside a cutscene and without hitting its cap. Native opening observation and all twelve final actor palette comparisons passed. The separate frame-driven check stops at the first source assignment of boss costume 32 after **52 frames** and reads the actual live actor 10: Room 46, visible, palette **0**. It invokes the same protected `RunBootScript` that the production constructor uses, through reflection solely to drive the normal engine entry frame by frame; no native state is assigned. Final Room 20/22/31/41/42 source previews also pass all twelve actor palette comparisons.

Before shared correction, the seven focused palette tests yielded **4 passed, 3 failed** (14 seconds). The failing cases are exactly the three palette-reset proofs described above. This failed evidence is retained rather than weakened to accept index colours.

Final focused results: **11 passed, zero failed/skipped**, 13 seconds, including the seven own-family palette cases, three version-specific palette-persistence cases from the shared correction and one generic LOOM EGA boot safeguard. The shared correction is parent commit `66e530a`, cherry-picked for verification as `c4b84291`. The harness build has zero warnings/errors. Local TRX: `test-results/palette-final-tests.trx`.

Separate required LOOM EGA/CD opening filter: **6 passed, zero failed/skipped**, 5 seconds. Whole-solution validation and matrix regeneration belong to the parent's integration step; this item does not raise a support cell.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official ScummVM master source fetched/read **18 October 2026**, never executed:

- [`actor.cpp`, `Actor::setActorCostume`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/actor.cpp): v1 returns before palette reinitialization; `initActor` supplies the initial zero palette.
- [`script_v2.cpp`, `o2_actorOps`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp): v1 palette writes use the actor number as the palette index; set-costume and set-palette are separate operations.
- [`costume.cpp`, `ClassicCostumeRenderer::byleRLEDecode_C64`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/costume.cpp): DOS Zak uses native skin index 8 and the reached actor palette slot for suit colour. Darkness is a separate native effect handled by the other fidelity item.

The comparison is with source semantics and Explorer execution. It is not an original-game visual or timing comparison. No ScummVM program was run and no game/profile/ROM/save files were changed.

## Differences and uncertainties kept

The eleven estimates remain **4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32**. The earlier ten-estimate evidence is historical. Remaining uncertainty includes call/branch order, saved or indirect costume values, earlier palette writers, variable actor identity and random/source-dependent palette choices. A global costume card represents one labelled estimate, whereas Play renders its reached actor state.

The reached opening assigns boss costume 32 in Script 127 +0002 before boot Script 1 +0108 assigns actor 10 palette 7. Thus the earlier visible boss context uses the engine-initial palette 0; the global card still shows labelled estimate 7, derived from the boot script's final palette. This discrepancy is now explicit, and the card was not silently replaced with a sampled context. Costume 30 likewise has initial 0 and later 14 in the opening; costume 6 has actor 2's possible 4 and engine-initial actor 6's reached 6. An already-derived colour does not mean there is one universal colour for a costume.

The Room 22 preview uses the Zak branch (costume 5, retained palette 13); its other source branch gives costume 6 to Annie. Room 31/41 previews give actor 5 costume 13 while retaining palette 0, because the prior Script 50 that would set 11 was not played. These are useful contrary contexts, not proof of a valid late-game arrival. The missing prerequisite is not filled.

Failed investigation setup: the initial harness treated the instruction's `Bytes` string as a byte array (compile error), then passed `all` as the fixture argument (missing-folder error); both were corrected. The initial preview argument index accidentally repeated boot-only observations and was corrected before reporting preview results. The first post-preview timeline/native equality guard exposed the genuine palette-reset mismatch; diagnostic output was retained locally. A Room 26 preview did not naturally start Script 81 within its bound, so it provides no Mars cutscene proof and was not promoted or forced. These failures do not justify changing source state.

## User checks (exact steps and where; none if none)

No new original-runtime check requested in this bounded investigation. Existing original-game visual acceptance remains open. The parent may inspect the corresponding Explorer source preview as part of the combined window check; no state preview is described as a played route.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent integrates own-edition source rows and the retained Partial claim in [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md) and [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), with version semantics in existing engine notes and the run's checkpoint. This record retains commands/results, not a new game reference document.
