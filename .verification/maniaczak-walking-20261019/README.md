# Maniac/Zak native walk-box geometry

| | |
|---|---|
| Families / item | ManiacClassic, ManiacEnhanced, ZakClassic, ZakEnhanced / native walking fidelity |
| Fixtures | `Games/mmansion ega en`, `Games/mmansionE ega es`, `Games/zak ega en`, `Games/zakE ega en` |
| Date / author | 19 October 2026 / Codex walking worker |
| Baseline | `ce405e7b`; branch `codex/maniaczak-walking-20261019` |
| Registry change | None: this fixes an engine approximation; original-game acceptance and the remaining matrix gaps stay separate |

## What was done

Replaced the v1/v2 Euclidean edge projection with native row-preserving binary edge search and weighted unsquared distance. Old actors now use one descending-box search with an FF bound, coarse zero-distance early return, strict nearest comparison and unchanged requested point on failure. The v3 last-valid fallback and threshold passes remain confined to the later path. Native crossing-point calculations use the same old geometry. V1/v2 also recompute the destination box on each walk request, including points inside the previous destination box; the later-engine cache shortcut remains.

Implementation is a new `ScummV2WalkBoxGeometry.cs`, two delegations in `ScummWalkBoxes.cs`, and one `_v2Animations` guard in `ScummActors.cs`. No session, executor, profile, App, palette or game-specific table changed.

Thirteen new test cases pin row clamping, weighted choice, coarse zero priority, equal-distance ties, FF/empty-box failure, interior crossing points, native visibility flags, both old actor cache paths, and real approach coordinates independently from every copy. Hand-picked expected points come from the native source rule and each copy's actual box bytes; the probe's separate transcription also checks every aligned room-grid position and every declared object approach.

## Commands run (through RTK)

From `.worktrees/maniaczak-walking-20261019`:

```powershell
rtk proxy dotnet run --project .verification/maniaczak-walking-20261019/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet run --project .verification/maniaczak-walking-20261019/Probe/Probe.csproj -p:NuGetAudit=false --verbosity quiet -- "D:\Juegos\Lucas" palette
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter FullyQualifiedName~ScummV2WalkGeometryTests --verbosity quiet -p:NuGetAudit=false
```

The final combined filter includes the new geometry tests; existing v2 player-box tests; LOOM v3 walk-box/v4 walking guards; all four Maniac/Zak Play routes; both Zak Flight suites; character artwork/admin inventory; Classic Maniac doorbell fidelity; and both Zak fidelity suites. Local TRXs are under `test-results/`; the baseline/after/remaining-palette source probe outputs are `before.txt`, `after.txt`, `palette-audit.txt`. Output files are ignored; code and this record are committed.

## Results (numbers)

Source-reference census, player-box eligibility, inclusive aligned native grid over each room's declared dimensions:

| Fixture | Grid points | Before differing | Object approaches | Before differing | After differing, both categories |
|---|---:|---:|---:|---:|---:|
| ManiacClassic | 246,330 | 86,939 | 450 | 62 | 0 |
| ManiacEnhanced | 226,005 | 78,119 | 460 | 59 | 0 |
| ZakClassic | 270,140 | 67,528 | 757 | 121 | 0 |
| ZakEnhanced | 267,150 | 67,758 | 762 | 123 | 0 |
| Total | 1,009,625 | 300,344 | 2,429 | 365 | 0 |

These are comparisons against independently transcribed source arithmetic, not original-game runtime or image acceptance. Declared approach points include helper/decorative headers, so the counts are geometry coverage, not a count of playable puzzles.

Real regression examples: Classic Maniac Room1 approach `(440,88)` now selects `(440,92,box4)`; enhanced own layout uses `(352,72)` → `(352,84,box4)`. Both Zak copies' Room3 approach `(792,104)` changes from `(808,106,box7)` to native `(816,104,box7)`; `(744,104)` changes from `(736,104,box8)` to `(736,114,box10)`.

Initial seven geometry cases fail on baseline. Geometry/LOOM walking checks pass 29 cases after the correction; the cache seam has two further reproduced baseline failures. **Final combined run: 85 passed, zero failed/skipped, 2m15s**, `test-results/walking-final.trx`; all thirteen new cases execute. Parent owns conformance, full-suite, window verification and integration.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official [`actor.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/actor.cpp#L1707-L1873), read 19 October 2026: `checkXYInBoxBounds` searches the requested row, measuring distance in coarse square blocks; `Actor_v2::adjustXYToBeInBox` selects boxes in descending order. Its `startWalkActor` path also recalculates old destinations rather than reusing the later-engine shortcut. The port preserves these version rules with room pixels converted at the boundary. Native execution was not observed; ScummVM was never launched. The existing ScummVM GPL attribution applies; parent updates the explicit helper path/date in shared notices.

## Differences and uncertainties kept

The first post-fix geometry run had one failure: its supposed equal-distance synthetic boxes were not equidistant. Correcting the fixture to equal two-unit distances makes the native strict-tie assertion meaningful. No production code changed for that mistake.

The first broad regression run had 73 passed / one failed: enhanced Maniac's historical fixed camera expectation was352, while native projections now leave ego at496 and camera336 after the same two screen-left clicks. The test still executes its native mat/key route using current ViewLeft; its expected camera value is now336. A subsequent fidelity run had ten passed / two failed because the completed Room14 crystal scenes take905 frames in both editions, two fewer than907; both still begin, return without failure and enter Room16. Exact duration expectations are updated, with no cap increase or bypass. Failed TRXs remain distinct from acceptance.

No claim of an earned bus route, original-game walking pictures, audiovisual equivalence or complete-story support. No game/profile/save/ROM mutation, junction, worktree removal, stash, force, push, catalog or seed.

The read-only palette audit reconfirms 111 settings, 19 variable operands, and estimates4–9/12–15/32. Those are context/order uncertainties: clothing transformations, two Mars actors and dynamic C4, actor5/6 forms, and initial/late boss colors. The preceding [palette context evidence](../zakclassic-fidelity-20261018/README.md) already demonstrates differing reached colors (boss0 before7; actor5 preview0 versus a prior palette11). Next useful work with available data is to prove and expose source-specific variants through native transformation/Mars paths, not replace all eleven cards with one sampled color. No palette value or support level changed here.

The parked bus attempt's old sentence composition is retained in the [flight review](../zak-flight-review-20261018/README.md). This geometry correction demonstrably changes Room3 approaches; the travel owner is notified to test that path through native input. It does not itself establish fare payment or an earned airport arrival.

## User checks (exact steps and where; none if none)

Parent owns actual-window acceptance. Existing original-game comparison remains unavailable under the standing instruction not to run ScummVM; additional same-family releases are still missing. No repeated subjective timing/listening check is requested.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent integrates source facts, notices and current scope into [engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md), [Maniac and Zak support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries) and the run checkpoint. This worker edits only its leased engine paths, tests, probe and evidence.
