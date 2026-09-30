# Room 36 exit through the game click

Decoded geometry: object 599 has box (464,0), 16x144, walk point (494,126), no image and no parent. Its verb 0x38 loads Room 37 through loadRoomWithEgo object 601, walk to (24,101). Script 006 selects through findObject +0x0096; for a selected object it walks to the object's point at +0x00E9, waits +0x00EE and starts script 007 [object,0x38] at +0x00F2.

Fresh boot, seed 0, catalog Room 36, 32 arrival frames, click (465,1), then 250 frames: stays in Room 36. Actual class mask for 599 is 0x00800002, including class 24 (Untouchable). The click selects no object (var071=0) and walks to (465,126), not the object's (494,126); no exit sentence runs. Full source trace and class/geometry diagnostic retained. No class or variable override was injected.

Decoded gate: entry +0x0011 checks var070 against 70 (0x46), then var0AB >5; otherwise +0x0028 checks var070 against 37. The supported branches clear class 24 at +0x002F, otherwise +0x003C sets it. The seed-0 catalog arrival is taken from Room 37 object's return verb but the preceding route was not executed. Local 200 can clear the class at +0x0094 after a reached transition into box 9 (with costume/put/walk), and sets it at +0x00C3 after box 10. These conditions must not be fabricated merely to make the exit clickable.

Therefore the condition for replacing the firewood descriptor with this direct game click was not met. Existing 086 start retains its explicitly labelled direct-verb Room 36 route and subsequent Room 37 click. No App or Core changes in this task. UI still describes that start honestly; no misleading claim that Bobbin walked out through script 006.

Probe build zero warnings/errors. Existing suite 249 and arrival task's six-room doorway 16/16 / RoomAudit zero gaps remain current. No WPF rerun needed for unchanged behavior. Reproduce detailed check: dotnet run --project .verification/room36-exit-20260930/ExitProbe/ExitProbe.csproj -- Games/loom. Trace: VisitProbe Games/loom 36 +32 !465,1 +250 !109,102 +300 (second click remains in 36; it is not a valid firewood start).
