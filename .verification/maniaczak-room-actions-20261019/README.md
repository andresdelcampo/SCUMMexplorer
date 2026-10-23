# Maniac/Zak room actions and source panels

Planned 19 October; final resumed window verification completed 23 October 2026. Covers ManiacClassic (`mmansion ega en`), ManiacEnhanced (`mmansionE ega es`), ZakClassic (`zak ega en`) and ZakEnhanced (`zakE ega en`), under `D:\Juegos\Lucas`. Game resources, user profiles and ScummVM were not changed or run. Each window used its own `output/<packet>/profile` and room cache, muted audio, and closed in its `finally` handler. The exclusive WPF lease was released after the final Zak Enhanced process exited successfully.

## Change and source

The added `MainWindow.GenericRoomActions.cs` reads each displayed room through its own `IScummDataSource`. Normal and Advanced offer direct object-verb exploration actions; the actual native verb names can replace early `Action` words without rereading room source. A direct action deliberately bypasses sentence composition and walking. Advanced exposes available room entry/exit/local scripts, object code and possible source relationships. V1/v2 have **no room-local script directory**; this check does not fabricate local-script rows or reuse LOOM's readable-script formatter. The new panel currently supplies raw disassembly rather than a generic readable formatter.

`MainWindow.PlayCutscenes.cs` keeps room membership and names in both modes; technical evidence appears only in Advanced. Room membership may differ from the launch room. `0115897` makes Interact and source controls readable on the dark theme. `646e9f7` permits only the existing R-8.34 reviewed unanswered Maniac menu preparation: stopped clock, static preview, same game, actual Room45, no selected party, no delivered/pending input. All other active/departed scene and boot guards remain. `8450770` distinguishes restarting play in a prepared room from actually entering it, so an explicit entry launch still executes entry when required.

Final Zak Enhanced assembly was built after merging parent `bde49369` (local merge `1bad3934`), zero warnings/errors, 7.98s. The two owned production files are identical to `8450770` for all four accepted family runs. Parent owns shared XAML/hooks, overlays, audio, final full regression, support matrix and promotion; this packet does not claim separate acceptance for those items.

## Commands

Run from the isolated worktree, always through RTK:

```powershell
rtk proxy dotnet build .verification/maniaczak-room-actions-20261019/RoomActionsCheck.csproj --no-restore -p:NuGetAudit=false --verbosity minimal
rtk proxy dotnet Exe/RoomActionsCheck/Debug/net10.0-windows/RoomActionsCheck.dll source D:\Juegos\Lucas .verification/maniaczak-room-actions-20261019/output/source-final
rtk proxy dotnet Exe/RoomActionsCheck/Debug/net10.0-windows/RoomActionsCheck.dll window D:\Juegos\Lucas .verification/maniaczak-room-actions-20261019/output/window-mmclassic-final "mmansion ega en"
rtk proxy dotnet Exe/RoomActionsCheck/Debug/net10.0-windows/RoomActionsCheck.dll window D:\Juegos\Lucas .verification/maniaczak-room-actions-20261019/output/window-mmenhanced-tv-final "mmansionE ega es"
rtk proxy dotnet Exe/RoomActionsCheck/Debug/net10.0-windows/RoomActionsCheck.dll window D:\Juegos\Lucas .verification/maniaczak-room-actions-20261019/output/window-zakclassic-final "zak ega en"
rtk proxy dotnet Exe/RoomActionsCheck/Debug/net10.0-windows/RoomActionsCheck.dll window D:\Juegos\Lucas .verification/maniaczak-room-actions-20261019/output/window-zakenhanced-final "zakE ega en"
```

These are acceptance commands, not permission to rerun completed puzzles or the catalog. The source mode constructs no App/MainWindow/window. It found **27/27/54/54 clean source titles** and **10/10/74/70 fully decoded ordinary object entries** in the sampled rooms (Maniac28/Zak1). This census is distinct from executing actions. The final window driver uses the native-tested TV17/190/4 and drawer1/92/1 examples; original failed packets remain unchanged.

## Actual window results

| Family | Result/time | Actual Normal / Advanced popup | Direct action from a fresh native intro |
|---|---|---|---|
| ManiacClassic | PASS, 24.83s | Room26, **2 / 5** rows | Room17, object190/verb4, **Turn on · TV**; TV state turns on |
| ManiacEnhanced | PASS, 53.67s including LOOM switch | Room26, **2 / 5** rows | Room17, object190/verb4, **Encender · TV**; TV state turns on |
| ZakClassic | PASS, 49.88s | Room16, **4 / 15** rows | Room1, object92/verb1, **Open · dresser drawer**; drawer state opens |
| ZakEnhanced | PASS, 49.00s | Room16, **4 / 15** rows | Room1, object92/verb1, **Open · dresser drawer**; drawer state opens |

The driver opens the actual room button, inspects the resulting popup's WPF rows rather than only its source list, and raises a real row's Click event. Both modes have readable titles without `verb 0x`; Normal rows have no evidence tooltip, Advanced rows do. Maniac's selected member launches from Room1 and invokes object394/verb9; Zak's selected entry launches Room2. Thus the sampled room's list includes a scene launched elsewhere. Acceptance proves the launch path, not completion or full catalog fidelity.

Before each direct action the driver explicitly restarts the genuine intro; Maniac uses the actual native party clicks and native override skip, Zak advances its own boot to control. No puzzle variable or actor position is forced. The real Interact expander is opened, a named source action row is clicked, and `LastAction` matches its edition-owned object/verb. **DeliveredInput and native sentence-script trace counts remain unchanged.** Both sampled native state changes occur. Room source/action identity survives an unchanged refresh; available names are native words rather than raw verb bytes.

All four windows expand available entry/exit source and an object's raw disassembly, assert possible-source-relationship controls, and retain an Advanced capture. All four sampled rooms have entry and exit; zero locals reflects the actual v1/v2 layout. This verifies Advanced source availability, not a general readable formatter or every branch/resource relationship. The enhanced Maniac run additionally switches to LOOM, confirms its legacy Interact control is restored and both generic hosts collapse. Only that run tests the LOOM switch.

`records/` preserves complete reports and four own-source traces. The earlier Maniac reports' final summary sentence predates the driver wording correction: interpret their proof through the table above; **static startup end-to-end playback was not accepted**, and only enhanced Maniac checks the LOOM switch. Six compact representative captures total 558,890 bytes. Four were visually inspected: the disabled static preview, enhanced Advanced source, classic direct TV and enhanced Zak direct drawer. The remaining accepted captures are retained for review.

## Failed evidence and limits retained

- `window-mmenhanced-1`: harness incorrectly assumed Room1 had multiple playable scenes. The source-derived membership census instead found Room26/31 each with two; Room1 has one. The driver now chooses an actually populated room and handles a single launch without requiring a popup.
- `window-mmenhanced-2` and `window-mmenhanced-fixed`: the static unanswered-party preview showed a disabled scene control. Instrumentation proved `BootRunning=false`; its retained native cutscene scope, not boot-running alone, blocked the action. A narrow boot-only experiment did not help and was reverted. The final exact reviewed R-8.34 gate is source-reviewed and compiled, **not retested end to end in a static-start window**, after the two-approach stop. Partial-party preservation/refusal stays in existing `ScummManiacEnhancedCutsceneTests.NativeStartupPreparationTracesItsOwnCutsceneAndPreservesAChosenParty` and the unchanged native preparation API.
- `window-mmenhanced-native` and `window-mmenhanced-final`: the telescope row correctly dispatched object413/verb11 with unchanged delivered-input and sentence counts, but the extra immediate no-override-cutscene assertion failed, including after a fresh native intro. No third telescope attempt or forced prerequisite was made. Direct invocation bypasses the normal walk; the conditional telescope outcome remains **unproved**. The independent, already source-tested TV example establishes the requested direct dispatch and its own observed native effect.
- `window-mmenhanced-tv`: a harness-only attempt to cancel selector preparation early left the action planner unavailable, producing a NullReferenceException. That optimization was removed; final checks retain normal initialization order. No product loader change was made.

The actual popup acceptance runs after native startup, never by bypassing an old source scene. Normal still filters candidates through a begun source run; Advanced retains derived candidates and evidence. This is not a new S7 catalog audit, an earned puzzle route, an original-game timing comparison, or full room/edition conformance. ScummVM was not executed. Existing source guards and unresolved generic readable formatting remain explicit; no support level is raised by this packet.
