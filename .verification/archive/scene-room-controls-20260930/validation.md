# Room departure and cutscene controls (30 September)

Catalog room changes now stop scene audio and the frame clock, clear pause state, and restore idle controls. Playback-driven room changes continue normally. The opening runs as suspended boot code rather than an open cutscene; leaving it from the catalog creates the ordinary post-boot exploration session before visiting the chosen room. This is an explorer cancellation policy, not a decoded game skip or a reached route.

Idle arrival polling no longer enables Pause or prevents a manual cutscene start. Pause requires a scene, movement, dialogue or an active draft. Sound mode and AdLib gain controls are disabled during playing or paused scenes, including the opening; the sound selection handler also rejects programmatic changes.

The same actual-WPF harness against the previously built master App (10d7322) reproduced 13 failures in [before](before/room-controls-check.txt). The fixed App passes all 19 assertions in [final](final/room-controls-check.txt): idle room entry, playing/paused trial departure, stopped voices, opening departure, no delayed restart and sound configuration restored. These use synthetic handler calls and real session/arrival clocks, not physical input or independent listening.

Required DoorwayClickCheck rooms 2, 3, 4, 8, 10 and 37: 16/16, comprising 11 game clicks and five labelled direct-verb fallbacks ([report](doorways.txt)). Committed suite: 249/249, with both game roots configured and the unrelated untracked LoomObjectLookAuditTests excluded. Core was unchanged; RoomAudit/PNG comparison and Blockers were skipped because rendering and script execution were unchanged.

Main checkout `D:\Documents\Personal\Code Projects\SCUMMexplorer`: `dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj -c Release`, zero warnings/errors. Artifact: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. Focused harness and doorway harness were built to separate ignored Exe output folders. The initial run1 report records a harness setup error (waiting for an automatically selected room); the harness was corrected to wait for the loaded resource tree before selecting a room.
