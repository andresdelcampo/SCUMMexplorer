# Whole-draft WPF input — 8 October 2026

**168/168 assertions pass across 12 independent prepared Room 04 casts.** The real `MainWindow` handles four routed text-input events or four scaled staff-strip clicks, then its own playback step reaches the target's forward/reverse response. English Roland EGA, Spanish Roland EGA and English CD/VGA all pass. No production behavior changed.

This closes the **Explorer whole-draft WPF integration** item left explicitly untested by the historical [game-key checks](../game-keys/validation.md). It does not close the original-game physical-input or earned-prerequisite comparisons.

## Exact preparation and input

Each case opens a fresh game/session through the App, selects Room 04 and uses the public staff/eight-note Game Status controls. The room's own object is aimed through `HandleRoomPoint`, the room-click handler's point seam. No story, actor placement, costume, walk box or draft-table edit is made. EGA reports “Known in this exploration session”; CD retains “Estimated from seeded source execution”. Neither establishes a player-earned route.

The two input routes are distinct:

- **Text:** four `PreviewTextInput` routed events raised on the real window. Its registered handler marks each event handled and sends the ASCII note through the game input script.
- **Staff clicks:** each note's own current on-slot rectangle is projected into the rendered strip's actual WPF dimensions. `InputHitTest` reaches that image, then `ClickVerbStrip(Point)` uses the same point conversion, on-slot hit test and dispatch as the mouse-up handler. No OS mouse event, pointer movement or `MouseEventArgs.GetPosition` delivery is simulated.

EGA's source note verbs are 1–8; CD's are 90–97. Each cast uses its own draft pattern. This seeded run uses note IDs 3,1,3,2 forward and 2,3,1,3 reverse; they are not universal native-game keys. Eight ordinary App playback ticks separate the notes, then at most 600 finish the response. These ticks call the real WPF playback/refresh path, but are driven synchronously; no wall-clock cadence or audio synchronization claim follows.

| Edition | Target | Forward response | Reverse response |
| --- | --- | --- | --- |
| English Roland EGA | Hole 171 | “The hole's already wide open.” | “Whatever lives in the hole / might not like that.” |
| Spanish Roland EGA | Agujero 171 | “El agujero está ya totalmente abierto.” | “Puede que no le guste nada / a lo que viva en ese agujero.” |
| English CD/VGA | Thorns 101 | “I don't think you can weave with thorns.” | Same source refusal |

Both input routes dispatch 3A/3B through Script 7 +002E in all editions. All 12 cases have Script 41 +0000/+009A's balanced scope and settle in Room 04 without a draft, cutscene or movement. The strip dimensions are approximately 995.886 × 174.28 WPF units. Three post-cast strip captures were saved; the English EGA and CD captures were inspected. They are Explorer presentation evidence, not native pixel-bound comparisons.

## Retained first attempt

`out-first/` records **20/25 assertions and only 2/12 cases**, and remains failed. The harness incorrectly ran each single note's entire instant playback before sending the next; Script 20 chained to Script 21 and cleared the buffer between notes. It also applied CD's note-verb numbering to EGA, so the first staff-click case found no matching slot. `out-second/` corrects those harness assumptions and passes all 12 cases. This is no evidence of an App defect; no engine/renderer/input patch was made.

## Isolation and reproduction

The helper creates its own profile/cache under the output directory before constructing the App. Its offscreen window uses `ShowActivated=false`, `ShowInTaskbar=false`; every case confirms it never activates. Scene sound is `None` and CD voice is disabled. No native-game control, OS input, save, ROM or game-data writes occur. Existing running applications are not controlled or restarted.

The build initially emitted the SDK's first-run development-certificate notice. A read-only CurrentUser/My check found only older localhost certificates and no newly dated certificate for this run. Reproduction explicitly disables development-certificate generation and telemetry and uses the private CLI home.

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`, every shell/child command uses RTK:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/wpf-casting-20261008/run.ps1
```

`run.ps1` contains the exact isolated build and execution commands. Its verified executable is `D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\wpf-casting-20261008\out-build\bin\debug\LoomWpfCastingCheck.exe`. Build: **zero warnings/errors**. `out-second/checks.txt`, `cases.json`, the 12 traces, slot records, screenshots and private profile/cache remain ignored. The script defaults to a separate `out-reproduction/` packet and preserves both existing attempts.

The reproduction script parses without errors; whitespace checks pass. All **347 relative links in seven changed Markdown files** resolve. No broad Core or scene sweep was repeated for this verification-only change.

The three original-game comparison areas remain open in [the live dossier](../../docs/loom/FINAL_GAPS_HANDOFF.md). The smallest next picture comparison is Room 43's matched actor/staff frame. Native Computer Use and the standalone Room 34 reveal remain stopped/parked under the user's earlier instruction.

**9 October update:** the user accepts the checked Room 43 scene's actor arrivals and placement and says to move on. [That concern is closed](../room43-accepted-20261009/README.md); the paragraph above records the 8 October handoff, not a current instruction to repeat Room 43. Physical casting/input is now the next priority, followed by listening/timing.
