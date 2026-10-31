# ZakClassic S9: available-copy compatibility checks

| | |
|---|---|
| Family / stage | ZakClassic / S9 |
| Fixture and edition | Games/zak ega en; English DOS classic, SCUMM v1 |
| Date and author | 2026-10-18, Zak family agent (Codex) |
| Commits | this family commit |
| Registry change | unchanged: no alternate edition verified or missing feature implemented |

## What was done

Fixture: `D:/Juegos/Lucas/zak ega en`, English DOS SCUMM v1. A content scan of every immediate directory under `D:/Juegos/Lucas` found **one ZakClassic copy**. ZakEnhanced is a separate family, not an alternate Classic edition. Alternate-language/release/demo comparison is **blocked by missing copies**; this record does not complete that coverage or raise any registry cell.

The bounded harness reads data only, inventories detected family copies and records index/glyph hashes plus resource/reader facts in `result.json`. The index fingerprint is `E2EA0E3CD72C412665458CFF8C5202BBB7AE2322EDE0D35190485D875CA1356F`; it describes this fixture, not every release. Based on the relevant categories from [prior editions workflow](../editions-20261005/README.md), only available-copy identification, data reading, text/font and version-selection checks were repeated. No new edition branch was needed.

## Commands run (through RTK)

Commands, cwd `.worktrees/zak-classic-s9`:

```
rtk proxy dotnet build .verification/zakclassic-editions-20261018/EditionCheck/EditionCheck.csproj --ignore-failed-sources -p:NuGetAudit=false
rtk proxy dotnet Exe/EditionCheck/Debug/net10.0/EditionCheck.dll D:/Juegos/Lucas
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakClassicEditionTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ZakClassic)"
```

## Results (numbers)

| Edition | Check | Result |
|---|---|---|
| Available English classic | Detection, profile and resource adapter | ZakClassic; `English, Zak McKracken classic`; V1 dialect; ScummV1DataSource |
| Same copy | All indexed room structures and resource inventory | 58 rooms read; 35 costumes, 151 scripts, 110 sounds indexed; this is structural coverage, not a new visual/audio audit |
| Same copy | Own scene text and native font distinction | Script140's own first line decoded; native v1 apostrophe raster differs from v2; hashes in `result.json` |
| Same copy | Display-label independence | Focused regression verifies changing only descriptive Edition metadata cannot select another script dialect; synthetic metadata is not a real alternate language |
| Same copy | S4b–S7 behavior | Reused bounded native play and S7 source/window evidence: 54 groups, 13 starts, five frame limits; no broad rerun justified by a new edition |
| Other Classic languages, releases or demos | Edition-dependent detection/scripts/fonts/graphics/audio comparison | Blocked: no additional local copy |

Build: zero warnings/errors. Two tests pass, zero failures/skips: edition contract and family conformance. Parent owns final full suite.

## Comparison with ScummVM (source files and date; runtime comparison if any)

No new engine behavior changed; source comparison reuses S7 script_v2.cpp review of 18 October 2026. ScummVM was not executed. See the linked S7 evidence below.

## Differences and uncertainties kept

Comparison remains source/explorer evidence only; no ScummVM executable ran. Reuse [S7 record](../zakclassic-cutscenes-20261018/README.md) and [S7 checkpoint](../../docs/archive/checkpoints/20261018-S7-ManiacZak.md) for source comparison and real-window results. Classic eleven estimated costume colours, S5 original-game visual acceptance, darkness/flashlight/walking limits, five capped S7 previews and user timing/listening checks remain unchanged. No data/profile mutation, seed, catalog, forced story state or junction. Registry unchanged because no missing feature was implemented and no alternate edition was verified. Next edition checks require an actual additional Classic copy; no new user retest is needed for this test/document-only pass.

## User checks (exact steps and where; none if none)

None new for this test/document-only pass; prior S7 timing/listening and visual checks remain open.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), [edition index](../../docs/zak/README.md), [existing source map](../../docs/zak/ZAK_SCRIPT_MAP.md). No new script or engine fact was introduced.
