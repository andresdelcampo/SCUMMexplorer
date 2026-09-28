# 28 September 2026: visual-first UI refresh

The default LOOM view now leads with decoded room artwork and a wide scene. Technical catalogs and traces remain available in Advanced mode, while doorway outlines are opt-in. This milestone changes presentation and test setup only; it does not claim timed scene playback or complete gameplay execution.

## Build and Core tests

- Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`
- Build: `& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' dotnet build 'SCUMMexplorer.slnx' --verbosity quiet` — 4 projects, 0 errors, 0 warnings.
- Tests: `& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' dotnet test 'SCUMMexplorer.slnx' --no-build --verbosity quiet` — 197 passed, 0 warnings.
- App artifact: `Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`.

## UI checks

The targeted WPF harnesses use synthetic WPF events and render-to-bitmap captures; they do not represent physical mouse or keyboard input.

| Harness | Result |
| --- | --- |
| WindowCheck | Passed in default and 1050 × 700 layouts; room picker count/accessibility, selection accent, hidden basic-mode resource tree, default hidden exits, Advanced mode, script-to-room return, and scene captures for Rooms 03 and 17. |
| PresentationCheck | 40 assertions passed, including the hidden-by-default exit overlay, opt-in overlay geometry, viewport sizing, horizontal pan/key behavior, music position, costume gallery and resource switching. |
| Room03ActionCheck | 46 assertions passed for coordinate action, keyboard-accessible Advanced hotspot and action choices, Room 03 → Room 04, reverse route, and inspection behavior. |
| ClickInputCheck | 11 passed. |
| DoorwayClickCheck | 75 passed. |
| PointerInputCheck | 13 passed. |
| ReverseHoverCheck | 22 passed. |
| HighlightCheck | 19 hotspot focus/hover checks passed with Advanced catalog explicitly enabled. |
| MusicRowCheck | 5 passed. |

The affected WPF regressions total 231 counted assertions, in addition to WindowCheck's state and screenshot assertions. The screenshots use the production `App.xaml` resources, including the rounded scrollbar template. The final captures are [Room 03](window-check/room-03.png), [Room 17](window-check/room-17.png), and [Room 17 at 1050 × 700](window-check/room-17-1050.png); PresentationCheck's geometry report is [here](presentation-final/geometry-check.txt).
