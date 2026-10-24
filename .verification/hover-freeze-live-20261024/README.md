# Maniac enhanced hover freeze, live route, 24 October 2026

User steps (verbatim): "Opened MMansion Enhanced / Play Game Intro -choose two kids, start, skip cutscene / Then expand options and put Hotspots as On hover / Then hover over the cartel, it starts to get sluggish, until it stops responding", later adding "try also hovering OUTSIDE of the cartel -in and out".

Result: **not reproduced**. No cause found, no fix made; a bounded-cost guard and requirement R-6.48 were added.

## What ran

1. Headless (`HoverCostGuardTests`, from the first probe): `ScummGameSession` of `mmansionE ega es`, the game's own clicks for the two kids (76,104; 196,104; 292,60), Esc at the cutscene's skip point, control in Room 44 (the sign is object 278 at 8,80, 32x24). Pointer on the sign, 3,600 frames of pointer + frame + `ScummPlayScreen.Scene()` + `ScummRoomOverlays.Objects` + `Compose`: frame 0.09 ms, scene 0.15 ms, objects 0.002 ms, compose 2.4 ms, flat over the 12 chunks; trace +50 lines per 300 moves; memory sawtooth 28-48 MB.
2. Real `MainWindow` (`LiveHover/Program.cs`, private profile, window at Left -12000 so no screen or input device is touched): open game, `GameIntro_Click`, the kids by `ClickScreen`, Skip button, options expander, Hotspots On hover, then 6,000 (stay on the sign, `out-before/live.txt`) and 8,000 (sweeps in and out at six speeds, `out-sweep-hover/live.txt`) moves through the window's move body (`PointAtScreen` + `UpdatePlayOverlayHover`), a Normal-mode 10 ms dispatcher heartbeat: body 0.17-0.28 ms mean, <1.5 ms worst, heartbeat gap at most 31 ms (one 108 ms), overlay children 0/1, HOTSPOTS rows 23 constant, trace +8 per 250 moves, managed memory sawtooth 170-300 MB, working set flat 590-640 MB.
3. One real-pointer run each (Win32 `SetCursorPos`, 20-25 s, pointer restored): stay on the sign, in-and-out sweeps with On hover, in-and-out sweeps with Always. Heartbeat gap at most 31 ms, input turnaround at most 31 ms, box drawn and removed (overlay children 0/1), memory flat. `out-real*/live.txt`.

## Read, not found

`PlayScreenImage_MouseMove` -> `PointAtScreen` + `UpdatePlayOverlayHover` -> `RefreshPlayOverlays` (clears and redraws one canvas, `IsHitTestVisible=false`; unchanged pointer returns early; Advanced-only row lighting): no subscription, timer, popup or tooltip is created per move; the game's pointer only affects the v2 verb highlight, not objects.

## Not covered (ideas for the user's machine)

Large or high-DPI maximised window, the user's saved view options (Doorways mode, Roland sound), a long session, software rendering. If it reproduces, a process dump of the stuck app (Task Manager, Create dump file) would show the stuck thread. Run: `LiveHover.exe <game folder> <out> <moves> [synthetic|real|advanced] [Never|Hover|Always] [stay]`.
