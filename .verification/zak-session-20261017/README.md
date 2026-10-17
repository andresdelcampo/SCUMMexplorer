# Zak Classic / Enhanced S4b: own-script boot verification

| | |
|---|---|
| Families / stage | ZakClassic and ZakEnhanced / S4b |
| Fixtures | `D:/Juegos/Lucas/zak ega en` (DOS v1 English); `D:/Juegos/Lucas/zakE ega en` (DOS v2 English) |
| Date and author | 2026-10-17, Zak worktree agent |
| Engine baseline | Shared branch through `9937aa6`, actor defaults `022d4c0`, boot-to-control `a01b992` |
| Own shared changes | sound `8873073`/`e9196fd`, object dispatch `a560392`, explicit verb slots `71f6cbf` |
| Registry | unchanged: S4b is a prerequisite, not a published feature cell |

## What was done

The `BootTrace` harness runs each fixture's own boot through the generic session. No game variable, room, answer or game data was forced; no Games junction was created. Source-defined initialization includes Zak's machine-speed value 32767. Script 1 starts Script 108, which finishes the post-dream action and enables input, so the profile uses the existing boot-until-control rule.

The Enhanced adapter audit reads all 58 rooms, entry/exit scripts, object verbs and images, 780 indexed states, and 148 present global scripts (capacity 155). Duplicate object 384 in Room 19 and one-object directories exposed shared reader bugs, fixed before acceptance. Classic uses its own V1 adapter and fixture, retaining the earlier adapter evidence in `.verification/ClassicBoot` and `.verification/maniac-v1-adapter-20261017`.

V2 sound lifetimes run Player_V2 control without PCM, preserving channel-zero music-timer changes. Verb operations address physical slots: delete uses raw slot + 1, create replaces that explicit slot, mode finds the first unsaved matching verb ID. Duplicate IDs stay distinct. Immediate object calls preserve ordinary/FD/FA flags; FE cancels object slots without cancelling same-numbered global scripts.

## Commands run (through RTK)

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/zak-enhanced-s4b`.

```powershell
rtk proxy dotnet run --no-restore --project .verification/zak-session-20261017/BootTrace -- boot "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/boot-enhanced-trace.txt
rtk proxy dotnet run --no-build --no-restore --project .verification/zak-session-20261017/BootTrace -- boot "D:/Juegos/Lucas/zak ega en" .verification/zak-session-20261017/boot-classic-trace.txt
rtk proxy dotnet run --no-build --no-restore --project .verification/zak-session-20261017/BootTrace -- dump "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/scripts.txt
rtk proxy dotnet run --no-build --no-restore --project .verification/zak-session-20261017/BootTrace -- sound "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/sounds.txt
rtk proxy dotnet test tests/SCUMMexplorer.Tests --no-restore --filter "FullyQualifiedName~ScummZakEnhancedSessionTests|FullyQualifiedName~ScummV2VerbTableTests|FullyQualifiedName~ScummV2ObjectSlotTests|FullyQualifiedName~ScummV2SoundLifetimeTests|FullyQualifiedName~ScummV2ExecutorTests|FullyQualifiedName~ScummV2SessionVariableTests|FullyQualifiedName~LoomSoundClockTests|FullyQualifiedName~LoomPcSpeakerAudioTests" -v minimal
```

## Results (numbers)

**S4b done for both fixtures.** Each own opening reaches Room 1 with player control enabled and no open cutscene; 120 further frames preserve that endpoint. No reached unsupported/unmodelled opcode remains on these bounded routes.

| Own fixture | Boot frames | Frame limit hit | Endpoint | After 120 frames |
|---|---:|---|---|---|
| ZakEnhanced English | 1828 | no | Room 1, control on, no cutscene | same |
| ZakClassic English | 1804 | no | Room 1, control on, no cutscene | same |

Both room sequences: `46,0,46,0,58,51,55,49,56,0,0,0,1`. Actor 1 ends at engine pixel coordinates (96,90), costume 1. This is output, not an original-game visual match. Random bedroom scripts and their propagated movement/sound effects remain labelled estimates. The trace compacts repeated random notices to a count and four samples; every other uncertainty is retained.

Sound 81 ends after 2857 control ticks at 236 Hz (5714 common callbacks); Sound 95 after 20545 ticks (41090 callbacks), supplying music-timer values through 42. Earlier sound/LOOM clock tests passed 12/12. Verb regression passed 49, failed 0, with one optional LOOM audit skipped. Parent performs the required full suite against current master.

Final integrated targeted suite above: **46 passed, 0 failed, 0 skipped**, including both independent Zak opening/120-frame stability cases.

## Comparison with ScummVM

Source read 17 October 2026; no ScummVM executable was run:

- [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp): verb slots, immediate object calls, pickup, colors, inventory redraw and sentence behavior.
- [script.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp): object execution, cancellation and nested continuation.
- [verbs.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/verbs.cpp): EGA highlight 14; [resource_v4.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/resource_v4.cpp): inherited 100-slot allocation.
- [player_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2.cpp), [player_v2base.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2base.cpp), [player_v2base.h](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2base.h): 236 Hz control, timer at byte 46, termination and recurring-state loops.
- [object.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp): duplicate lookup and v1/v2 distance units.

## Differences and uncertainties kept

- Bounded S4b acceptance is not all-room objects, playable UI, cutscene catalog or edition parity. S5, verb-based S6, S7 and S9 remain next.
- Individual sound timelines do not implement Player_V2 priority/restartable-next arbitration. Exhausting the 180-second control bound yields Unknown, not proof of a loop. No original-game listening/timing comparison is claimed.
- Inventory-held script execution and replaced actively executing object-slot continuation remain explicit limitations if reached; neither produced an unresolved step on these routes.
- Sentence rendering remains S6 presentation work. Seeded random paths remain estimates.
- Classic S2 stays 24 derived colors / 11 estimates; its cell and missing comparisons are unchanged.
- A sandbox testhost stalled earlier; authorized elevated execution completed. NuGet audit access was unavailable for the harness restore. An attempted simultaneous rebuild/run hit a transient file lock; final traces use one build and sequential runs.

## User checks

None required to reproduce engine acceptance: run the two boot commands above. No native engine or app screenshot comparison was performed.

## Where the facts went

[Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [Zak tracker](../../docs/zak/SUPPORT_STATUS.md), [Enhanced trace](boot-enhanced-trace.txt), [Classic trace](boot-classic-trace.txt). This supersedes the earlier verb-slot blockers here and in `.verification/ClassicBoot` for Zak only. Parent owns roadmap, checkpoint, changelog and handoff.
