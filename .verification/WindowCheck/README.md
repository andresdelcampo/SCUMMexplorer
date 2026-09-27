# Windows rendering check

This harness opens the real `MainWindow`, loads the supplied LOOM folder, selects Rooms 03 and 17 through the existing tree selection method, waits for the asynchronous room load, and saves WPF render captures and visible status text. It does not inject mouse/keyboard input or establish gameplay reachability.

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer` in PowerShell:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/WindowCheck/WindowCheck.csproj --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet 'Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\Games\loom' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\after'
```

The harness uses reflection for inspection only and is separate from the production application. Opening the game uses the app's normal last-folder persistence. Captures in `../before` were produced before the correction; `../after` uses the final correction. Both runs showed image 166 in Room 03 and images 354,355,360,361 in Room 17. Both scenes remained explicitly incomplete, with respectively one and four artwork estimates. Room 17's visible black patches remained. No audio verification was performed for this state-model change.
