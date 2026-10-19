# ZakClassic Mars palette context investigation

| | |
|---|---|
| Family / stage | ZakClassic / S2 palette contexts |
| Fixture | `Games/zak ega en`, DOS classic v1 English; actual root `D:/Juegos/Lucas` |
| Date / author | 19 October 2026 / Codex Mars worktree agent |
| Baseline | `5634143c` |
| Registry | Unchanged: Costumes Partial; no original-game comparison or universal palette promotion |

## What was done

Separated the native opening's stored clothing choice from its reached actor palette, traced the decoded Mars introduction and helmet/clothing branches, and preserved the two failed native character-switch approaches. No actor, costume, palette, inventory owner or story flag is assigned by the harness. Room selections are disclosed **state previews**; directly activated object verbs are **exploration actions**. The native input attempts do not establish an earned story route.

Canonical game-source facts belong in the parent's [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md); the tests pin the exact source anchors being handed to that map. The observation-only probe uses the supplied copy's own source through `IScummDataSource` and copied pre-instruction observations. It does not execute ScummVM.

## Commands

Run from this worktree, all through RTK:

```powershell
rtk proxy dotnet build .verification/zakclassic-mars-palettes-20261019/Probe/ZakMarsPaletteProbe.csproj --verbosity quiet -p:NuGetAudit=false
rtk proxy dotnet Exe/ZakMarsPaletteProbe/Debug/net10.0/ZakMarsPaletteProbe.dll boot
rtk proxy dotnet Exe/ZakMarsPaletteProbe/Debug/net10.0/ZakMarsPaletteProbe.dll script 81 108 5 62 59 19
rtk proxy dotnet Exe/ZakMarsPaletteProbe/Debug/net10.0/ZakMarsPaletteProbe.dll room 26 27 30 32 38 39
rtk proxy dotnet Exe/ZakMarsPaletteProbe/Debug/net10.0/ZakMarsPaletteProbe.dll calls 0x00A9 0x05BE
rtk proxy dotnet Exe/ZakMarsPaletteProbe/Debug/net10.0/ZakMarsPaletteProbe.dll playkey:317 frames:2000
rtk proxy dotnet Exe/ZakMarsPaletteProbe/Debug/net10.0/ZakMarsPaletteProbe.dll room:14 object:57:14 frames:10 room:5 object:169:1 frames:20 use:57:169 frames:2400 playkey:317 frames:1400
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ScummZakClassicMarsPaletteTests --verbosity minimal --logger "trx;LogFileName=mars-contexts.trx" --results-directory .verification/zakclassic-mars-palettes-20261019/test-results
```

The build before the final no-build test compiled both Core and tests. Large source dumps, raw instruction observations and generated TRX remain local/ignored. `native-f3.txt`, `native-artifact-switch.txt` and `boot-final.txt` retain the attempts and successful bounded opening observation; no proprietary resource bytes or screenshots are committed.

## Results

**Five tests passed, zero failed/skipped**, five seconds. The unique probe builds with **zero warnings/errors**. The own-copy proofs cover reached opening chronology, the decoded shared costume without palette writes, the conditional Leslie clothing rule, the helmet's variable random domain and the only indirect actor palette writer's exclusion of the companions.

The copied opening binds Leslie's stored clothing variable to 7 before its palette instruction. A later reached native inventory hook writes her displayed palette to 8 while that stored variable remains 7. The bounded opening ends with Melissa and Leslie both in costume 0 in Room 26, palettes 3 and 8. These are reached opening contexts, not pictures of a completed Mars arrival. They establish why the stored clothing choice cannot be substituted for the actor's current palette.

The decoded Mars introduction assigns shared costume 7 to two distinct actors and assigns no palette. The helmet branch defines a conditional random domain rather than one fixed colour. Native v1 palette retention plus the inventory-hook conditions support distinct conditional appearance contexts; the source by itself does not prove that the route reached them. Existing 35-card / 24-derived / 11-estimate totals stay unchanged in this item.

## Failed attempts and limits retained

1. Ordinary native F3 input from the opening, followed by 2,000 frames, does not switch the ego or execute Script 81. Source shows the native switch depends on story/character guards; the probe does not bypass them.
2. A distinct disclosed preparation selects Room 14, directly activates native pickup on object 57, selects Room 5, directly opens its slot, and composes ordinary native Use through verb/inventory/object/sentence clicks. After 2,400 frames, then F3 and another 1,400 frames, neither Script 62 nor Script 81 is reached. This attempt proves neither accepted artifact delivery nor character unlocking. The precise missing native interaction/prerequisite remains unresolved. Both records are retained; the blocker stops after these two approaches.

The prior 18 October Room 26 preview remains failed evidence; it is not repeated as an acceptance attempt. Its source explanation is now known: the entry tests the ego's actual actor room before starting the Mars introduction. Selecting a room as Zak does not establish a Melissa/Leslie arrival.

Investigation setup failures are retained: one log originally used the generic Ego variable rather than v1/v2's slot and was corrected; a compile used a nonexistent V2 Room constant and was corrected to the documented slot; an early stale probe binary ignored an unimplemented `playkey` argument and supplies no native switch evidence. Finally, an unquoted PowerShell logger semicolon let all five tests pass but failed the wrapper after test completion; the final correctly quoted invocation exits zero with its TRX in this record's directory. No failed assertion was weakened.

No original-interpreter picture, random distribution, full Mars play, timing or listening comparison is claimed. No forced prerequisites, direct Script 81 execution, game/profile/ROM/save change, junction, worktree deletion, hand-authored catalog or shipped seed was used. Parent owns integration/matrices, broader regressions, actual windows and the clean checkpoint.

## ScummVM comparison

Official master source read **19 October 2026**, never run:

- [`script_v2.cpp`, `o2_actorOps`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp): v1 writes the actor's numbered palette slot and sets costumes separately.
- [`script_v5.cpp`, `jumpRelative`, `o5_isEqual` / `o5_isNotEqual`, `o5_getRandomNr`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp): old-engine comparisons fall through on true and jump on false; the random instruction samples 0 through its supplied maximum. This confirms the conditional branch/domain reading without executing the unavailable original interpreter.
- v1 palette persistence remains the already recorded source comparison from [`actor.cpp`, `Actor::setActorCostume`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/actor.cpp), read 18 October in the [prior fidelity record](../zakclassic-fidelity-20261018/README.md).

## User checks

None required to finish this bounded source/context item. Original-runtime visual acceptance stays open in the parent checkpoint; nothing here raises a support level.
