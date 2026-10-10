# Script scan on EGA vs the hand-made catalog (6 October 2026)

`dotnet run --project .verification/cutscene-scan-ega -- "Games/loom ega mt32 en" src/SCUMMexplorer.App/loom-replays.json` (output in `output.txt`).
`LoomCutsceneIndex.Scan` is generic over `IScummDataSource`, so it runs on EGA unchanged: 400 triggers in 197 groups.
All 328 catalogued replays are found by the scan (room, object, verb). 72 triggers are scan-only: 32 groups have no catalogued member (mostly room entry scripts, Use/Look responses with several lines or room loads, and a few single-line draft reactions). Only the scan is not validated by a headless run here; the catalog's validation is.

## 10 October update: aligning the scan with the catalog
`titles` against the current code: identical title+category 217 of 328 (187 before), title differs 9 (24), category differs 105 (119; 90 of them are the dropped shared helpers). Changes in `LoomCutsceneIndex`/`LoomCutsceneTitles`: loads after the span count as a scene, a silent Use is a scene (225 of the 238 grouped replays agree on category, 211 before), and an unnamed plain verb is titled "Reached Room N" from its span's literal loads. Modes added: `titles csv` (features per replay) and `trigger <room> <object> <verb hex>` (the verb's code and the scripts it starts).

## 10 October: the headless run (`visit`)
`visit` runs every catalogued trigger in a fresh EGA session (no start setup) and lists the rooms it entered next to the catalog's: all 328 began a cutscene (271 s). Scene = left the room, or entry, or silent Use: 320 of 328 agree with the catalog's category; the 8 that do not are catalog scenes that stay in their room and speak. The same logic is `LoomCutsceneRun` in Core (test `LoomCutsceneRunTests`). The hardcoded catalog is not replaced; not yet wired into the app (M-01).

## 10 October: the 42 scan-only groups (`uncovered`, headless run of each)
None looks like noise; all are real `cutscene` code the catalog did not record. 23 play from a fresh start, 19 do not begin a cutscene without story state (14 room entries: Rooms 2, 10, 13, 15, 17, 22, 23, 24, 25, 28, 34, 36, 37 and 35's runs but stays; and Room 25 walk and look, Room 34 object 560, Room 43 object 640) and need a derived start setup before they can be checked.
- Draft reactions the catalog omits (17): one-line refusals such as Emptying at grave/cemetery/pool, Invisibility (reverse) at sheep/workers, Terror/Bleaching/Dyeing at lamb, shepherds, dozing; the run stays in the room: Dialogue.
- Real scenes: Room 08 Use (the Elders busy, 49 lines, run left to 9, 67, 54, 3, 11), Room 10 Use (to 13), Room 38 Look at straw (to 34, 68, 37), Room 28 Use dragon (silent, 9 triggers share it); Room 38 and 45 Use distaff (6 lines, stays).
