# Game text gallery verification — 26 September 2026

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

The gallery exposes 780 extracted entries from the supplied `Games/loom`: 639 spoken lines, 134 object names and 7 stored lines. The extractor visits indexed scripts, room entry/exit and local scripts, object verbs, and old-bundle static object names. Shared verb entries may contain repeated wording; this is an entry count, not a count of unique sentences. Text is read from the user's local game files, not distributed in the application.

Four indexed scripts (00, 51, 55 and 116) have invalid resource lengths at their catalog locations. Two Room 70 local paths (203 and 204) have truncated instructions. Exact source diagnostics are retained in [window-check.txt](window-check.txt) and the gallery's collapsed details. The feature does not promise complete recovery of every stored string, identify unproven scene associations, or replay dialogue in gameplay order.

Focused decoder tests: 2 passed. Full solution build: zero warnings/errors.

All commands used the absolute RTK executable with the explicit working directory above. RTK launch required escalation.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~LoomScummTextDecoderTests --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/TextGalleryCheck/TextGalleryCheck.csproj --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet 'Exe/TextGalleryCheck/Debug/net10.0-windows/TextGalleryCheck.dll' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\Games\loom' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\game-texts'
```

The actual WPF windows were rendered. [Nine assertions](window-check.txt) cover all entries being exposed, search narrowing, Room 03 filtering, Spanish accents, original room artwork, a focusable room action, dispatch to Room 03 in MainWindow, empty results clearing stale content, and the object-name filter. The room action used a synthetic routed click; physical mouse/keyboard input was not tested. [Gallery capture](gallery.png) was visually inspected.

Application: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`. Open a LOOM game folder and choose **Game texts** in the main toolbar.

Format reference for old-bundle object names: ScummVM [`getObjOrActorName`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/object.cpp), which reads the name offset at OBCD +16. SCUMM text control semantics were checked against [`string.cpp`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/string.cpp).
