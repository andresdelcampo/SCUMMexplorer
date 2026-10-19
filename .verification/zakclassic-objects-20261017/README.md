# Zak McKracken classic S5: first-visit object audit

| | |
|---|---|
| Family / stage | ZakClassic / S5 |
| Fixture and edition | `D:/Juegos/Lucas/zak ega en` (DOS v1, English) |
| Date and author | 2026-10-17, ZakClassic S5 worktree agent |
| Commits | This record's family commit; shared prerequisites identified below |
| Registry change | Objects remains Partial: S6 click responses are not implemented. Costume level unchanged. |

## What was done

The separate classic harness runs this fixture's 58 rooms, records settled object selections, source instruction counts, geometric gaps, actors and unresolved operations. No enhanced-edition run is substituted for classic evidence. The generic preview follows R-5.3a: boot, labelled source-backed arrival without playing the route, then settling. No story flags are forced.

Shared source prerequisites: `a653f84`, `6d54f0f`, `0e0fb78`, `0ab757a`, `bf1baee`, `7672066`, `9eae4fa`; these implement old-layout/state handling, pseudo rooms, duplicate images, live v1 actor loading and initialization, and native arrival-coordinate scaling. Their integration is separate from this family evidence commit.

## Commands run (through RTK)

From `.worktrees/zak-classic-s5`:

```powershell
rtk proxy dotnet build .verification/zakclassic-objects-20261017/RoomAudit/ZakClassicRoomAudit.csproj --ignore-failed-sources -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/ZakClassicRoomAudit/Debug/net10.0-windows/ZakClassicRoomAudit.dll "D:/Juegos/Lucas/zak ega en" .verification/zakclassic-objects-20261017/baseline
rtk proxy dotnet Exe/ZakClassicRoomAudit/Debug/net10.0-windows/ZakClassicRoomAudit.dll "D:/Juegos/Lucas/zak ega en" .verification/zakclassic-objects-20261017/verified
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~ScummZakClassicFirstVisitTests --verbosity quiet
```

## Results (numbers)

Final `verified/` run: **58/58 rooms open, 57 without a detected gap, one retained flag (Room 44), zero errors**. 50 settled, three left (8, 11, 40), five reached the frame bound (27, 28, 32, 36, 42). Objects appear in 36 rooms; the actor layer reports actors in 28. Unknown-camera notices remain in Rooms 3 and 33. Room 44's inactive/untouchable rectangles overlap (432,72), 32×8; no source state was invented to fill them.

Three new classic cases passed (0 failed, 0 skipped), pinning settled active IDs in Rooms 1 (90/95/102), 5 (169/170), and 7 (183–188). They also check untouchability, copy-selection consistency and actual bedroom actor pixels. Both Zak conformance rows passed in the enhanced worktree's common-source run; parent performs the combined suite. Harness and final targeted build had zero warnings/errors. The earlier actor-zero reports remain as failed evidence; after the native initialization fix, the bedroom actor is visibly rendered.

| Sample room | Loaded number | Active / stored | Comparison |
|---|---:|---:|---|
| 23 | 207 | 4 / 10 | selected source doorway layout |
| 36 | 199 | 4 / 7 | selected source doorway layout; frame bound retained |
| 53 | 158 | 8 / 14 | own classic positions, not Enhanced coordinates |
| 8 | 144 | 4 / 27 | selected travel-office layout; room-left capture |
| 14 | 129 | 1 / 21 | conditional lock-dependent writes retained |
| 9 | 146 | 3 / 12 | native arrival walk now (272,120) in runtime pixels |
| 37 | 170 | 4 / 6 | variable-selected alternatives |
| 33 | 182 | 0 / 23 | alternate-layout writes skipped; camera uncertainty retained |
| 27 | 185 | 4 / 19 | background object work; frame bound retained |
| 10 | 10 | 0 / 11 | intrinsic-off loop and conditional scripts |
| 35 | 195 | 2 / 21 | projection branch, not alternate 194 |

Baseline `9bc3ec8`: all 58 rooms failed the v3 index reader used by the old generic path. After `a653f84`: 56 of 58 rooms without a detected gap, Room 44 flagged at (432,72), 32×8 with no usable stored fill; Room 19 failed with duplicate object-image ID 384. The pseudo-room index ignored v2-style hexadecimal operands, and the classic actor layer could not load its costumes, so actor counts were zero. These failed results remain in local `baseline/` and `audit/`.

The native v1 actor bridge `bf1baee` was integrated separately from family acceptance. It obtains live colours from the actor's reached palette, without converting the S2 estimate inventory into a full-support claim.

## Comparison with ScummVM (source files and date; runtime comparison if any)

ScummVM master source, read 17 October 2026: [object.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp), [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp). The sample takes the largest classic entry scripts: 23 (254 instructions), 36 (215), 53 (179), 8 (137), 14 (90), 9 (66), 37 (62), 33 (60), 27 (53), and 10/35 (45 each). Classic offsets come from this fixture, not the enhanced script dump.

No ScummVM executable was run. The sample is a source comparison, not the original-game picture/timing comparison required for full S5 visual acceptance.

The eleven sample pictures were inspected alongside these edition-specific branch readings. Facts and exact offsets are recorded once in the [script map's Classic Rooms rows](../../docs/zak/ZAK_SCRIPT_MAP.md). No claim of original-game visual parity follows from this inspection.

## Differences and uncertainties kept

The geometric gap detector is not a proof that every scene is complete. Seeded choices and arrival assumptions remain labelled; later rooms are state previews. S2 remains 24 derived costumes and 11 labelled estimates; this run does not compare those costumes with a running original engine.

## User checks (exact steps and where; none if none)

Compare the source sample's first-visit pictures with the original game in a later authorized check. No original executable was launched during this run.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), [shared object-state rules](../../docs/engine/SCUMM_OBJECT_STATES.md).
