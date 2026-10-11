# Other v4 games (browse only), 11 October 2026

`GenericV4Check` opens a game in the real `MainWindow`, selects rooms and the first costume, and saves `room-NN.png`, `costume.png` and `report.txt`.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/GenericV4Check/GenericV4Check.csproj --verbosity quiet
$env:SCUMM_EXPLORER_PROFILE_ROOT = '<private profile folder>'
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet 'Exe/GenericV4Check/Debug/net10.0-windows/GenericV4Check.dll' '<Games/FUTURE/monkey1/VGA>' '<output>' 33 34 10
```

Result for Monkey Island VGA: 83 rooms, 293 resources; rooms 33 (Scumm Bar front) and 34 (town) show complete backgrounds, Costume 01 shows 45 stored poses. Known imperfection: Guybrush's shirt is dark in the poses, so the costume palette (taken from the room holding the costume) is probably not what the game uses.

Monkey Island EGA: every room fails with "no PA block" (16-colour image format), so the scanner offers no resources. Existing games: scan hashes (title, version, id, every resource) of all 19 local folders matched before and after the change.

## Indiana Jones and the Last Crusade, 256 colours (`Games/FUTURE/indy3 vga en`)

`indy-vga/` holds rooms 01, 08 and 15 and Costume 01 in the real window (`GenericV4Check`). The folder is the v4 format with one plain file per room (see CHANGELOG); the four older strip codes 0x02/0x03/0x04/0x07 were added from ScummVM's `unkDecode8` to `11`. All 87 room files with a picture decode; Rooms 98 and 99 are resource banks without one. Costume colours look right (Indy in a brown suit), which suggests the dark shirt of Monkey's Guybrush is a Monkey-specific costume colour mapping rather than the palette lookup.
