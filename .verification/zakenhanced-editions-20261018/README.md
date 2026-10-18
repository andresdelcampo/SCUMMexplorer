# ZakEnhanced S9 available-copy check — 18 October 2026

Fixture: `D:/Juegos/Lucas/zakE ega en`, English DOS SCUMM v2. A content scan of every immediate directory under `D:/Juegos/Lucas` found **one ZakEnhanced copy**. ZakClassic is a separate family, not an alternate Enhanced edition. Alternate-language/release/demo comparison is **blocked by missing copies**; this record does not complete that coverage or raise any registry cell.

| Edition | Check | Result |
|---|---|---|
| Available English enhanced | Detection, profile and resource adapter | ZakEnhanced; `English, Zak McKracken enhanced`; V2 dialect; ScummV2DataSource |
| Same copy | All indexed room structures and resource inventory | 58 rooms read; 39 costumes, 148 scripts, 112 sounds indexed; this is structural coverage, not a new visual/audio audit |
| Same copy | Own scene text and native font distinction | Script140's own first line decoded; native v2 apostrophe raster differs from v1; hashes in `result.json` |
| Same copy | Display-label independence | Focused regression verifies changing only descriptive Edition metadata cannot select another script dialect; synthetic metadata is not a real alternate language |
| Same copy | S4b–S7 behavior | Reused bounded native play and S7 source/window evidence: 54 groups, 13 starts, five frame limits; no broad rerun justified by a new edition |
| Other Enhanced languages, releases or demos | Edition-dependent detection/scripts/fonts/graphics/audio comparison | Blocked: no additional local copy |

The bounded harness reads data only, inventories detected family copies and records index/glyph hashes plus resource/reader facts in `result.json`. The index fingerprint is `81A29C74DA8F6E181EED9FED2EC7F60478B40A6A14594582B80B09ECB9BED0DA`; it describes this fixture, not every release. Based on the relevant categories from [prior editions workflow](../editions-20261005/README.md), only available-copy identification, data reading, text/font and version-selection checks were repeated. No new edition branch was needed.

Commands, cwd `.worktrees/zak-enhanced-s9`:

```
rtk proxy dotnet build .verification/zakenhanced-editions-20261018/EditionCheck/EditionCheck.csproj --ignore-failed-sources -p:NuGetAudit=false
rtk proxy dotnet Exe/EditionCheck/Debug/net10.0/EditionCheck.dll D:/Juegos/Lucas
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakEnhancedEditionTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ZakEnhanced)"
```

Build: zero warnings/errors. **Two tests pass**, zero failures/skips: new edition contract and existing family conformance. No shared runtime/App changes. Parent owns final integration/full suite.

Comparison remains source/explorer evidence only; no ScummVM executable ran. Reuse [S7 record](../zakenhanced-cutscenes-20261018/README.md) and [S7 checkpoint](../../docs/checkpoints/20261018-S7-ManiacZak.md) for source comparison and real-window results. S5 original-game visual acceptance, darkness/flashlight/walking limits, five capped S7 previews and user timing/listening checks remain unchanged. No data/profile mutation, seed, catalog, forced story state or junction. Registry unchanged because no missing feature was implemented and no alternate edition was verified. Next edition checks require an actual additional Enhanced copy; no new user retest is needed for this test/document-only pass.
