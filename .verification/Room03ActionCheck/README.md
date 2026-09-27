# Room 03 action check

This harness opens the real WPF `MainWindow`. It checks the shared room-coordinate dispatch, then separately focuses a hotspot button and sends a synthetic WPF routed Enter event. It does **not** inject physical mouse or keyboard input.

Arguments: `<game folder> <output folder> <object> <expected room> [<reverse object> <reverse verb hex>]`.

- **Expected room** is the room the action should reach, or `0` when the action must remain pending. The `0` mode was used on 26 September, before `stopScript` was modelled; object 162 no longer stays pending.
- When the click leaves scripts pending, the harness presses Continue (`AdvanceFrame_Click`) until the reached load opens the destination. It checks that the destination comes from a reached room-load event and that the incomplete-state label stays visible.
- With a reverse object and verb, it runs that verb in the destination and checks that a reached load returns to Room 03.
- Returning through the catalog between steps is setup, not traversal.

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer` (27 September 2026 exit check):

```powershell
dotnet build .verification/Room03ActionCheck/Room03ActionCheck.csproj --verbosity quiet
dotnet 'Exe/Room03ActionCheck/Debug/net10.0-windows/Room03ActionCheck.dll' 'Games\loom' '.verification\room03-exit\window-check' 162 4 167 38
```

The harness writes `window-check.txt` and WPF render captures under the output directory. It uses reflection only to reach application methods and inspect assertions; production code does not depend on the harness.
