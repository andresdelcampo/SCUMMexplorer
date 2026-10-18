# ZakClassic S7 cutscenes — 18 October 2026

Family ZakClassic; fixture `D:/Juegos/Lucas/zak ega en`, English DOS SCUMM v1. This separate family run uses its own bytes and fresh boots. No scene was named by the user: the agent selected three source-established examples from the derived scan, not a handwritten catalog.

Shared prerequisites `0bacc96` and `2632e39` fix old literal-call/text syntax, native source attribution and direct object invocation globals. Initial `baseline.txt` preserves the faulty static scan (7 triggers/groups, no scenes); `before-native-context.txt` preserves the intermediate census before invocation context was corrected. No family Core changes were made.

## Results

`scan.txt`: **130 triggers, 54 groups, 37 static scenes**. `census.txt`: all 54 groups run independently from boot, **13 begin**, 41 do not begin within 600 trial frames, 147 seconds on four workers with separate readers. Five begun entries hit the 6000-frame bound: `2/0/0`, `11/0/0`, `11/249/1`, `14/55/11`, `23/410/253`. These are not claimed completed. No prerequisites are fabricated to make the remaining branches begin. The scene classifications and possible text counts are static evidence.

| Selected proof | Source | Fresh-run result |
|---|---|---|
| Room 08 · Entry (`8/0/0`) | Script 140 +0x0004 cutscene, +0x0014 first line, +0x036E end | Begins and ends within 911 frames, visits Rooms 0 and 16 |
| Room 03 · Push doorbell (`3/147/9`) | Object 147 +0x0067 starts Script 39; +0x0000 cutscene, +0x0078 first baker response, +0x0166 end | 142 frames |
| Room 07 · verb 0x11 tool kit (`7/34/17`) | Object 34 +0x0026 sets price 45, +0x002A starts Script 24; +0x0000 cutscene | 256 frames |

The source facts are in [ZAK_SCRIPT_MAP.md](../../docs/zak/ZAK_SCRIPT_MAP.md). Four tests pass (18 seconds): counts/native titles plus the three independent source-offset/fresh-start proofs. Initial Classic title assertion failed because the test-generation command decoded UTF-8 as ANSI; corrected the file encoding and reran all four successfully. No game text was changed. Source offsets differ where the Classic edition's actor operations differ; Enhanced offsets were not assumed.

Live trace probes: Room 8 is loaded as pseudo 144; frame 40 records `cutscene at Script 140 +0x0004 (native v1/v2 scope)`. The doorbell records `cutscene at Script 39 +0x0000 (native v1/v2 scope)` immediately, ends at probe frame 131. The planner's 142 frames include the room visit. Traces are committed beside this record.

## Commands

Working directory `.worktrees/zak-classic-s7`; generated artifacts `Exe/ZakClassicCutsceneCheck/Debug/net10.0/`.

```
rtk proxy dotnet build .verification/zakclassic-cutscenes-20261018/CutsceneCheck/ZakClassicCutsceneCheck.csproj --ignore-failed-sources -p:NuGetAudit=false
rtk proxy dotnet Exe/ZakClassicCutsceneCheck/Debug/net10.0/ZakClassicCutsceneCheck.dll "D:/Juegos/Lucas/zak ega en" scan
rtk proxy dotnet Exe/ZakClassicCutsceneCheck/Debug/net10.0/ZakClassicCutsceneCheck.dll "D:/Juegos/Lucas/zak ega en" prun 4
rtk proxy dotnet Exe/ZakClassicCutsceneCheck/Debug/net10.0/ZakClassicCutsceneCheck.dll "D:/Juegos/Lucas/zak ega en" probe 8 0 00
rtk proxy dotnet Exe/ZakClassicCutsceneCheck/Debug/net10.0/ZakClassicCutsceneCheck.dll "D:/Juegos/Lucas/zak ega en" probe 3 147 09
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~ScummZakClassicCutsceneTests
```

Build passed without warnings/errors. Harness also accepts `global <ids>`, `entry <rooms>` and `dump <room> <object> <verb hex>` for independent source verification. Final census followed the shared native invocation fix; intermediate output is retained. Parent owns final full-suite and WPF checks.

## Comparison and retained limits

Source-only comparison: [ScummVM script_v2.cpp](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp), read 18 October 2026, `o2_cutscene`, `o2_endCutscene`, `o2_beginOverride`: save/restore interface state, freeze other scripts and register the skip continuation. ScummVM was not launched. These tests establish source execution, not original-game audiovisual timing or full-story fidelity.

Preview visits and direct verbs are exploration actions; they do not replay native clicks or earn the full story state. Buy17 is created in Room 7, absent from boot's verb table, so the title keeps the raw unknown verb. Five capped runs, the inherited attribution limits, S5 visual comparison limits and Classic's eleven estimated costume colours remain explicit. No seed, forced story state, junction or game-data modification.

Parent performs window acceptance and registry/matrix change; Cutscenes is eligible for Partial afterward, never Full from this census. User check: open this fixture, use Room 8's `Play Cutscene` list for `Room 08 · Entry`, then Advanced global picker `Room 03 · Push doorbell`; check scene artwork/sound and Space pause/Escape when enabled. The first line is "Meanwhile, back in the secret room^"; the baker says the shop is closed and out of fresh bread. Perceived timing remains a user check.
