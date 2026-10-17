# Zak Enhanced S4b: own-script boot verification

| | |
|---|---|
| Family / stage | ZakEnhanced / S4b |
| Fixture and edition | `Games/zakE ega en` (DOS enhanced, English) |
| Date and author | 2026-10-17, Zak worktree agent |
| Commits | Shared source foundation `632a0d7`; sound lifetime in this record's commit |
| Registry change | Unchanged: S4b is a prerequisite, not a published feature cell |

## What was done

The reproducible `BootTrace` harness reads the fixture's global and room scripts (`dump`), traces v2 sound control (`sound`), or runs the generic session (`boot`). It never edits game variables. The first dump exposed the existing actor palette/name decoding gaps; these were reported to the shared v2 owner.

`ScummPcSpeakerAudio.ReadV2Lifetime` runs the existing Player_V2 control interpreter without PCM synthesis. It records the sound script's channel-zero music timer, including decreases, and proves loops only when the complete control state repeats. The existing sound clock accepts these optional timelines; older formats keep their existing behavior. Native duration excludes PCM filter release. A truncated script or exhausted bound remains Unknown.

## Commands run (through RTK)

From the isolated Zak worktree:

```powershell
rtk proxy dotnet run --no-restore --project .verification/zak-session-20261017/BootTrace -- dump "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/scripts.txt
rtk proxy dotnet run --no-restore --project .verification/zak-session-20261017/BootTrace -- sound "D:/Juegos/Lucas/zakE ega en" .verification/zak-session-20261017/sounds.txt
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummV2SoundLifetimeTests|FullyQualifiedName~LoomSoundClockTests|FullyQualifiedName~LoomPcSpeakerAudioTests" --verbosity quiet
```

## Results (numbers)

Sound control and LOOM clock/speaker regression: 12 passed, 0 failed. Zak's Sound 81 finishes after 2,857 ticks at 236 Hz (5,714 common-clock callbacks); Sound 95 finishes after 20,545 ticks (41,090 callbacks) and supplies timer values through 42. Synthetic tests also pin a timer decrease, exact recurring-state loop detection and truncated-code rejection.

Boot acceptance is pending shared v2 executor/session integration; no successful boot is claimed by these sound checks.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Source read 17 October 2026: [player_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2.cpp), [player_v2base.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2base.cpp) and [player_v2base.h](https://github.com/scummvm/scummvm/blob/master/engines/scumm/players/player_v2base.h). `nextTick` uses 236 Hz; for versions before 3, `getMusicTimer` returns channel zero's word at byte 46, written by the sound stream. No ScummVM executable was run.

## Differences and uncertainties kept

- The generic sound clock tracks independent resources; Player_V2 priority and restartable-next-sound arbitration are not represented. These lifetime results establish individual control streams, not competing voices.
- The sound control trace is capped at 180 seconds; exhausting it is Unknown, not evidence of a loop. The all-sounds report includes such cases.
- No original-game observation, listening, picture or timing comparison is claimed.
- Initial harness compilation required corrections to the project-relative path and the resource collection property. A sandbox testhost stalled; the same targeted tests completed with the authorized elevated invocation. NuGet audit endpoint access was unavailable during the initial harness restore.

## User checks (exact steps and where; none if none)

None yet: S4b boot acceptance remains pending.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [Zak tracker](../../docs/zak/SUPPORT_STATUS.md). The boot facts and final result will be added when the shared session reaches a bounded checkpoint.
