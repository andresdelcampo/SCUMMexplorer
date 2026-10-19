# ManiacClassic fidelity: Script 174 continuation and native actor distances

| | |
|---|---|
| Family / stage | ManiacClassic / requested engine-fidelity pass |
| Fixture and edition | `Games/mmansion ega en`, classic English v1; `TestGames.Root` = `D:/Juegos/Lucas` |
| Date and author | 2026-10-18, Codex family worker |
| Commits | Shared query/distance `fa5670f`; family proof/evidence follows |
| Registry change | Unchanged: Cutscenes remains Partial because a source-derived list and this bounded continuation do not establish a hand-validated complete catalog. Other cells already Full within their documented scope. |

## What was done

Ported native `getClosestObjActor` to the existing v1/v2 executor partial, retaining descending inclusive range, strict comparison, 255 sentinel, ties, same-remote-room distance and preview uncertainty. A seven-line dispatch hook in the generic executor retains estimate metadata. No game/room/offset branch was added.

At the capped-Zak investigator's request, normalized `walkActorToActor` approach distance into pixel coordinates in the same v1/v2 partial. Official source applies the eightfold conversion to every operand including FF; later-engine width behavior is not applied here. Parent owns actual Zak capped-scene results, separate from this worker's tests.

The single bounded harness follows the already checked native S6 party/exit/mat/key route, chooses the displayed native Push verb, clicks the actual doorbell rectangle twice, then advances 3000 frames. It never calls VisitRoom/ActivateObjectVerb, writes story variables or changes inventory. A filtered, read-only script observer records actual Script 174 execution. The existing route helper accepts an optional observer solely for these copied observations.

## Commands run (through RTK)

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/maniac-classic-fidelity`.

```powershell
rtk git worktree add .worktrees/maniac-classic-fidelity -b codex/maniac-classic-fidelity codex/maniaczak-fidelity
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummV2ClosestTests|FullyQualifiedName~ScummV2ExecutorTests|FullyQualifiedName~ScummManiacClassicFidelityTests|FullyQualifiedName~ScummManiacClassicCutsceneTests|FullyQualifiedName~ScummManiacClassicPlayTests|FullyQualifiedName~ScummManiacClassicStateTests|(FullyQualifiedName~SupportConformanceTests&DisplayName~ManiacClassic)" --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~LoomBootTests|FullyQualifiedName~LoomCdCutsceneTests|FullyQualifiedName~LoomActorTests|FullyQualifiedName~LoomActorLookTests" --verbosity quiet
rtk proxy dotnet run --project .verification/maniacclassic-fidelity-20261018/Continuation --no-restore -p:NuGetAudit=false
```

The worktree creation command ran at the repository root. Final harness output is [continuation.md](continuation.md). The retained original S7 [doorbell proof](../maniacclassic-cutscenes-20261018/proof-1.txt) records the unsupported Script 174 baseline. Existing S7 `Census script 174` was also used to read the actual global script; the final proof command reused its object-verb source disassembly before the native route harness was finished.

## Results (numbers)

- 45 focused checks passed, zero failed/skipped: 13 new native query/arrival cases; 21 prior v2 executor cases; one genuine continuation; four Classic cutscene cases, two Classic Play cases, three Classic State cases and one Classic family conformance row. Build/restore completed with zero warnings/errors.
- LOOM EGA/CD regressions: **41 passed, zero failed/skipped**, 1m13s (EGA boot/actors/looks and CD source-derived scenes/intro Skip). Final observer-filter-only adjustment was then checked by the one native continuation test again (one pass) and the same harness (PASS).
- Native route: doorbell `(448,32,8,8)` reached with camera left 352; native Push verb 9. Doorbell lines appear at continuation frames 59/100; Ed's outdoor response at 629/680. After 3000 frames: Room 1, player control, no cutscene, two scenes begun, key still held by Dave; Ed has returned to Room 26. Script 174 executes 484 closest queries and reaches its normal stop; the inspected post-query result is FF, handled by its own fallback code. No unsupported-v2 uncertainty remains. [Copied observations](continuation.md) retain seeded preview estimates rather than claiming original-game timings.
- Edge checks pin greatest-id tie, 254/255/256 distance boundary, inclusive/self/inverted range, same/different/zero offscreen room, unknown candidate with no partial destination overwrite, estimated native range propagation, both approach directions, arrival without overshoot and literal FF approach.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official master sources read 18 October 2026: `engines/scumm/script_v2.cpp` (dispatch 66/E6), `script_v5.cpp` (`o5_getClosestObjActor`, `ScummEngine_v5::walkActorToActor`), `vars.cpp` (native range words 19/20) and `object.cpp` (distance/position). Sources and versioned facts live in [engine script map](../../docs/engine/SCUMM_SCRIPT_MAP.md#scumm-v1v2-closest-query-and-actor-approach-distance-18-october-2026). ScummVM was not executed, as instructed. Source execution and original-game observation remain distinct.

## Differences and uncertainties kept

The earlier S7 unsupported result is retained, not rewritten. The first unit-test setup used decimal literals instead of the disassembler's hexadecimal syntax (ten failures); the corrected syntax passed nine cases, while the last unknown-candidate case needed explicit Forget because native initialized actors are known room zero, not unknown. This corrected setup passes all 13 final cases. Initial no-restore build found no worktree assets; ordinary restore/build resolved it. A helper signature initially used List for the shared HashSet of limitations; compile fixed before tests.

The first harness attempt guessed key E as Push and correctly rejected native verb 13. The corrected approach clicks the source-created displayed Push verb. Nullable verb coordinates initially caused compile errors, corrected by explicit unknown guards. The next route reached the expected outdoor response but its assertion incorrectly expected session presentation trace to include every executed instruction; that trace does not. The read-only execution observer now proves Script 174 calls and stops without introducing state. An initial verbose observer log was unnecessarily large; final output summarizes query count and keeps the first 45 copied entries.

No complete-story, original picture/timing/audio acceptance, walk-box metric replacement, palette completion or new edition is claimed by this item. Earlier S5 visual findings and seeded-path estimates remain. Source scene starts, selected party and exact message frames are Explorer execution evidence only. Game data, profile, ROM and saves were not changed. No junction was created, no worktree removed, no full suite run by this worker; parent performs final integration protocol.

## User checks (exact steps and where; none if none)

Optional next interactive check: open the classic English fixture, select Syd and Bernard, Start, skip the introduction at its actual skip point, walk left to the front door, lift the mat/take the key, select Push and click the doorbell. Let Ed walk outside, say both lines and return; continue walking afterward. Perceived timing and original-game comparison remain unconfirmed, with no new approval gate.

## Where the facts went (links to the script map / engine notes / tracker entries)

Engine rules: [SCUMM_SCRIPT_MAP](../../docs/engine/SCUMM_SCRIPT_MAP.md#scumm-v1v2-closest-query-and-actor-approach-distance-18-october-2026). Parent integrates game-specific source rows and decision log in [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md) / [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), and owns checkpoint/current work/changelog/matrices. Suggested game row: Script174 +0000 reads closest into C9 from C8; +0003 branches on FF, +0009/+0016 uses actor rooms and +002B copies fallback/result into C8, +002E stops; evidence decoded+play, fixture's own Script174 and native 3000-frame doorbell continuation, 2026-10-18. No new requirement is needed.
