# M3c: "Play this cutscene" from Game Texts (29 September 2026)

## What the user now sees

- In **Game Texts**, a text printed by a known cutscene's script shows a **Play this cutscene** button next to "Visit room" ([game-texts-044-button.png](game-texts-044-button.png)). Its tooltip names the cutscene and its stated start, the same wording as the "Play a cutscene ▾" menu.
  - Script 044 (the opening): its 9 texts (the CGA copyright line, the fairy's four lines, Bobbin's four lines).
  - Script 046 (the Elders' trial): its 40 texts, and its group under "Conversations and cutscenes".
  - Script 052 prints no text (decoded, 29 Sep), so no text offers it; it stays in the menu only.
  - Object names, labels and the texts of other scripts show no button.
- A click closes Game Texts and plays the cutscene exactly as the menu entry does (`PlayOpening` or `PlayKnownCutscene`): the status line says "Playing … from a stated start". If the view was in the inspection mode, the interactive mode is turned on first, because playback runs only there.
  - A line of the trial: Room 08 opens, the Elders' trial plays, and its first line "Parece que los Ancianos están ocupados." is on screen after about 8.5 s ([trial-from-game-texts-app.png](trial-from-game-texts-app.png)). Escape ends it in Room 09 about 1.5 s later.
  - A line of the opening: the boot stops where script 001 starts script 044 (3 frames after Room 69's assumed answer) and Room 01 shows its points of light within 0.5 s ([opening-from-game-texts-app.png](opening-from-game-texts-app.png)).
- The button plays the whole cutscene from its start, not from the chosen line. Which script a text belongs to is the extractor's `ScriptId` (the indexed script that holds the `print`), so a line printed by a room-local script that a cutscene starts would not offer the button; none of the known cutscenes has one.

## Verification

- `GameTextsCutsceneCheck` (new, [game-texts-cutscene-check.txt](game-texts-cutscene-check.txt)): 0 failures. The real `MainWindow` and the real `TextGalleryWindow`, built as `GameTexts_Click` builds it (the extraction and `GalleryCutscenes()`), but shown modeless so the check can drive it; the button is clicked through its `Click` event. No physical input.
- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 229/229.
- Not rerun: CutscenePlaybackCheck and OpeningPlaybackCheck (the play methods are unchanged; the new check plays both through them), DoorwayClickCheck and the other WPF checks (no change to room clicks, doorways or room loading), RoomAudit and Blockers (no Core change).

## Commands

```powershell
dotnet build .verification\game-texts-cutscene\GameTextsCutsceneCheck\GameTextsCutsceneCheck.csproj -o <scratch>\gtc
Start-Process -FilePath <scratch>\gtc\GameTextsCutsceneCheck.exe -ArgumentList "<repo>\Games\loom","<out folder>" -WorkingDirectory <repo> -Wait
```
