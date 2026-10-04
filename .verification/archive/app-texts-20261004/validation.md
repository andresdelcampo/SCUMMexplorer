# App texts review (4 October 2026)

Changed (normal-mode text only; no script numbers, resource names or "EGA background" wording):

| Where | Before | After |
| --- | --- | --- |
| Room placeholder, LOOM | Select a room to view its EGA background. | Pick a room to explore the game from there. |
| Room placeholder, Indy 3 | Select an EGA room to preview its background. | Select a room to see its background. |
| Indy 3 subtitle | EGA room background preview · object and script decoding is not yet verified. | Room backgrounds and texts from the game. |
| Scene subtitles | Original LOOM EGA artwork / EGA background preview · … | Original artwork from the game |
| Placeholder label | ROOM BACKGROUND | ROOMS |
| Welcome text | SCUMMexplorer is being built around game concepts… | Walk through rooms, trigger cutscenes, change objects and discover the game's secrets. |
| Welcome subtitle | Open a game folder to step inside. | Explore classic adventure games from the inside. |
| Status after loading / after a background | Choose a room to begin. / Background ready | Ready (the title above already says "Choose a room") |
| Costume subtitle | Static pose gallery · all stored decodable poses at the same pixel scale | Every stored pose of this costume, at the same scale |
| Tooltips | Interactive (script numbers, verbs, hover mechanics), Original framing, Other possible music, Restart | plain wording; the Interactive tooltip keeps what the user can do (click, double click, keys, how to inspect) |

Kept: the platform line ("DOS (inferred)", from the loaded version), evidence wording in Advanced panels, the detection note tooltip ("DOS EGA profile inferred from…").

Saved state: the last game is stored under `%LOCALAPPDATA%\SCUMMexplorer`: `last-folder.txt` (the folder path), `view-options.txt` (doorways, bytecode, readable, framing, options panel) and `room-cache\` (room thumbnails, one folder per game). Deleted all three on 4 October; the next start shows the welcome page. Shots: `welcome.png` (fresh start look), `loaded.png`. Screenshots render the window content without the window's own background, so the title area looks pale.
