# Validation: hotspot row highlights its box (27 September 2026)

Status: **done.** Builds with 0 warnings; 156/156 tests pass (no Core change). [HighlightCheck](highlight-check.txt): 19/19 rows in Rooms 03, 10 and 17.

## Change

A second canvas over the room image (`HotspotHighlightOverlay`, not hit-testable, same margin and scale as the doorway overlay) shows one cyan box at a hotspot's rectangle, at its current session position. A row in the Hotspots list draws its box on `MouseEnter` and `GotKeyboardFocus`. `MouseLeave` clears the box unless the row has keyboard focus, and `LostKeyboardFocus` clears it unless the mouse is over the row. The box is redrawn when the canvas size changes and cleared when the list is rebuilt (room change, mode change). The box is shown whether or not "Show doorways" is checked.

## Check

`HighlightCheck` (new harness, `.verification/hotspot-highlight/HighlightCheck`) uses the real `MainWindow`. It browses to each room, focuses the first hotspot row and raises synthetic `MouseEnter` on the others, and checks for exactly one box whose aspect ratio matches the object's rectangle. It then clears focus or raises `MouseLeave` and checks that the canvas is empty and collapsed. Captures: [Room 03 focus](room-03-row-0-focus.png), [Room 10 hover](room-10-row-1-hover.png), [Room 17 hover](room-17-row-1-hover.png). No physical mouse or keyboard input.

```powershell
dotnet build .verification/hotspot-highlight/HighlightCheck/HighlightCheck.csproj
Start-Process -Wait -NoNewWindow -WorkingDirectory $PWD -FilePath Exe\HighlightCheck\Debug\net10.0-windows\HighlightCheck.exe -ArgumentList "Games\loom", ".verification\hotspot-highlight", "3", "10", "17"
```
