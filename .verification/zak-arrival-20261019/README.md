# Timed Zak flight and destination arrival — 19 October 2026

Scope: supplied ZakClassic (`zak ega en`) and ZakEnhanced (`zakE ega en`) under `D:/Juegos/Lucas`; isolated `.worktrees/zak-arrival-20261019`, baseline `ce405e7b`. Parent owns shared docs, matrix, final integration and WPF serialization. Support levels stay unchanged: this is a bounded travel continuation, not a full playthrough or validated cutscene catalog.

## Preparation and source evidence

`arrival.csv` reuses the accepted 18 October Enhanced native input recipe on each independently read fixture, followed by ordinary frame advancement. The same explicit room previews and direct native knife pickup prepare the route; native Use retrieves the CashCard, the reached alien scene is skipped, the user selects San Francisco and Buy in the reservation interface, and native Walk boards the plane. This is disclosed exploration preparation, not an earned bus/airport journey. No runtime variables, owners, actors, ticket values or timers are assigned as prerequisites.

Both copies' Room 11 object 241/FD is `delay 28800` at +003D, `increment var[0x035]` at +0041, then stop. The delay counts down unchanged with the session's native frame clock. Object 263's FD watches that flag and starts Script 79. Its final immediate Walk13 of exit 256 invokes Script 120's native destination mapping; the purchased destination 663 maps to logical airport 145, exit object 190. Door 256 +004F requests `loadRoomWithEgo var[0x02D], var[0x02C], walkTo=(8, 60)` with current values 190/145. The destination is physical Room 8, distinct from departure logical airport 144 although both airports share the resource.

The timer itself worked. The executor previously resolved the load's four arguments but left `TargetId` null for a variable room. That discarded the automatic arrival request as dynamic. The common executor now binds the event destination from the operand's current value, preserving room-operand estimate certainty and leaving unknown targets unresolved. Literal and signed walk arguments retain their existing behavior. No v1/v2 opcode, walking, session, App or game-specific rule was changed.

This correction does not refactor the existing deferred nested scene-dispatch order. A room-owned child records its room load for the session's existing dispatch; these checks establish the resulting destination/state/control, not full synchronous native call-stack ordering for arbitrary scripts. The parent was notified of that boundary; no additional scheduling refactor is claimed.

Primary engine source read 19 October 2026: official ScummVM [script_v2.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v2.cpp), `o2_loadRoomWithEgo`, and [script_v5.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v5.cpp), `o5_loadRoomWithEgo`. Both obtain the object and room through native variable-or-direct reads before starting the scene. ScummVM was not executed; original-game visual, audio and perceived timing comparison remain outside this proof.

## Verification

All commands use RTK. Console and test output stay local/ignored.

```powershell
rtk proxy dotnet build .verification/zak-arrival-20261019/Probe/ArrivalProbe.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy Exe/ArrivalProbe/Debug/net10.0/ArrivalProbe.exe "D:\Juegos\Lucas\zakE ega en" route .verification/zak-arrival-20261019/arrival.csv
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName~ScummZakArrivalTests|FullyQualifiedName~ScummDynamicEgoRoomTests|FullyQualifiedName~LoomBootTests|FullyQualifiedName~LoomClickInputTests|FullyQualifiedName~LoomCdRunningSetupTests" --verbosity quiet --logger "trx;LogFileName=arrival-focused.trx" --results-directory .verification/zak-arrival-20261019/test-results
```

Both console probes automatically reach physical Room 8/logical airport 145 after 2,927 ordinary additional frames (zero-based reported frame 2926), with no subsequent input, Skip or timer bypass. The accepted cabin endpoint had 17,484 jiffies left. Ticket 82 remains consumed, departure is zero, and peanuts 51 retain their Room 11 code and owner. Explicit 2,000-frame post-arrival observation remains at the destination with control.

The final console recipe has a 10,000-frame safety bound with explicit final physical-room/logical-room/control assertions; successful runs stop as soon as the destination is reached and then observe its ordinary frames. That bound does not alter a production cap or the native timer.

Focused tests: **23 passed, zero failed/skipped**, 1m50s (`test-results/arrival-focused.trx`). Eight synthetic event-binding cases cover known variables across v1/v2/v3/v4, estimated room values, explicit unknown and literal destinations/signed coordinates. Two own-copy route tests assert that the last nonexpired timer frame still remains in the cabin, no arrival Script79 began, and only the next native tick can release the landing sequence; they then verify the exact destination, returned control, completed flight slots, ticket/peanuts and unchanged cash. The remaining checks are LOOM EGA boot/click and CD running-setup safeguards. Probe and WPF harness builds have zero warnings/errors. Parent's final suite covers the integrated tree.

## Failures and limits retained

The first faithful probes were initially bounded at 100,000 frames. After source timer expiry, they showed the same stable cabin endpoint because the automatic room request was discarded. Both own probe processes were stopped after diagnostic observation rather than wasting the entire idle bound; partial `classic-first.txt` and `enhanced-first.txt` remain local. A second bounded source-observed Enhanced run (`enhanced-observed.txt`) proved the exact nested door/script request and null target, without changing preparation or game state. Fixed runs are `classic-fixed.txt` and `enhanced-fixed.txt`; old failures are not acceptance.

The first synthetic unknown test forgot that native globals begin zeroed, and therefore observed room 0. Its failure is retained in `dynamic-ego.trx`. Explicitly removing that synthetic known-zero cell tests an actual unknown; all eight corrected cases pass in `dynamic-ego-corrected.trx`. No production fallback was introduced to satisfy the test. Existing seeded actor-placement estimates, repeated ambient Script06 slots, prior bus failures and other unplayed destinations/puzzles remain disclosed and untouched. No complete earned journey, alternate release, original-game audiovisual comparison, restart, catalog or seed is claimed.

## Actual window

`WindowCheck/ArrivalWindowCheck.csproj` copies the accepted real MainWindow flight driver. New per-edition JSON recipes retain the earlier preparation and append native timer-boundary/automatic-arrival checks. It verifies the real timer starts, drives its ordinary callback deterministically, captures the final unexpired cabin frame and arrival, checks composed/displayed pixels and capture state neutrality, and uses a private profile/cache.

```powershell
rtk proxy dotnet build .verification/zak-arrival-20261019/WindowCheck/ArrivalWindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy Exe/ArrivalWindowCheck/Debug/net10.0-windows/ArrivalWindowCheck.exe "D:\Juegos\Lucas\zak ega en" ".verification/zak-arrival-20261019/window-classic" ".verification/zak-arrival-20261019/classic-arrival.json"
rtk proxy Exe/ArrivalWindowCheck/Debug/net10.0-windows/ArrivalWindowCheck.exe "D:\Juegos\Lucas\zakE ega en" ".verification/zak-arrival-20261019/window-enhanced" ".verification/zak-arrival-20261019/enhanced-arrival.json"
```

The parent granted an exclusive WPF lease. Both windows ran serially and **passed**, with zero shipping App edits:

| Copy | Initial cruising remainder / last unexpired boundary | Automatic arrival |
|---|---|---|
| Classic | 17,478 jiffies; 2,913 ordinary callbacks leave delay 0, still cabin 11 | Physical Room 8/logical 145 with control at callback 6679; 11 delivered inputs |
| Enhanced | 17,478 jiffies; 2,913 ordinary callbacks leave delay 0, still cabin 11 | Physical Room 8/logical 145 with control at callback 7002; 11 delivered inputs |

The native delay resumes only after falling below zero, so the zero-delay cabin capture is intentionally still unexpired. The callback then advances one ordinary frame and follows native landing/disembark; no further input is delivered. Ticket 82 remains owner 14/departure 0; peanuts 51 remain owner 1. There are 14 local captures, with all four new unexpired/arrival captures inspected. Zak is visible in both cabin and airport in each copy. Displayed pixels equal the composed frame and capture changes no game snapshot/frame. Immediate arrival captures include the app's transient music-preparation progress; no subjective sound/timing or original-interpreter picture acceptance is claimed.

Both processes exited successfully; the WPF lease was released to the parent. The verifier's source/driver is reproducible, while private profiles/cache/logs/captures stay local and ignored. Shared engine commit `1d8217a`, own acceptance bundle `8be154a` and recipe inclusion `32cb14e` are handed to the parent; the final follow-up records the completed windows and diagnostic safety bound.

Game data, saves, ROMs and the normal user profile remain unchanged. No ScummVM process, junction, worktree removal, stash or force operation was used.
