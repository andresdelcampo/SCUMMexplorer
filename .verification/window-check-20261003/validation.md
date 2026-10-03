# WindowCheck repaired (3 October 2026)

The harness still looked for the removed "Show exits" checkbox (`RoomNavigationToggle`) and crashed. Its two checks on the exits toggle and overlay were removed; every other check (room cards, accessible names, selection accent, hidden tree in basic mode, Advanced mode, compact 1050 x 700 layout, script-to-room return) is unchanged. Run for Rooms 03 and 17: exit code 0, no `error.txt`; captures and `window-check.txt` are in this folder.
