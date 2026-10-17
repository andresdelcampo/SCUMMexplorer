# Zak McKracken enhanced S5: first-visit object audit

| | |
|---|---|
| Family / stage | ZakEnhanced / S5 |
| Fixture and edition | `D:/Juegos/Lucas/zakE ega en` (DOS v2, English) |
| Date and author | 2026-10-17, ZakEnhanced S5 worktree agent |
| Commits | This record's family commit; shared prerequisite identified below |
| Registry change | Unchanged: Objects remains Partial until S6 supplies click responses. |

## What was done

The reproducible RoomAudit harness runs every indexed room through the generic first-visit path and writes the scene, source instruction counts, active image IDs, gap results, actor layer and unresolved operations. It also dumps the fixture's own global and entry scripts for the source comparisons. This is a state preview under R-5.3a: rooms other than the boot endpoint are entered without playing their route. No story variables are forced.

Shared prerequisites are `a653f84` (old layouts and intrinsic-state semantics), `6d54f0f` (profile visibility), `0e0fb78` (hexadecimal pseudo numbers and room-scoped settling), `0ab757a` (duplicate image entries), `bf1baee`/`7672066` (native actor lifecycle), and `9eae4fa` (arrival walks in runtime pixels). The arrival bug was exposed by this audit: Room 9's native walk (34,60) was treated as pixels; it must become (272,120). No game-specific coordinate rule was introduced.

## Commands run (through RTK)

From `.worktrees/zak-enhanced-s5`:

```powershell
rtk proxy dotnet build .verification/zakenhanced-objects-20261017/RoomAudit/ZakEnhancedRoomAudit.csproj --ignore-failed-sources -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/ZakEnhancedRoomAudit/Debug/net10.0-windows/ZakEnhancedRoomAudit.dll "D:/Juegos/Lucas/zakE ega en" .verification/zakenhanced-objects-20261017/baseline
rtk proxy dotnet Exe/ZakEnhancedRoomAudit/Debug/net10.0-windows/ZakEnhancedRoomAudit.dll "D:/Juegos/Lucas/zakE ega en" .verification/zakenhanced-objects-20261017/verified
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummZakEnhancedFirstVisitTests|FullyQualifiedName~ScummZakEnhancedSessionTests|FullyQualifiedName~SupportConformanceTests&DisplayName~Zak" --verbosity quiet
```

## Results (numbers)

Final `verified/` run through shared `9eae4fa`: **58/58 rooms opened; 55 without a detected gap, three retained gap flags (13, 16, 44), zero errors**. 50 settled, three left (8, 11, 40), five reached the 2400-frame bound (27, 28, 32, 36, 42). Objects appear in 37 rooms and the actor layer reports actors in 28. Unknown-camera notices remain in Rooms 3 and 33; the arrival scaling fix removed the corresponding notices in 31, 43 and 44. These remaining notices and bounded outcomes are not silently accepted as a complete original game run.

Final targeted validation: **8 passed, 0 failed, 0 skipped**, covering the three new room-state cases, existing Zak session/data tests and both Zak conformance rows. Harness build: zero warnings/errors. Room 1 retains active IDs 90/95/102, Room 5 169/170, Room 7 183–188; the tests also check old untouchability, copy-selection consistency and actual bedroom actor pixels. Matrix remains unchanged. Parent performs the combined build/full suite.

| Sample room | Loaded number | Active / stored | Outcome |
|---|---:|---:|---|
| 23 | 207 | 4 / 10 | settled |
| 36 | 199 | 4 / 7 | frame limit |
| 53 | 158 | 8 / 14 | settled |
| 8 | 144 | 4 / 27 | room left |
| 14 | 129 | 1 / 21 | settled |
| 9 | 146 | 3 / 12 | settled |
| 33 | 182 | 0 / 23 | settled, camera uncertainty |
| 37 | 170 | 4 / 6 | settled |
| 35 | 195 | 2 / 21 | settled |
| 27 | 185 | 4 / 19 | frame limit |

Baseline on master `9bc3ec8`: all 58 rooms failed the generic first-visit path with `Truncated old-v3 object-state table`. That path was still using the v3 index reader for the v2 format. No initial gap count was obtained; zero reported baseline gaps does not mean zero actual gaps.

First run with shared prerequisite `a653f84` (local cherry-pick `c47145a`): 54 of 58 rooms without a detected gap, three flagged rooms (13, 16, 44) and one error (19, duplicate image ID 384). The pseudo-room index was still ignoring v2 hexadecimal operands; Rooms 23 and 36 consequently selected no object images. This failed evidence remains in local `audit/`; it is not acceptance evidence.

The copied audit initially used the later-engine whole-state mask for its index-only comparison. After correcting that comparison to the scene's intrinsic mask 8, all three flagged rectangles also exist in the index-only composition (`gap-details/`). Room 13: object 288, state 2, at (232,104), 8×8. Room 16: object 334, state 2, at (336,0), 48×8. Room 44: objects 81/653/655, states 3/2/2, at (432,72), 32×8. These are source-inactive, untouchable records; forcing them on to satisfy the detector would invent state. The pictures were inspected, but their intended black areas are not asserted as an original-game match.

## Comparison with ScummVM (source files and date; runtime comparison if any)

ScummVM master source read 17 October 2026: [object.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp), [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp). The ten-room source sample is selected by the largest entry instruction counts (v2 has no local-script directory): 23, 36, 53, 8, 14, 9, 33, 37, 35, 27. The boot bedroom (1) is an additional regression target.

No original ScummVM executable was run. These are source comparisons, not original-game picture or timing comparisons.

The rendered sample was inspected alongside the edition's decoded branches. Sources and offsets live in the [script map's Rooms table](../../docs/zak/ZAK_SCRIPT_MAP.md), with ten Enhanced rows added for this audit. Pseudo layouts 23/36 now select their doorway images; the initial zero-object pictures were rejected. The branch-dependent omissions in 14/33/35 are retained because other pseudo layouts' writes do not run here. Room 53's additional ornaments are for pseudo 156, not the selected 158. This validates the specified branch relationships; it does not establish a played route or native visual parity.

## Differences and uncertainties kept

Seeded arrival and random branch choices remain labelled assumptions. A settled preview is not proof of the earliest played route or an original-game picture match. The colour-0 rectangle gap detector is a limited geometric check, not proof that every room detail is correct.

## User checks (exact steps and where; none if none)

The original-game comparison required by S5 is not performed in this run because executing ScummVM is prohibited. Compare the ten-room sample above against the game's first visits in a later authorized visual check.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [tracker](../../docs/zak/SUPPORT_STATUS.md), [shared object-state rules](../../docs/engine/SCUMM_OBJECT_STATES.md).

