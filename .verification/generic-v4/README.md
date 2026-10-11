# Other v4 games (browse only), 11 October 2026

`GenericV4Check` opens a game in the real `MainWindow`, selects rooms and the first costume, and saves `room-NN.png`, `costume.png` and `report.txt`.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/GenericV4Check/GenericV4Check.csproj --verbosity quiet
$env:SCUMM_EXPLORER_PROFILE_ROOT = '<private profile folder>'
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet 'Exe/GenericV4Check/Debug/net10.0-windows/GenericV4Check.dll' '<Games/FUTURE/monkey1/VGA>' '<output>' 33 34 10
```

Result for Monkey Island VGA: 83 rooms, 293 resources; rooms 33 (Scumm Bar front) and 34 (town) show complete backgrounds, Costume 01 shows 45 stored poses. Guybrush's shirt is dark in the poses because a costume is drawn in the palette of the room that stores it (room 10, dusk); the EGA costume is white.

Monkey Island EGA (first version of this record): every room failed with "no PA block" (16-colour image format), so the scanner offered no resources. Later the same day the EGA strip code was shared with the v3 decoder: `monkey-ega/` shows rooms 10, 33, 34 and Costume 01 (white shirt, 81 rooms, 286 resources). Existing games: scan hashes (title, version, id, every resource) of all 19 local folders matched before and after the change.

## Indiana Jones and the Last Crusade, 256 colours (`Games/FUTURE/indy3 vga en`)

`indy-vga/` holds rooms 01, 08 and 15 and Costume 01 in the real window (`GenericV4Check`). The folder is the v4 format with one plain file per room (see CHANGELOG); the four older strip codes 0x02/0x03/0x04/0x07 were added from ScummVM's `unkDecode8` to `11`. All 87 room files with a picture decode; Rooms 98 and 99 are resource banks without one. Costume colours look right (Indy in a brown suit), which suggests the dark shirt of Monkey's Guybrush is a Monkey-specific costume colour mapping rather than the palette lookup.

## Maniac Mansion and Zak McKracken (enhanced v2 and classic v1)

`mm-enhanced/` and `zak-enhanced/` (rooms 1, 4, 12, 30, the Costumes tab, `costume-cards.png`), `mm-classic/` and `zak-classic/` (rooms 4, 10, 30). Enhanced: Secret Lab door with its dithered wall, the Pepsi lab, Zak's mountain and van; the costume cards show the limb costumes (Maniac's heads are separate costumes). Classic: Maniac's foyer with the grandfather clock in the original pinks and browns, Zak's rooms. Rooms were not compared with ScummVM. Zak Enhanced Costume 00 is an empty entry ("points past the end of its file").

## SCUMM v5 (Indy 4, Monkey Island 1 CD, Monkey Island 2)

`indy4-classic/` (rooms 1, 4, 20; Costumes tab: Indy in several poses), `monkey-cd/` (the English folder of the CD, rooms 1, 4, 20), `mi2/` (Monkey Island 2 talkie). `monkey1 vga en` showed nothing before because it is the CD (v5, `ENGLISH/MONKEY.000`). Indy 4 room 20 is a plain tan screen (a flat room). No ScummVM comparison.
