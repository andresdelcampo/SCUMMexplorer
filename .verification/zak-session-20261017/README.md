# Zak Enhanced S4b: own-script boot verification

| | |
|---|---|
| Family / stage | ZakEnhanced / S4b |
| Fixture and edition | `D:/Juegos/Lucas/zakE ega en` (DOS enhanced, English; no Games junction) |
| Date and author | 2026-10-17, Zak worktree agent |
| Commits | Shared source foundation `632a0d7`, executor `494ebc0`/`e3a0fc4`, session `a568445`/`2797a4d`; sound `8873073`/`e9196fd`; object dispatch `a560392` |
| Registry change | Unchanged: S4b is a prerequisite, not a published feature cell |

## What was done

The reproducible `BootTrace` harness reads the fixture's global and room scripts (`dump`), traces v2 sound control (`sound`), or runs the generic session (`boot`). It never edits game variables. The first dump exposed the existing actor palette/name decoding gaps; these were reported to the shared v2 owner.

The adapter audit reads every room, entry/exit, object verb and object image: 58 rooms, 780 indexed object states, global capacity 155 and 148 present scripts. Room 19's duplicate object 384 and one-object script directories exposed shared reader issues, now corrected by the shared owner. The decoded later opening path is recorded separately from execution in the script map.

Immediate v2 object dispatch preserves the source's ordinary/FD/FA flag combinations and replaces only a matching object slot. FE stops every slot of that object, including nested callers, without stopping a same-numbered global script. The two focused slot tests cover flag separation, replacement and cancellation; actively executing replacement pointer continuation is explicitly unresolved.

`ScummPcSpeakerAudio.ReadV2Lifetime` runs the existing Player_V2 control interpreter without PCM synthesis. It records the sound script's channel-zero music timer, including decreases, and proves loops only when the complete control state repeats. The existing sound clock accepts these optional timelines; older formats keep their existing behavior. Native duration excludes PCM filter release. A truncated script or exhausted bound remains Unknown.

## Commands run (through RTK)

From the isolated Zak worktree:

```powershell
rtk proxy dotnet run --no-restore --project .verification/zak-session-20261017/BootTrace -- dump "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/scripts.txt
rtk proxy dotnet run --no-restore --project .verification/zak-session-20261017/BootTrace -- sound "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/sounds.txt
rtk proxy dotnet run --no-restore --project .verification/zak-session-20261017/BootTrace -- boot "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/boot-trace.txt
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummV2SoundLifetimeTests|FullyQualifiedName~LoomSoundClockTests|FullyQualifiedName~LoomPcSpeakerAudioTests" --verbosity quiet
rtk proxy dotnet test tests/SCUMMexplorer.Tests --no-restore --filter "FullyQualifiedName~ScummV2ObjectSlotTests|FullyQualifiedName~ScummZakEnhancedSessionTests|FullyQualifiedName~ScummV2SoundLifetimeTests|FullyQualifiedName~ScummV2ExecutorTests|FullyQualifiedName~ScummV2SessionVariableTests|FullyQualifiedName~LoomSoundClockTests|FullyQualifiedName~LoomPcSpeakerAudioTests" -v minimal
```

## Results (numbers)

Sound control and LOOM clock/speaker regression: 12 passed, 0 failed. Zak's Sound 81 finishes after 2,857 ticks at 236 Hz (5,714 common-clock callbacks); Sound 95 finishes after 20,545 ticks (41,090 callbacks) and supplies timer values through 42. Synthetic tests also pin a timer decrease, exact recurring-state loop detection and truncated-code rejection.

Final integrated targeted suite: **34 passed, 0 failed, 0 skipped**, including Zak adapter/blocked-boot assertions, v2 object slots, executor and session variables, sound timelines and existing sound regressions. Parent performs the required full suite after merging current master.

**S4b remains blocked.** With the integrated v2 foundation, the unmodified boot records 0 frames, no room entered, no pending scripts, no player control, and this uncertainty: `Script 01 +0x0000: unsupported v2 verbOps (0x00, [delete var[0x042]]) at +0x003B`. Advancing another 120 frames preserves that unresolved state. The later Room 1 path in Script 108 has only been decoded, never reached. No state was edited or opcode skipped to make progress.

The next bounded task is a source-backed v2 verb-slot table (`o2_verbOps`): deletion addresses raw slot + 1, creation supplies its own slot, while enable/disable looks up a verb ID. The later-version table keyed only by verb ID is not a faithful substitute. Keep S4b open and S5/S6/S7/S9 unstarted until real boot acceptance is met.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source read 17 October 2026: [player_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2.cpp), [player_v2base.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2base.cpp) and [player_v2base.h](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2base.h). `nextTick` uses 236 Hz; for versions before 3, `getMusicTimer` returns channel zero's word at byte 46, written by the sound stream. No ScummVM executable was run.

Object dispatch and verb-table evidence: [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp), `o2_doSentence` and `o2_verbOps`; [script.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp), `runObjectScript`, `runScriptNested` and `stopObjectScript`. Source evidence only, read 17 October 2026.

## Differences and uncertainties kept

- The generic sound clock tracks independent resources; Player_V2 priority and restartable-next-sound arbitration are not represented. These lifetime results establish individual control streams, not competing voices.
- The sound control trace is capped at 180 seconds; exhausting it is Unknown, not evidence of a loop. The all-sounds report includes such cases.
- No original-game observation, listening, picture or timing comparison is claimed.
- Initial harness compilation required corrections to the project-relative path and the resource collection property. A sandbox testhost stalled; the same targeted tests completed with the authorized elevated invocation. NuGet audit endpoint access was unavailable during the initial harness restore.

## User checks (exact steps and where; none if none)

None: the recorded boot is blocked before any room. The harness reproduces the diagnostic without launching a native engine.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [Zak tracker](../../docs/zak/SUPPORT_STATUS.md). Parent orchestrator owns the shared engine notes, roadmap, checkpoint and handoff. Registry cells remain unchanged.
