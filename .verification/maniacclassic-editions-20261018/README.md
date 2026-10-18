# ManiacClassic S9: available edition and compatibility boundaries

18 October 2026. Fixture: `D:/Juegos/Lucas/mmansion ega en`, English Maniac Mansion classic, SCUMM v1. One bounded available-copy pass; **alternate classic releases remain blocked because no additional local copy is present**. `mmansionE ega es` is the distinct ManiacEnhanced family, not a classic-language comparison. No support cell is raised.

| | |
|---|---|
| Family / stage | ManiacClassic / S9 |
| Fixture and edition | `D:/Juegos/Lucas/mmansion ega en` (English classic) |
| Date and author | 2026-10-18, Codex ManiacClassic worker |
| Commits | This family commit; parent scanner correction 274a098 |
| Registry change | Unchanged: no additional classic release is available |

## What was done

Edition × check × result:

| Edition | Check | Result |
|---|---|---|
| English classic reference | Local inventory and content identification | PASS: one classic folder in the supplied root; content-only LFL identity agrees with the scanner; English classic, not a demo |
| English classic reference | Format and resource directories | PASS: V1 reader/engine/dialect; 52 rooms, 23 costumes, 173 scripts and 61 sounds |
| English classic reference | Reviewed startup assumption | PASS: existing fingerprinted native party recipe reaches Room 44 with player control and no active cutscene |
| English classic reference | Own scripts/text and fresh scene assumptions | PASS: own scan 59 triggers/27 groups/25 scenes, English Push/Turn off/Turn on; all four retained S7 tests pass, including three fresh previews |
| English classic reference | Native play and font selection | PASS: two retained native play routes and two charset tests pass; no Spanish preview-label transformation is enabled by this executable |
| English classic reference | Unreviewed menu boundary | PASS: replacing only the in-memory menu descriptor with a non-menu source file rejects preparation before any queued/delivered input, trace or variables change |
| English classic reference | Edition label cannot select conversion code | PASS: an in-memory Spanish label on this same copy still enables no executable-fingerprinted label transform |
| English classic reference | Graphics/audio | Reuse prior S2/S3/S5 evidence; no plausible alternate data to compare, no new image/listening claim |
| Other classic language, demo or patched release | Detection, numbering, startup, text/font, picture and sound differences | BLOCKED: no additional classic fixture; synthetic negative checks are not another release |

## Commands run (through RTK)

[available-copy.txt](available-copy.txt) retains the single harness output, including index/menu/executable SHA256 values and the complete top-level folder inventory. The harness uses production scanner, profile, startup factory and source scanner; it does not author prerequisites or rewrite resources. The negative regression changes only a resource descriptor in memory, uses Room 1 bytes as an intentionally invalid menu and performs no fixture writes.

From `.worktrees/maniac-classic-s9`, all commands through RTK:

```powershell
rtk proxy dotnet run --project .verification/maniacclassic-editions-20261018/Check/ManiacClassicEditionCheck.csproj -- "D:\Juegos\Lucas\mmansion ega en"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummManiacClassicEditionBoundaryTests|FullyQualifiedName~ScummOldEditionTests|FullyQualifiedName~ScummV2CharsetTests|FullyQualifiedName~ScummManiacClassicPlayTests|FullyQualifiedName~ScummManiacClassicCutsceneTests"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~SupportConformanceTests&DisplayName~ManiacClassic"
rtk proxy dotnet build .verification/maniacclassic-editions-20261018/Check/ManiacClassicEditionCheck.csproj --no-restore
```

## Results (numbers)

Final focused baseline: **14 passed, zero failed/skipped**, about 11 seconds; family conformance **1 passed**. The 14 include two new boundary regressions, four existing edition identity rows, two charset checks, two classic native Play routes and four classic scene checks. Harness final build: zero warnings/errors. After parent scanner correction 274a098, both new boundary tests pass again, including the additional stale-note assertion. Parent performs the integrated full suite.

Initial verification errors are retained here: the new test first called the wrong session constructor (compile error), then asserted the tracker’s stale V2 dialect instead of the actual V1 dialect (13 passed/1 failed). Production profile confirms V1 and the assertion/documentation were corrected; neither was a game regression. The first harness build emitted a nullable dictionary-key warning, corrected without changing behavior. The captured output also exposes a stale scanner note claiming scripts/sounds were unread; reported to the parent for its shared correction, with a regression assertion added here.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Checks follow [the edition method](../editions-20261005/README.md), R-14.2/R-14.4 and the [S7 record](../maniacclassic-cutscenes-20261018/README.md). S6 and S7 establish this copy’s own input, object IDs and source offsets; no enhanced offsets are reused. The scan is not a hand-validated catalog. Six of 27 source groups began in the earlier full S7 census; this run reuses that census instead of repeating it. No ScummVM executable was run, so this is not original-game observation.

The reused S6/S7 source evidence covers ScummVM `script_v2.cpp`, `verbs.cpp` and `charset.cpp`, read 17–18 October 2026. No new interpreter behavior is asserted by S9.

## Differences and uncertainties kept

Retained: S5 original-image comparison and darkness/flashlight limits, Room 18’s six intrinsic black slots, walking/timing/audio perception limits, and unsupported Script 174 `getClosestObjActor` after the doorbell scene. Spanish native-font work belongs to the enhanced worker and does not establish classic alternate-edition compatibility. No game files, player profile or registry levels were changed. The parent-owned scanner text correction was integrated; this family authored no shared engine change. S8 and shipped seeds remain opt-in.

## User checks (exact steps and where; none if none)

No new user check for this unchanged reference copy. Earlier S6/S7 visual, timing and listening checks remain open; alternate classic comparison requires another supplied release.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Family tracker](../../docs/maniac/SUPPORT_STATUS.md), [edition index](../../docs/maniac/README.md), [existing source map](../../docs/maniac/MANIAC_SCRIPT_MAP.md). No new decoded script fact was added.
