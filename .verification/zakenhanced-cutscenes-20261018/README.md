# ZakEnhanced S7 cutscenes — 18 October 2026

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

## Final headless result

Shared prerequisites `0bacc96` and `2632e39` correct old call/text scanning, source attribution and native direct-object invocation context. Final `scan.txt` has **130 triggers / 54 groups / 37 static scenes**. `census.txt` runs every group in its own fresh boot: **13 begin**, 41 do not begin within 600 trial frames; 155 seconds on four independent-reader workers. Of the 13 begun, **five reach the 6000-frame bound**, so begun does not mean completed: keys `2/0/0`, `11/0/0`, `11/249/1`, `14/55/11`, `23/410/253`. Other remaining branches need state not earned by the preview; no flags, inventory or story prerequisites were fabricated.

Three representative triggers selected by the agent (the user named none) pass source-offset and fresh-run tests:

| Trigger | Attributable source | Final run |
|---|---|---|
| Room 08 · Entry (`8/0/0`) | Script 140 cutscene +0x0004; first text +0x0014; end +0x036E | 911 frames, visits Rooms 0 and 16 |
| Room 03 · Push doorbell (`3/147/9`) | Object 147 +0x0067 starts Script 39; cutscene +0x0000..+0x015E | 142 frames |
| Room 07 · verb 0x11 tool kit (`7/34/17`) | Object 34 +0x002A starts Script 24; cutscene +0x0000..+0x02EE | 256 frames |

Facts and branch meanings are in [ZAK_SCRIPT_MAP.md](../../docs/zak/ZAK_SCRIPT_MAP.md). Buy's name is absent from the boot verb table (Room 7 installs it later), so the generic title honestly retains `verb 0x11`. These are direct exploration actions, not native clicks or a continuous story route. The room visit may choose a labelled preview state. Static categories/possible texts do not prove which branch ran.

```
rtk proxy dotnet Exe/ZakEnhancedCutsceneCheck/Debug/net10.0/ZakEnhancedCutsceneCheck.dll "D:/Juegos/Lucas/zakE ega en" scan
rtk proxy dotnet Exe/ZakEnhancedCutsceneCheck/Debug/net10.0/ZakEnhancedCutsceneCheck.dll "D:/Juegos/Lucas/zakE ega en" prun 4
rtk proxy dotnet Exe/ZakEnhancedCutsceneCheck/Debug/net10.0/ZakEnhancedCutsceneCheck.dll "D:/Juegos/Lucas/zakE ega en" probe 8 0 00
rtk proxy dotnet Exe/ZakEnhancedCutsceneCheck/Debug/net10.0/ZakEnhancedCutsceneCheck.dll "D:/Juegos/Lucas/zakE ega en" probe 3 147 09
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~ScummZakEnhancedCutsceneTests
```

Four tests pass (19.7 seconds). The first sandbox testhost connection timed out; elevated retry passed. `before-native-context.txt` preserves the intermediate 12-begun census. The second full census followed the shared invocation fix, not an attempt to alter story outcomes. Shared regression and final full suite belong to the parent integration record.

Parent real-window acceptance passes: 130 triggers/54 groups/37 static scenes/13 starts, background 135.9s in the paired run. Room 8's list displays Script 140's intertitle and the global picker displays Script 39's baker response; captures were inspected. User checks: open the fixture, Room 8's cutscene list, play `Room 08 · Entry` (source trace starts after 40 frames); then Advanced global picker (All playable filter) `Room 03 · Push doorbell`, hear/read the baker's response. Check Space pause/resume and Escape when enabled. Perceived timing, original-game visual comparison and S5/S6 limits remain open. Cutscenes is Partial; no Full catalog claim. No seed or game-data modification. Final suite/merge evidence is in the parent checkpoint.
