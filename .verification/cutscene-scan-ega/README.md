# Script scan on EGA vs the hand-made catalog (6 October 2026)

`dotnet run --project .verification/cutscene-scan-ega -- "Games/loom ega mt32 en" src/SCUMMexplorer.App/loom-replays.json` (output in `output.txt`).
`LoomCutsceneIndex.Scan` is generic over `IScummDataSource`, so it runs on EGA unchanged: 400 triggers in 197 groups.
All 328 catalogued replays are found by the scan (room, object, verb). 72 triggers are scan-only: 32 groups have no catalogued member (mostly room entry scripts, Use/Look responses with several lines or room loads, and a few single-line draft reactions). Only the scan is not validated by a headless run here; the catalog's validation is.
