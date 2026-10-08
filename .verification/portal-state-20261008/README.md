# Closed Room 34 portal and invalid Rusty return

**Checkpoint scope, reconciled 8 October:** `ebdf3b2` correctly blocks closed/untouchable portal traversal and preserves prepared ghost/restoration state. It did not supply missing story history for a fresh direct Room 46 catalog selection. The user's subsequent fresh 46 → 34 report is separately corrected in `9de8193` by approved actual source prerequisites; see [catalog return validation](../catalog-return-20261008/README.md). The standalone fortress reveal remains parked. Instructions and counts below describe this earlier portal checkpoint.

The user reported ordinary SCUMMexplorer traversal from Room 34 through the visually closed portal to Room 46 and back. Sleeping Rusty then spoke and walked as the ghost; subsequent appearances used the wrong story state. The standalone fortress-reveal investigation is parked at the user's request. This packet concerns the unintended portal traversal only. Plan, prompt and handoff files were left untouched by this fix.

## Reproduction and source explanation

The WPF harness starts a new folder session and visits Room 34 through the catalog. It uses the application's actual `HandleRoomPoint` input/playback path at the portal rectangle, without editing story bits, actor costumes, walk boxes or positions.

Before the fix, the English and Spanish EGA input scripts left the session in Room 34 because the portal was untouchable. The window nevertheless retried the portal's use callback directly, entering Room 46. The initial portal objects are English 560, Spanish 559 and VGA 485, all at `(224,16)` with size `48×64`. VGA already stayed in Room 34 in this baseline sample. Both EGA baselines failed all three original assertions: remain in Room 34, avoid the direct fallback, and preserve Rusty's costume.

English Room 46 entry +0x0000 sets the late-scene bit D7A. The distinct Rusty ghost bit D9C is established by actual Script 87 (+0x022B / +0x0916). Room 34 entry tests D9C at +0x01A4 and selects ghost costume 120 at +0x01CA or sleeping-boy costume 112 at +0x0289. Global 98 assumes that setup: it does not initially assign the ghost costume. The forced early traversal therefore combines a later scene with a Rusty state whose prerequisites never happened. It does not demonstrate a separate costume-renderer defect.

The correction applies the game's shared hit selection to doorway retries, omits untouchable exits from the active doorway overlay, and prevents a failed direct callback from opening a merely possible static destination. Advanced static destination buttons remain explicit catalog browsing.

## Focused validation

- **41 WPF/source assertions pass** in `Ui/Program.cs`. English EGA, Spanish EGA and VGA single/double clicks on the closed portal retain Room 34, both actors' costumes and the pre-late flag. Closed portals have no active doorway overlay.
- The English prepared later preview uses the application's existing Firewood setup and real Script 87 to establish D9C; catalog visits and the existing staff adjustment are disclosed exploration assumptions. No private state or costume assignments are made. A refused portal use callback cannot force its static Room 46 target. Rusty keeps costume 120 through the Room 46 return, global 98 close-up and eastward walk.
- After actual local 209 restoration, the enabled portal reaches Room 46 through native input's use sentence with no fallback. Its source return does not bring sleeping Rusty back. The prepared prerequisites take 4,095 frames, ghost conversation 1,141, restoration 640, and final return 82.
- **30 existing WPF navigation/playback assertions pass** from the correctness `UiAudit`: ordinary Room 03 → 04 navigation, conditional grave look, scene picker eligibility, pause, step and Escape/Skip for EGA and VGA.
- Solution build passes with **0 warnings and 0 errors**. The before/after PNGs and full reports are generated in ignored `out-*` directories; the final closed-portal, valid ghost and enabled-portal renders were inspected.

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`:

```powershell
rtk proxy dotnet run --project .verification/portal-state-20261008/Ui/Ui.csproj -- Games .verification/portal-state-20261008/out-final
rtk proxy dotnet run --project .verification/correctness-20261008/UiAudit/UiAudit.csproj -- Games .verification/portal-state-20261008/out-navigation
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

Built application: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`. Retest from a fresh session: reopening the game folder resets the session. An already altered session is not silently repaired by inventing story flags. No game resources, original saves or saved application-state files were deleted. No ScummVM takeover was needed for this fix.
