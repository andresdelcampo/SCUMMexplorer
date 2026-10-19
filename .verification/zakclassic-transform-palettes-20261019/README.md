# ZakClassic S2: retained transformation palette contexts

| | |
|---|---|
| Family / stage | ZakClassic / S2 palette context follow-up |
| Fixture and edition | `Games/zak ega en`, DOS classic v1 English; external root `D:/Juegos/Lucas` |
| Date and author | 2026-10-19, Codex transformation worker |
| Commits | This record and its dedicated tests are one item; see the integration checkpoint |
| Registry change | Unchanged: Costumes Partial; 35 costumes, 24 Script and 11 labelled Estimate before and after this item |

## What was done

Read every own-copy script, then observed native costume operations in six separately booted, disclosed room previews. The harness never writes an ego, actor, palette, costume, variable, object flag, inventory owner or prerequisite. It selects the room through the existing explorer preview API, advances ordinary frames, and copies observations. It separately invokes the native perched-bird verb through the existing direct exploration API, disclosing that this bypasses the story trigger.

The important new source distinction is the native indirect costume table: room-entry code chooses a form using both the actor and an initialized table cell. Literal `actorOps` scans alone miss the shared costume 8 result. Other retained palettes come from prior scripts, not a nearby instruction on another actor. Full decoded source anchors are supplied to the parent for the canonical [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md); this worker does not own shared production files or docs.

## Commands run (through RTK)

From the isolated `zakclassic-transform-palettes-20261019` worktree:

```powershell
rtk proxy dotnet build .verification/zakclassic-transform-palettes-20261019/ZakClassicTransformationProbe.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/ZakClassicTransformationProbe/Debug/net10.0/ZakClassicTransformationProbe.dll all
rtk proxy dotnet Exe/ZakClassicTransformationProbe/Debug/net10.0/ZakClassicTransformationProbe.dll "Script 50" "Room 27 / Entry script" "Room 32 / Entry script" "Room 39 / Entry script" "Script 108"
rtk proxy dotnet Exe/ZakClassicTransformationProbe/Debug/net10.0/ZakClassicTransformationProbe.dll preview 20 22 27 32 39 42
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummZakClassicTransformationPaletteTests|FullyQualifiedName~ScummV1ActorColorsTests|FullyQualifiedName~ScummV1PalettePersistenceTests" -p:NuGetAudit=false --verbosity quiet --logger "trx;LogFileName=transformations-final.trx" --results-directory .verification/zakclassic-transform-palettes-20261019/test-results
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~SupportConformanceTests.FamilyIsRecognisedAndItsClaimsHold&DisplayName~ZakClassic" --verbosity quiet --logger "trx;LogFileName=conformance.trx" --results-directory .verification/zakclassic-transform-palettes-20261019/test-results
```

The local raw instruction census is `source.txt`; copied reduced preview operands are `previews-reduced.txt`, and the final assertion run is `previews-final.txt`. Capture used `rtk proxy powershell -NoProfile -Command` and `Set-Content -Encoding UTF8`. Raw source/variable dumps and generated TRX files are ignored local evidence, not shipped game data. This directory's private `.gitignore` excludes its test output explicitly.

## Results (numbers)

Eight new tests pass. With the ten existing classic palette safeguards, **18 passed, 0 failed, 0 skipped**, in 34 seconds. Separate ZakClassic conformance: **1 passed, 0 failed, 0 skipped**, in 10 seconds. Final probe build: **0 warnings, 0 errors**.

Five native entry previews retain Zak's reached suit colour 13 while changing to the own-source selected costume. Three exercise the indirect table read, including the actual selected cell and its value. The sixth preview naturally reaches flying bird costume 15 on actor 6 in Room 42, retaining its reached suit colour 6. Direct native object 629 verb FD then gives the same actor perched costume 14 with all palette slots unchanged. The tests distinguish the animal's native appearance from an earned transfer of player control to it.

The source-table test pins all 13 initialized cells and demonstrates that costume 8 is absent from literal costume assignments even though the native transformation table can select it. A separate source test prevents associating the dolphin actor's palette write with the nearby falling-Zak costume instruction. Global costume cards remain estimates where they were estimates.

## Comparison with ScummVM (source files and date; runtime comparison if any)

No ScummVM executable was run. This investigation extends the source rules already reviewed on 18 October in [the earlier palette evidence](../zakclassic-fidelity-20261018/README.md): `actor.cpp` (`Actor::setActorCostume` retains v1 palettes), `script_v2.cpp` (`o2_actorOps`, actor-indexed palette slot), and `costume.cpp` (native Zak suit colour from the reached actor). New source facts in this record come from the supplied game's own v1 disassembly and Explorer execution, read 19 October 2026.

This is a source-semantic and reached Explorer state comparison, not an original-interpreter picture, audiovisual timing, earned puzzle route or all-path proof. No other release is inferred from this copy.

## Differences and uncertainties kept

All eleven global estimates remain: 4, 5, 6, 7, 8, 9, 12, 13, 14, 15 and 32. Other protagonists' transformation table results are decoded possible contexts; this worker did not earn the native selector prerequisites. A source-written palette value and a sampled current palette are distinct. In particular, Leslie's saved colour is initially 7, whereas native Script 5 conditionally changes the actor to 8 when its costume differs from 4. No universal colour was inferred from either value.

Direct room selection does not establish arrival prerequisites. In several transformation previews, Zak's appearance changes while the actor remains outside the selected room; those are source-operation observations, not accepted pictures of a completed story route. Room 22 and the bird's Room 42 preview do place the corresponding actors visibly. Reached observations retain inherited path and operand estimates. The unprepared water preview from the prior record still retains palette 0; this worker has not played the earlier falling/water scene that would establish actor 5's colour 11.

The first eight-test packet had six failed assertions and two passes. Five failures came from an overbroad harness assertion that prohibited any actor palette write: the native background Script 5 writes Leslie's palette while Zak's palette remains unchanged. The corrected assertion scopes the unchanged actor being tested. The sixth failure incorrectly expected Leslie's live palette to equal the saved colour 7; the assertion now checks the independently stored source colour and does not pin the actor to a false universal value. The original `transformations.trx` is retained. These were harness assumptions corrected from source evidence, not relaxed native palette expectations. The initial verbose observation output also copied every variable; final output retains only ego/indirect-index/selected-cell bindings.

Original-game visual acceptance, complete transformation prerequisites and other contexts remain open. The parent independently owns source-context API changes, matrix regeneration and the full integration suite.

## User checks (exact steps and where; none if none)

No new user-only check is required for this bounded evidence item. The existing S2 original-game picture comparison remains pending under the standing no-ScummVM-run rule.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent integrates the decoded transformation table and retained-palette rows in [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), the unchanged Partial claim in [Zak tracker](../../docs/zak/SUPPORT_STATUS.md), and the combined [19 October palette checkpoint](../../docs/checkpoints/20261019-Palettes-ManiacZak.md). Generic v1 palette persistence is already documented in [engine object states](../../docs/engine/SCUMM_OBJECT_STATES.md).
