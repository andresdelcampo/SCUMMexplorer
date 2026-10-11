# Other v4 games (browse only), 11 October 2026

`GenericV4Check` opens a game in the real `MainWindow`, selects rooms and the first costume, and saves `room-NN.png`, `costume.png` and `report.txt`.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/GenericV4Check/GenericV4Check.csproj --verbosity quiet
$env:SCUMM_EXPLORER_PROFILE_ROOT = '<private profile folder>'
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet 'Exe/GenericV4Check/Debug/net10.0-windows/GenericV4Check.dll' '<Games/FUTURE/monkey1/VGA>' '<output>' 33 34 10
```

Result for Monkey Island VGA: 83 rooms, 293 resources; rooms 33 (Scumm Bar front) and 34 (town) show complete backgrounds, Costume 01 shows 45 stored poses. Known imperfection: Guybrush's shirt is dark in the poses, so the costume palette (taken from the room holding the costume) is probably not what the game uses.

Monkey Island EGA: every room fails with "no PA block" (16-colour image format), so the scanner offers no resources. Existing games: scan hashes (title, version, id, every resource) of all 19 local folders matched before and after the change.
