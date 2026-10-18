# ZakEnhanced S7 baseline — 18 October 2026

Fixture: `D:/Juegos/Lucas/zakE ega en`, English DOS SCUMM v2. This is a preserved failed baseline, not S7 acceptance or a registry claim. The user named no scenes; representative source triggers will be selected from the corrected scan.

The existing shared scan found 7 triggers, 7 groups, 0 static scenes; 4 began in fresh-start runs. It missed Room 2 entry's `startScript 0x7E` at +0x0049, which reaches Script 126's cutscene +0x0004..+0x01CA. The scanner required a comma after literal script numbers, while v2 has a single operand. Its text matcher also required `text,` rather than the v2 `[text "..."]` form. These shared issues were sent to the Core owner; no local engine workaround or handwritten catalog was added.

Commands (worktree `.worktrees/zak-enhanced-s7`, all through RTK):

```
rtk proxy dotnet build .verification/indy3-cutscenes-20261017/CutsceneList/CutsceneList.csproj --ignore-failed-sources -p:NuGetAudit=false
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:/Juegos/Lucas/zakE ega en" scan run
rtk proxy dotnet build .verification/zakenhanced-cutscenes-20261018/CutsceneCheck/ZakEnhancedCutsceneCheck.csproj --ignore-failed-sources -p:NuGetAudit=false
```

Builds passed with zero warnings/errors. `baseline.txt` retains the full initial census. The family harness preserves the existing scan/run/probe modes and adds `global <ids>` and `entry <rooms>` for source-offset proofs. Source dumps for Scripts 126/24/29 and Room 2 are local generated evidence.

ScummVM comparison is source-only: [script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp), read 18 October 2026, `o2_cutscene`, `o2_endCutscene`, `o2_beginOverride`. They save/restore interface state, freeze other scripts and register the skip continuation. ScummVM was not executed. Source execution does not prove original-game timing, visual fidelity, or a full story route. S5/S6 retained limitations remain unchanged.

Status: awaiting shared scanner fix, final census, three source-established fresh-start proofs and parent WPF verification. Cutscenes remains unchanged at None until acceptance. No seed, forced state or game-data modification.
