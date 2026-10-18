# ManiacEnhanced engine fidelity: native darkness and flashlight

| | |
|---|---|
| Family / stage | ManiacEnhanced / engine fidelity; version-rule checks on ManiacClassic, ZakClassic and ZakEnhanced |
| Fixture and edition | `D:/Juegos/Lucas/mmansionE ega es` (DOS v2 Spanish), `mmansion ega en` (DOS v1 English), `zak ega en` (DOS v1 English), `zakE ega en` (DOS v2 English) |
| Date and author | 2026-10-18, lighting worktree agent |
| Commits | This record's implementation commit; parent owns App integration and checkpoint |
| Registry change | Unchanged: existing Full bounded Play/Objects levels are not a complete-game or original-picture claim; Cutscenes remain Partial and ZakClassic costumes retain their separate estimates |

## What was done

Added the native DOS v1/v2 front-background lighting stage, after object/room-colour composition and before actors. Full room light, darkness, flashlight enable and actor-colour enable are separate source bits. The beam follows the pointer and camera, snaps before centering, clips as a moved rectangle and uses the separate native v1/v2 nibble corner tables. Actors remain drawn over the dark room using the native darkness palette, or their own reached palette when its light-mode bit is set. The v2 EGA palette's fixed red entry 12 is retained.

The native v2 constructor's inherited 6x4-strip flashlight default was missing for classic Maniac and both Zak copies; it is now initialized before boot. Scripts can replace it. No game/room/actor exception, invented battery, state patch or picture fill was added. `ScummRoomScene.Compose(selection?)` retains explicit inspection selections; capture metadata supplies the pointer and viewport. The old-v3/v4/v5 lighting paths are unchanged. Parent owns actual App static-thumbnail/room wiring and real-window evidence.

## Commands run (through RTK)

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/maniac-enhanced-fidelity`.

```powershell
rtk proxy dotnet build src/SCUMMexplorer.Core/SCUMMexplorer.Core.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet run --project .verification/maniacenhanced-fidelity-20261018/LightingCheck/LightingCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet run --project .verification/maniacenhanced-fidelity-20261018/LightingCheck/LightingCheck.csproj -p:NuGetAudit=false --verbosity quiet -- "D:/Juegos/Lucas" .verification/maniacenhanced-fidelity-20261018/output/inspect inspect
rtk proxy dotnet run --project .verification/maniacenhanced-fidelity-20261018/LightingCheck/LightingCheck.csproj -p:NuGetAudit=false --verbosity quiet -- "D:/Juegos/Lucas" .verification/maniacenhanced-fidelity-20261018/output/torch torch
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummV2LightingTests|FullyQualifiedName~ScummManiacEnhancedFirstVisitTests|FullyQualifiedName~ScummManiacClassicFirstVisitTests|FullyQualifiedName~ScummZakClassicFirstVisitTests|FullyQualifiedName~ScummZakEnhancedFirstVisitTests|FullyQualifiedName~LoomActorLookTests.AfterTheBoot|FullyQualifiedName~LoomCdRunningSetupTests" -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet build .verification/maniacenhanced-fidelity-20261018/WindowCheck/WindowCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy .\Exe\WindowCheck\Debug\net10.0-windows\LightingWindowCheck.exe "D:\Juegos\Lucas\mmansion ega en" .verification/maniacenhanced-fidelity-20261018/output/window-maniac-classic 5
rtk proxy .\Exe\WindowCheck\Debug\net10.0-windows\LightingWindowCheck.exe "D:\Juegos\Lucas\mmansionE ega es" .verification/maniacenhanced-fidelity-20261018/output/window-maniac-enhanced 5
rtk proxy .\Exe\WindowCheck\Debug\net10.0-windows\LightingWindowCheck.exe "D:\Juegos\Lucas\zak ega en" .verification/maniacenhanced-fidelity-20261018/output/window-zak-classic 36 torch
rtk proxy .\Exe\WindowCheck\Debug\net10.0-windows\LightingWindowCheck.exe "D:\Juegos\Lucas\zakE ega en" .verification/maniacenhanced-fidelity-20261018/output/window-zak-enhanced 36 torch
```

Official source was fetched read-only through RTK PowerShell `Invoke-WebRequest`; the local `source/` and all captures/output are ignored. The headless harness creates no application window/profile, launches no original executable and writes only in its output directory.

## Results (numbers)

Four own-source dark previews: Maniac Room 5 in each edition, Zak Room 36 in each edition. All reach lights 0, 6x4 strips. The unlit source background has respectively 65,690 enhanced-Maniac / 66,022 classic-Maniac / 86,804 classic-Zak / 69,235 enhanced-Zak nonzero pixels; all show zero nonzero background pixels after the native dark stage. The two Maniac actor layers remain visible in native dark colours 0/8. Source previews can have no actor placed; no actor is added to disguise that.

Actual source exploration check: visit Room 28, invoke flashlight 69 pickup verb 14 and Switch on verb 4, then visit Room 36. Both Zak editions retain owner 1 and independently reach lights 12, 9x7 strips through their own Script 6. At screen pointer (179,79), capture pointer (179,63) and camera left 16, classic/enhanced show 2,742/2,472 nonzero background pixels inside the native beam. Moving the pointer changes the beam without running a script or changing state through rendering. Captures of both beams and the two dark Maniac actors were inspected.

The same disclosed direct exploration actions in Room 7 acquire Maniac flashlight 84 for Dave (owner 3), but Switch on correctly retains darkness because batteries are missing. The source's battery branch is not bypassed. No earned-route/complete-puzzle claim follows from direct activation.

Focused run: 30 passed, zero failed/skipped, including 12 lighting cases, retained four-family first-visit tests and LOOM EGA boot/CD state-setup regressions. Two additional genuine-source Zak torch cases then passed (32 distinct focused cases total, 14 lighting cases). Final committed-code check: all 14 lighting cases plus four family conformance rows passed, zero failed/skipped (18 total, 32 seconds). The initial Core build had zero warnings/errors; final integration build/full suite belong to the parent. Synthetic geometry regressions pin 1,440 lit pixels for v1 and 1,432 for v2 in a 48x32 flashlight, including native asymmetric corners, mouse snapping, viewport edge clipping and camera offset; these numbers are geometry tests, not game-state preparation.

Actual-window acceptance with parent App integration `6b2b1d7` (own-worktree cherry-pick `82d8c12`): all four `MainWindow` processes exit 0. After selecting the source room, stopping Play and calling the production refresh, the actual `RoomPreview.Source` bitmap equals `scene.Compose(selection)` through `DisplayPalette` pixel for pixel: classic Maniac 87,040, enhanced Maniac 81,920, and each Zak 122,880 background pixels. All four source words are lights 0; Normal technical notes are absent. The source actor overlays are visible in the Maniac captures and are not inserted in the actorless Zak first-visit preview.

The two actual Zak windows then continue their own `_play` sessions through the disclosed direct source pickup/Switch on actions and Room 36 visit. Both production `ShowPlayFrame` bitmaps match the composed frame, reach lights 12 / 9x7 strips, and visibly move the beam between screen points (179,79) and (250,100); the Normal technical tooltip is absent. Both full-window beam captures and their moved versions were inspected, as were all four dark-room captures (eight screenshots total). Room 36 is a source ending/death backdrop: classic displays its own `THE GAME IS OVER` message after the direct visit; this is an exploration-state rendering check, not a successful puzzle route or earned ending. The harness does not suppress that line, its actors, music preparation or other reached source behavior. Build zero warnings/errors. Each process has a private profile, thumbnail cache and failure log under its output folder; no existing app/user profile was touched.

## Comparison with ScummVM (source files and date; runtime comparison if any)

Official ScummVM master sources read 18 October 2026: [gfx.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/gfx.cpp) (`drawFlashlight`, separate v1/v2 curve tables), [costume.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/costume.cpp) (`byleRLEDecode_C64`, `ClassicCostumeRenderer::setPalette`), [scumm.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/scumm.cpp) (v2 constructor and room/flashlight/actor order), and `script_v2.cpp::o2_lights` (existing opcode conversion independently confirmed). No fixed upstream commit is claimed; SHA-256 pins the fetched snapshots:

| File | SHA-256 |
|---|---|
| gfx.cpp | 9E02F8C814702CBA46636DBE47A99D76275E6EFBC1C31558F1FE65DD5BA1AA86 |
| costume.cpp | BDB879F6DF210E37BC75C6302CEE732E953521591707CD18FE525886B3EE46F8 |
| scumm.cpp | E5030A537949FC361CE589814A2F73BA9CECC55537B470835BC1D46E74F85AA1 |
| gfx.h | 43E299C9EC349F5AE6D798B0A3EBA17BBA08242F08E86695A15718637B9C94FD |

No ScummVM executable was run. Source execution and Explorer captures do not establish original-game visual/timing parity.

## Differences and uncertainties kept

The native Maniac battery puzzle was not earned; battery-dependent beam activation remains untested in its two editions. Both version geometries and actual Zak source beams are checked. The geometric tests use synthetic buffers explicitly; no synthetic light word or story variable is installed in a game session.

Final bounded Maniac battery follow-up (parent-requested, after window acceptance): two distinct preparation/input attempts failed, so this item stopped under the standing rule. First, direct `ActivateObjectVerb(167,14)` was invalid preparation: the Room 2 battery object has only Use 11, no pickup entry. Its source read established that radio 142 pickup 14 calls the real pickup for both radio and batteries, then assigns battery owner 14 (inside the radio); radio Open 1 transfers the batteries to the current actor. The second approach executes those actual radio actions and acquires torch 84 in Room 7: enhanced owner142=3, owner167=3, owner84=3, player control true. It clicks the displayed Use 11 verb, the actual held battery 167 and torch 84 inventory slots, then the native sentence line. All four inputs are accepted, but the sentence remains `Usar` and source battery variable `var[0x032]` remains 0 for the bounded 1200 frames; later source continuation reaches Room 37/Script 177. No story/owner/actor/variable shortcut or facilitating Core change was introduced. This does not establish whether the native composition was blocked by reached action state, input handling or other source context. The enhanced attempt failed first; Classic's corresponding source was read, but no successful classic battery route is claimed.

The diagnostic is reproducible with `LightingCheck ... output/battery-second battery` and intentionally exits 1 with the input/trace report when the native insertion fails. It is not an acceptance test or a claimed supported route. `NativeManiacTorch.cs` and the `inspect` mode retain the source-driven preparation; local HarnessFailure logs retain both failed attempts. This follow-up changes only verification code and this record; all earlier 14 lighting tests, conformance and real-window acceptance remain the completed scope.

Earlier shown-lit inspection limitations are superseded by the parent App wiring and four actual-window checks above. Original-game comparison, full-game walking/puzzles, camera/other first-visit limitations and unavailable alternate copies remain open.

Failed evidence: the first harness build used a one-level-too-deep project reference and failed compilation; corrected to the existing three-level evidence-project convention. The first source probe used Zak Room 10, which was correctly lit; the own-source scan selected dark Room 36 for acceptance. Initial new torch tests wrongly compared snapshots across pointer input (native mouse variables change); two failed assertions were corrected to compare before/after each render at a fixed pointer. The native beam already rendered correctly, and no engine workaround was introduced. One initial xUnit style warning was corrected with the recommended containment assertion. These failures are retained here; local logs/captures remain ignored.

## User checks (exact steps and where; none if none)

Parent supplies the integrated actual-window check. Optional: open either Maniac copy and browse Room 5; the room should be dark with a gray actor if the source places one. A genuine flashlight puzzle remains subject to native batteries. For prepared Zak flashlight captures see local `output/torch/*-torch.bmp`; these are source exploration actions, not an earned route.

## Where the facts went (links to the script map / engine notes / tracker entries)

Parent owns the coordinated [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md), [engine object-state notes](../../docs/engine/SCUMM_OBJECT_STATES.md), trackers and final checkpoint. Supplied proposed source facts: Maniac Script 55 computes the room mode (enhanced +0044, classic +0040); enhanced +0000 resizes 6x4, classic inherits the native default. Zak Script 6 +003F/+0046 selects 9x7/3x3 and +004A writes the reached mode; flashlight 69 Room 28 Switch on +002A/+002C locks/renames it. Each fixture was read independently.
