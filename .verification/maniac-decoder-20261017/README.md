# Maniac/Zak S4b prerequisite: v1/v2 operands

17 October 2026. Decoder-only prerequisite; no session or support level is claimed.

The actor-name operand previously ended at its subopcode, letting name bytes be mistaken for instructions. The enhanced palette operand also omitted its index byte. Classic v1 uses the actor number instead, so `ScummScriptDialect.V1` now preserves that distinction. Delay reads the complement of the stored 24-bit value.

Engine source: ScummVM master [script_v2.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v2.cpp), read 17 October 2026: `o2_actorOps` (686–725), `o2_delay` (1415–1424). No ScummVM executable was run.

## Validation

From this worktree, through RTK:

```powershell
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummV2OperandDecodingTests|FullyQualifiedName~ScummV1ActorColorsTests|FullyQualifiedName~LoomScriptDisassemblerTests" --verbosity quiet
```

40 passed, 0 failed, 0 skipped. Sandbox testhost communication stalled; the same test command outside the sandbox completed in under a second. NU1900 reports unavailable NuGet vulnerability metadata. Synthetic checks pin instruction boundaries, truncation diagnostics, classic/enhanced palette differences and delay extremes. Local fixtures under `D:\Juegos\Lucas` pin Script 1:

| Fixture | Instructions | Decoded/resource bytes | Unresolved paths |
|---|---:|---:|---:|
| mmansion ega en | 174 | 995/995 | 0 |
| mmansionE ega es | 221 | 1207/1207 | 0 |

Existing Zak colour expectations changed because decoding now reaches instructions hidden after actor names. The regression pins the newly reachable source pairs: costume 1, Script 108 +00D8/+00EC (palette 13); costume 2, Script 102 +0164/+0168 (palette 2); costume 35, Room 44 entry +0099/+0090 (palette 1). Costume 13 remains an estimate: Script 50 +001D sets actor 5's palette 11, but its ordering against the costume's room entry depends on execution. Maniac's accepted costume colours remain unchanged. These are decoded facts, not observed gameplay; no support matrix level changes.

## Actor allocation prerequisite

`ScummActorTable` now accepts an optional actor count and exposes instance `Capacity`/`Contains`; its existing default remains 13 slots (12 usable actors). `ScummProfile.ActorCount` gives Maniac's two families 25, Zak and the existing families 13, following [scumm.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/scumm.cpp) lines 1634–1647. Session/runtime owners connect that parameter separately. Existing static validity remains for legacy callers; version-aware callers must use the instance method.

Validation: the same test command with filter `FullyQualifiedName~ScummActorCapacityTests|FullyQualifiedName~LoomActorTests|FullyQualifiedName~ScummV2OperandDecodingTests` passed 25 tests, 0 failed/skipped. Checks cover all four fixture profiles, actor 24 placement/talk colour, and unchanged default snapshot size. No gameplay fidelity claim.

## Override skip-path follow-up

The v1/v2 `beginOverride` embeds and consumes its three-byte jump. The decoder now follows that jump's target as well as normal continuation, so execution can find the skipped-scene path (`script_v2.cpp`, `o2_beginOverride`, 1505–1511). The original decoder filter now passes 42 tests, including both dialects' distinct normal/skip paths; fixture boot counts above are unchanged.
