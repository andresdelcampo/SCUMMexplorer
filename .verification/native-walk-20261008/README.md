# Native VGA walking comparison — 8 October 2026

The VGA/v4 session now uses ScummVM's ordinary `Actor` walking algorithm. EGA/v3 retains `Actor_v3` walking. The shared v3 implementation previously delayed the first movement frame, selected old gates and used v3 step counters for VGA as well. The correction separates v4 gates, fixed-point steps, final box assignment and direction interpolation. It adds no room exception, story-state assignment or scene start.

## Independent reference

`Reference/build.ps1` extracts the unchanged `Actor::calcMovementFactor`, `Actor::actorWalkStep`, `Actor::walkActor` and `Actor::findPathTowards` bodies from the local matching ScummVM 2.2.0 source downloaded during the [previous investigation](../final-gaps-20261008/README.md). The small C++ host compiles with local MSYS2 GCC 15.2.0. Its animation/facing/rendering/sound calls are inert; this reference verifies **kinematics**, not a full ScummVM game run. The host's pre-v5 bounds checks use the same slope tests. Route matrices and adjusted initial/destination points come from the existing decoded Core data; native resource parsing and placement are therefore not independently tested here. Compared boxes have constant scale 255.

Source SHA-256:

| File | Hash |
| --- | --- |
| `actor.cpp` | `766C8BEAE69FCF431BD8086DC08F7A0E861EEFF5763F66CA31616737D4B131EE` |
| `boxes.cpp` | `C9F54179F96B505872F265AADC5122BEEE0D2C7FEA1E728D06951D15D0955202` |
| Extracted methods | `CEF54053D5F38F5FA4A3BA80C945371D9CEB3E71F3739017581F1423D295D11B` |

All **11 bounded walks** match the corrected implementation frame for frame: coordinates, walk box, movement flags and trace length. They cover Room 34's return leg and paths across boxes 5–10, plus Rooms 2, 20, 41 and 43. The previous shared-v3 walker diverges from frame 1. Output stays ignored in `out-before/` and `out-v4/`; these are geometry comparisons, not scene recipes. In particular, resource-default geometry with box 6 enabled is not claimed to be a single reachable story state.

## Room 34: the evidence limit is narrower, not closed

Native geometry runs from `(246,64)` or `(246,80)` toward the box-8 endpoint still sample box 7 before box 8. The native walk finishes at the shared endpoint in box 9, as destination adjustment chooses the higher containing box. This does not satisfy local 200's previous-box 6 condition.

The **actual fresh Room 46/object 610 look** runs global 97 without added placement or bits. Its first movement frame is `(246,65)`; frame 9 reaches `(246,80)` in box 6. The resumed script hides box 6 at `+002D` and re-places Bobbin at `+003C`, yielding `(246,82)` in box 5. User input is disabled through the arrival and enabled at frame 11. Entry's `+0375` reads bit `01B = 1`, so this preview **does not start local 200**. It cannot establish the monitor's native samples. `out-arrival2/arrival.json` retains instructions, selected pre-instruction values/dependencies and frame state.

A separate **fresh Room 34 visit** has bit `01B = 0`, `070 = 0`, `00E = 0`, starts local 200 and hides box 6 (`80`). Public walking to object 485's own `(246,64)` walk point is adjusted to `(246,82)` in box 7. A second public walk to the decoded box-8 endpoint follows boxes 7, 8, 9. At frame 246 the monitor reads current 8 and previous 7 at `+0020/+0027`, with camera 272. The previous-box test fails before the camera test; no crossing scene starts. `out-monitor/monitor.json` retains this trace. Its starting catalog arrival and inherited position estimates remain labelled; this is not a source-earned original-game route.

The engine's ordering agrees with the matching source: `scummLoop` runs scripts before `walkActors`, then moves the camera. Native v4 can consume multiple short legs in a frame, so the picker note no longer claims that every walk must sample one neighbour per frame or that a placement script is necessary. It retains the intervening matrix box, failed trials and unresolved original trigger.

**Still unresolved:** source-earned/native eligible state, exhaustive dynamic callers and original-game reachability. No dead-code classification or 100% completion claim follows. An existing-save request from the prior chat remains pending; it was not repeated. A useful next source check is whether legitimate state transitions can leave Bobbin in box 6 while the early monitor becomes eligible. The source flag/guard pairing above should guide that check before a replay or new-save request.

## Validation

- 51 focused Core tests pass: v4 fractional/signed motion, scaled ignore-box movement, four-direction turning, intervening-box sampling, disconnected routes, actual return wiring; existing EGA actor/look/box and VGA cutscene tests.
- VGA scene sweep: **222/223 groups complete normally and with Escape**, with matching boundaries and settled output. Room 34 remains `no-scene` in both runs. This rerun is warranted by the generic VGA walking change; the English EGA full sweep and full Core suite were not rerun.
- Existing real WPF interaction/playback harness: **30 assertions pass** for EGA/VGA native object input, navigation, pause/step/resume/Skip and scene-picker guards.
- Clean solution build, zero warnings/errors. Build cwd: `D:\Documents\Personal\Code Projects\SCUMMexplorer`; app: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`.

Game resources, ROMs and user saves were not edited. Independent original-game picture/listening checks remain open. The 11 reference comparisons exclude animations and sound; the WPF checks are not independent ScummVM viewing/listening proof.

## Reproduce

All commands run from the repository through RTK, including the build script's compiler and the exporter's native child process. Source and generated method/binary files remain ignored.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/native-walk-20261008/Reference/build.ps1
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/native-walk-20261008/Export/Export.csproj -- .verification/native-walk-20261008/out-v4 v4
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/native-walk-20261008/Export/Export.csproj -- .verification/native-walk-20261008/out-arrival2 arrival
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/native-walk-20261008/Export/Export.csproj -- .verification/native-walk-20261008/out-monitor monitor
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter 'FullyQualifiedName~LoomV4WalkingTests|FullyQualifiedName~LoomActorTests|FullyQualifiedName~LoomActorLookTests|FullyQualifiedName~LoomWalkBoxTests|FullyQualifiedName~LoomCdCutsceneTests' --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/correctness-20261008/Audit/Audit.csproj -- 'Games/loom vga en' .verification/native-walk-20261008/out-scenes - scenes
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/correctness-20261008/UiAudit/UiAudit.csproj -- Games .verification/native-walk-20261008/out-ui
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```
