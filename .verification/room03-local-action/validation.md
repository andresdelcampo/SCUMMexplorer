# Room 03 local action validation — 26 September 2026

Working directory for every command: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

All shell commands used the absolute RTK executable. RTK could not launch in the sandbox (`Access is denied`); the subsequent commands ran with approved escalation.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter 'FullyQualifiedName~LoomRoomLocalScriptTests|FullyQualifiedName~LoomObjectVerbTests|FullyQualifiedName~LoomBoundedScriptExecutorTests|FullyQualifiedName~RoomObjectSelectionTests' --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

Results: 37 tests passed, 0 failed, 0 skipped. Final full solution build: 0 warnings, 0 errors. Five of the tests cover the new local directory and session integration. The synthetic unconditional local-load fixture verifies child-event propagation; it is not the supplied Room 03 script and does not establish its gate.

Application artifact: `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`.

The [WPF harness instructions](../Room03ActionCheck/README.md) describe the exact build/run commands. The [final assertions](window-check.txt) passed against the real `MainWindow`:

- Room pixel `(24,24)` selects object 162's decoded action ahead of overlapping artwork; invoking verb `0x36` starts its room-local child and does not claim a reached room load.
- The child remains pending and advancing a frame keeps Room 03 displayed.
- The hotspot button and verb button accept focus and synthetic WPF routed Enter events, with the same result.
- The visible action note says no room load was reached and that scripts remain pending; exploration state remains incomplete.
- Inspection mode at the same pixel cycles stored object 166 artwork without running a verb or navigating.
- Room 04 renders when selected separately through the catalog. This is direct browsing, not an executed transition or a verified way back.

Captures: [coordinate action](coordinate-action.png), [keyboard action](keyboard-action.png), [inspection](inspection-artwork.png), [Room 04 browsing](room-04-browse-only.png).

No physical mouse/keyboard input was verified: the computer-use runtime was not exposed. The window was closed after the harness finished. Room 17 was not rechecked in this pass. The Room 03 exit milestone remains incomplete.
