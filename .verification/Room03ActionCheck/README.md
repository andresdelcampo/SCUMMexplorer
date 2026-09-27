# Room 03 action check

This harness opens the real WPF `MainWindow`. It checks the shared room-coordinate dispatch, then separately focuses a hotspot button and sends a synthetic WPF routed Enter event. It does **not** inject physical mouse or keyboard input. The computer-use skill was read, but this session exposed no callable `node_repl` / Computer Use runtime.

The final argument is the expected reached room, or `0` when the real-data action must remain pending. For a pending action the harness asserts that no room load is reported, a frame advance stays in Room 03, and the incomplete-state label remains visible. Room 04 is then rendered by catalog browsing only. Returning through the catalog is setup, not reverse gameplay traversal.

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer`:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/Room03ActionCheck/Room03ActionCheck.csproj --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet 'Exe/Room03ActionCheck/Debug/net10.0-windows/Room03ActionCheck.dll' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\Games\loom' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\room03-local-action' 162 0
```

The harness writes `window-check.txt` and WPF render captures under the output directory. It uses reflection only to reach application methods and inspect assertions; production code does not depend on the harness.
