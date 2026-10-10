# Room Play Cutscene button (findings E-1)

Opens the real `MainWindow` on the English EGA reference, selects rooms 02 and 13 and checks `RoomCutsceneButton` is visible in Normal and in Advanced mode.

```powershell
rtk proxy dotnet build .verification/room-cutscene-button/RoomCutsceneButton.csproj --verbosity quiet
$env:SCUMM_EXPLORER_PROFILE_ROOT = "$env:TEMP\rcb-profile"
rtk proxy dotnet Exe/RoomCutsceneButton/Debug/net10.0-windows/RoomCutsceneButton.dll 'D:\Documents\Personal\Code Projects\SCUMMexplorer\Games\loom ega mt32 en' [room...]
```

10 October: PASS (visible and enabled in both modes). Exit code 1 on failure.
