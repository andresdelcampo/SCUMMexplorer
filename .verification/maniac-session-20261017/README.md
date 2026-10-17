# ManiacEnhanced S4b bounded session — 17 October 2026

Result: **incomplete; no registry cell raised**. The v2 source, dialect, engine slots and bounded interpreter now execute the enhanced boot and real party selection, but the first gameplay room has not returned player control.

Fixture: `D:/Juegos/Lucas/mmansionE ega es`, detected ManiacEnhanced/v2. Own index declares 179 script slots and 780 objects. The all-room adapter regression reads every room's objects, entry/exit scripts, verb entries and images without the old-v3 offsets. It preserves all directory records; repeated object IDs resolve verb code from the last record, matching `getObjectIndex`.

## Reproduce

From the repository/worktree root:

```
rtk proxy dotnet run --project .verification/maniac-session-20261017/BootTrace --no-restore -- "D:/Juegos/Lucas/mmansionE ega es" select
```

The constructor advances 5000 frames to the still-waiting Room 45 party menu. The harness explicitly assumes room clicks `(76,104)` on Syd/object260, `(196,104)` on Bernard/object263 and `(292,60)` on Start/object403, advancing 100 frames after each and then at most 5000 further frames. It sends genuine fixed-script-4 input; it does not write selection/story variables. Own Script114 and object403 establish party slots48=1,49=5 and selected count111=3. This is one labelled user route, not a source-established default party.

Final observed route: `45 ? 0 ? 33 ? 49 ? 44`. Final room44, control=false, cutscene=false, character count variable7=32. Script131 completes the opening; Script164 stops at `+0x0004` with unsupported v2 `verbOps (0x00, [delete var[0x06F]])`. Explicit old verb-slot creation/deletion semantics remain the blocker before player control. Frozen Script177 is retained. The character-counter diagnostic found two integration bugs that are now fixed: old text operands needed normalization, and the native counter had to be imported back into executor state after Print. ScummVM displays complete chunks and preserves character count across FF03 boundaries; incremental rendering was not required.

The saved `boot-trace.txt` records constructor/menu and final endpoint separately. The initial `endCutscene` also reports that the ego's initial room/position is unresolved; it is not silently replaced with a made-up room. Seeded random choices and their downstream transitions/sounds remain estimates. Conservative v2 estimate propagation can mark the remainder of a bounded run conditional after an estimated operand read.

## Source and scope

Read-only ScummVM source comparison, master retrieved 17 October 2026; ScummVM was never executed:

- [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp): arithmetic/indirection/bit words, unsigned reversed relational comparisons, named-state branch polarity, actor units, camera units, intrinsic object drawing, sentence dispatch and waits.
- [vars.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/vars.cpp): separate v1/v2 engine slots.
- [object.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp): old object headers, reverse duplicate lookup, intrinsic-mask parent checks and untouchability.
- [resource_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/resource_v2.cpp), [room.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/room.cpp): directories and fixed entry hook.

New opcode execution lives in `ScummBoundedScriptExecutor.V2.cs`; shared executor changes are guarded/additive. Source instruction observations occur before operand normalization. The raw object code address space and declared entry offset are preserved, including backward branches.

Remaining limits include v2 explicit verb-slot operations (Zak's first blocker), sentence UI rendering, unretained actor name/sound presentation fields, complete classic-specific cutscene semantics, independent sound voices without Player_V2 priority/restart arbitration, and bounded sound-lifetime analysis. Sound56 exceeded the native lifetime analysis bound in an earlier nonconditional diagnostic; current route reports estimated sound timing instead. S5 first-visit object acceptance, S6 verb play, S7 cutscene acceptance and S9 remain unattempted.

## Validation

Final focused command: `rtk proxy dotnet test tests/SCUMMexplorer.Tests --no-restore --filter "FullyQualifiedName~ScummManiacEnhancedSessionTests|FullyQualifiedName~ScummV2ExecutorTests|FullyQualifiedName~ScummV2ObjectSlotTests|FullyQualifiedName~ScummV1DataSourceTests|FullyQualifiedName~LoomBoundedScriptExecutorTests" --verbosity quiet`.

39 passed, 0 failed, 0 skipped. Includes exact enhanced endpoint/party regression, all enhanced rooms, classic adapters, special object dispatch, unsigned/state/bit/indirect/coordinate semantics and existing old-v3 executor checks. NU1900 reflects unavailable NuGet vulnerability metadata. Parent orchestrator performs the required master merge/build/full suite separately.

