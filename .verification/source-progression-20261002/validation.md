# LOOM source progression — 2 October 2026

Continued after `1d66026` (scythe background) and `8f6d5a5` (status/costumes), preserving their behavior and all unrelated work. Completed implementation `8d12d0f` supports generic challenge expressions/native intro input. Separate source recipe commits: `b17c53d` (096) and `f18a18f` (both Room20 responses). No replay promotion, game-data edits, explorer status edits or staff browsing floors were used to establish prerequisites.

## Room41 casting and090 certainty

The previous native recipe was rerun from fresh sessions. Staff9/271use,10/295use/052, reached13 native325 double-click/025(4), direct13/323use for raw5 and direct28/4703D for raw6 remain explicit exploration choices. Catalog37 then direct603use executes the actual source arrival into41 (46 settling frames), replacing catalog41's assumed arrival. Wait42 more frames for632; native632 look; wait451 for633. Native floor at decoded633 walk point177,47 for40 frames, aim168,44 for1 frame, then loaded slot4 note inputs at8-frame gaps dispatch633/41. No note sequence or puzzle bit was written by the probe.

Actual source arrival does not eliminate inherited draft certainty. Event recording shows026 +0071 and +007F both Conditional; +007F also carries estimated argument index1 for response41.633/41 +0000 starts204 Conditional, and090 opens Conditional +0000 before its Conditional load42 +0005 is refused. Local213/204 end, but090 has no end:12 begins/11 ends after3000 frames, final41. Waiting for204's actual override (59 frames) and pressing Escape still has the same090 outcome. Native input is established; native090 completion is not.

A separate direct-response control uses the same setup/active633 and calls633/41 directly. It waits40 frames for204's actual override, sends Escape, then settles1227 frames.090 reaches its source end +0730 in43,12/12 scopes. This localizes the remaining native dependency to inherited input/draft certainty rather than a missing090 implementation. Direct invocation and the override are not evidence of a native cast completing090.

[Room41 state/events/hashes](room41-summary.json), [native recipe](native41.json), [native override](native41-skip.json), [direct control](direct41-skip.json). Full packets are ignored under `native41-source-1/`, `native41-skip-3/`, `direct41-skip-1/` in the current output directory.

##096 and both Room20 responses

[096](late096-validation.md): source43/639use reaches44;653look starts095; after2 frames Escape executes its recorded override0720.095 writes known D7B/DA3 and loads45.656look obtains the staff,655use/092 returns43;639use then completes096 in46,146 settling frames,4/4 scopes. Full095 control still has estimated flags and remains43,4/3 after3000. No known-state/actor injection.

[Room20](room20-validation.md): actual16/350 response47 sleep writes D8F=1 in117 frames. From catalog18,369look/062 or371look/056 now establishes a settled20 arrival (888/967 frames), with source-placed ego. Both390/48 and391/48 finish206 and the subsequent return18, seven balanced scopes for all four combinations. Fresh catalog20 still lacks that arrival and retains both original incomplete roots. Longer prerequisites do not remove fresh-entry gaps.

## Native challenge and level4 dependency

The new `stopAtBootInput:true` starts001/Room69 and awaits input rather than an assumed challenge answer. Native practice-choice876 click plus32 frames executes201 expressions0022/0049/0056 and generates `[0,4,9,1]` in this seed0 run. Glyphs are source IDs0..11 (886..897), not distaff note IDs1..8. Four actual glyph clicks dispatch203/205 and the source welcome branch;205 writes D6B=1. Seed/timer/pointer provenance remains estimated, so001/044 does not reach an opening room: after5000 frames still69, no open/begun scope. D6F remains0. No boot gate was manufactured. [Expression implementation](expression-validation.md), [native challenge state/events](native-challenge-summary.json).

**Correction:** D6F is set1 by203 on a wrong answer; native level4 gates require D6F=0 (`equalZero`, branch on false). The prior handoff's claim that it must be1 was wrong. It is also not the sole prerequisite. Egg270/3A loads slot0 via023, marks it used via034, then requires the used flags for slots2 and1 before029(4,10). Equivalent source gates exist in06/local200 and08/local217.034's used bit is4000; learned2000 is separate.

Fresh native egg input initially finds270 untouchable; after executing08/255use/046 (2519 settling frames), native walk-point/look/aim can hit270. Keyboard notes using loaded slot0 match022→026→007→270/3A; this is real native response dispatch. Full047 changes rooms and ends the frozen room-owned270 caller before its remaining gate code, leaving raw3. Waiting22 frames for047's actual override and pressing Escape before its room changes lets270 continue, execute023(0)/034(0)/023(2), and stop at the missing slot2-use prerequisite. Raw remains3; slots1/2 have not been established as used. No level4 completion claimed. [Native egg controls](native-level4-summary.json), [source override recipe](level4-skip.json).

Next native work: establish slots1/2 learning/use through their actual source owners/input; inspect conservative packed-word/response provenance at023/026 before claiming native090. Do not clear estimated sets, write used bits, supply known flags or substitute page edits. The native challenge answer path remains conservative; a reached opening from it is still unestablished.

## Regression and reproduction

Fresh Core289/289; visible WPF62/62; doorways16/16; controls19/19; input18/18. All363 roots match source fingerprints, statuses, exact activity timing, rooms and scope traces; four memberships and both sequences match the scope baseline. Blockers has zero blocked random sites. RoomAudit76 has zero gaps, all76 current PNGs unchanged. Final main Release build zero warnings/errors, executable `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. [Hashed reports](check-summary.json), [regression commands](expression-validation.md). Synthetic input/visible normal-clock checks, not physical gameplay/listening.

Main cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`, master.334 starts=84 scenes/250 reactions;168/235 sites,50 outstanding owners,123 unresolved edges. Fresh363:330 complete,4 incomplete,19 no-start,10 brief. All existing gaps remain. No user app stopped, no push, no game/scummvm changes. Unrelated `.verification/object-click-room17/`, `.verification/room-object-interaction/`, `tests/SCUMMexplorer.Tests/LoomObjectLookAuditTests.cs` preserved. Session token-usage tool unavailable; account limits were not used as session usage.

Run from the main cwd with required absolute RTK `proxy`; Python uses bundled runtime with `-X utf8`. Every rerun must use fresh output. Probe reflection is read-only; it records inputs, actual room changes, pending slots, overrides and event certainty. `require-room` prevents wrong-room response calls; settling requires balanced scopes, idle speech/ego/camera and inactive draftD66 for32 frames.

```powershell
python -X utf8 .verification/source-progression-20261002/create_probes.py
dotnet run --project .verification/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/source-progression-20261002/native41-skip.json Exe/SourceProgressionSession-20261002/native41-fresh
dotnet run --project .verification/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/source-progression-20261002/direct41-skip.json Exe/SourceProgressionSession-20261002/direct41-fresh
dotnet run --project .verification/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/source-progression-20261002/late.json Exe/SourceProgressionSession-20261002/late-fresh
dotnet run --project .verification/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/source-progression-20261002/room20.json Exe/SourceProgressionSession-20261002/room20-fresh
dotnet run --project .verification/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/source-progression-20261002/boot.json Exe/SourceProgressionSession-20261002/boot-fresh
dotnet run --project .verification/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/source-progression-20261002/level4-skip.json Exe/SourceProgressionSession-20261002/level4-fresh
```

`summarize.py <packet-folders...>` creates `route-summary.json`; `finalize_evidence.py` groups that generated summary and retains all source packet hashes. Its final deletion affects only that generated intermediate. [Historical packet recipes/hashes](historical-packets.json) preserve failed/early controls: old native-boot mode had no actual room; early glyph guard wrongly rejected zero; early204 waits missed the real override; egg-before-trial lacked a touchable target; full095/native090/raw3 egg outcomes remain controls. These failures are not promoted as successes. No full game code/text/images/binaries are tracked.
