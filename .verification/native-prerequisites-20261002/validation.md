# Native LOOM prerequisites and Room41 certainty — 2 October continuation

Continued from `575d458` with the completed status/costume/scythe changes and source096/20 recipes preserved. This task changes diagnostic recipes and documentation; production code, game resources and replay catalog remain unchanged.

## Native level4 source execution

[Both orders](level4-validation.md) now execute the missing slot2/slot1 use actions and the final source gate:

* Clean trial046 → native egg270/slot0 → actual047 override/Escape → Room06 dye lesson/native207 slot2 → Room62 wheel lesson/native808 **reverse** slot1: raw4 in62,9/9 scopes.
* The same setup with the wheel before dye: raw4 in06,9/9 scopes.

029 receives(4,5) from62/local205 or(4,3) from06/local200. Learned2000 and used4000 are source-written for all three slots; D6F remains0. Raw4 and slots1/2 retain estimated provenance. Native keyboard input reads the loaded notes, including reversal; there is no supplied note pattern, status edit, browsing floor, actor placement or flag write. Catalog starting rooms and direct lesson requests remain explicit exploration choices, so these are not original-game walkthrough claims.

Failed controls remain recorded, including the forward808 response that does not mark use and the dye/gold-before-trial ordering that leaves046 open. [All level4 packet digests](level4-summary.json), [lesson controls](lessons-summary.json).

## Room41 needs more than learned-bit certainty

The native route can now use the source-executed raw4 above instead of direct323/470 level5/6 responses. It retains actual052 arrival and native325 learning, actual37/603→41 arrival, source-enabled632 look/633, decoded walk point/aim and loaded slot4 notes. Fresh [native](room41-level4.json) and [actual204 override](room41-level4-skip.json) variants both stop in41 with15 begins/14 ends after3000 settling frames.090 opens Conditional and its possible42 load +0005 is rejected. The added three balanced scopes come from the longer prerequisite route; no fresh-entry gap disappears.

A separate [direct633/41 control](room41-level4-direct.json), using that **same raw4 setup**, waits for204's actual override, presses Escape and completes090 +0730 in43,15/15. It proves the direct response can complete with raw4; it does not prove native casting completed090. The three older raw5/6 controls were also freshly repeated: native12/11, native+204 Escape12/11, direct+204 Escape12/12 in43. [Exact events/state/packet hashes](room41-summary.json).

The older native cast's event sequence localizes the estimate earlier than026:

| Source event | Certainty / passed argument |
| --- | --- |
|022 +0069 →023(0), then023(1)|Reached; no estimated argument|
|022 +0069 →023(2),023(3),023(4)|Conditional; argument0 estimated|
|022 +00C3 →026(4,0)|Conditional; argument0 estimated|
|026 +0071 →007(target,55)|Conditional|
|026 +007F →007(target,41)|Conditional; argument1 estimated|
|633/41 →204 →090 +0000/+0005|Conditional; possible42 not entered|

The loaded response word03B is0x4241 and is not itself estimated. The scan reaches Conditional **before selecting slot4**, while rejecting earlier randomized candidate drafts.022's comparisons +007D/+0092/+00A7/+00BC and reverse-capability check +00CC can read those estimates; its loop then carries `PathEstimated` into later023 calls.023 additionally derives learned/reverse/used flags by masking the estimated packed word. This is conservative whole-word/path propagation, not evidence that response41 was absent or that the204/090 implementations cannot finish. Fixing only023's learned-bit mask would leave the already-estimated022 path. No estimate set was cleared or exception added.

Any future improvement must preserve the randomized notes and earlier-path provenance and be generic across scripts/loops. A native090 success remains unestablished. Native intro's opening continuation is unchanged from the previous checkpoint.

## Preserved recipes and regressions

Fresh reruns retain the successful095 actual override/staff/092/639→096 recipe in46,4/4; full095 control remains43,4/3. All four sleep→18/369 or371→20/390 or391 combinations finish7/7 and return18. [Recipes](preserved-recipes.json), [state/events/hashes](preserved-summary.json).

Core289/289 and main Release build passed with zero warnings/errors. Full363-root audit, four established memberships, both sequences and Blockers are checked against the scope baseline by `summarize.py --checks`; see [hashed fresh reports](check-summary.json). No new WPF/RoomAudit/pixel run is claimed for this recipes/documentation-only task. Prior62/16/19/18 UI/navigation/input and76 zero-gap pixels remain dated in the previous record. Source-resource fingerprints and existing production code stay unchanged.

Replay coverage remains334 starts=84 scenes/250 reactions,168/235 sites,50 outstanding owners,123 unresolved edges. No catalog promotion. Unrelated untracked object-click/room-object-interaction work and `LoomObjectLookAuditTests.cs` are preserved/excluded from Core tests.

User clarified that no app should be running and explicitly authorized stopping any stray SCUMMexplorer instance. The process check found none. No app was launched or stopped by this task; diagnostic console processes finish themselves. No push, worktree changes or game/scummvm changes. Session token-usage tool unavailable; account limits were not used as session usage.

## Reproduction

Exact main cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`; final executable `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. Every shell invocation uses required absolute RTK `proxy`; bundled Python uses `-X utf8`. Use fresh output folders.

```powershell
python -X utf8 .verification/native-prerequisites-20261002/create_probes.py
dotnet run --project .verification/native-prerequisites-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/native-prerequisites-20261002/room41-level4-skip.json Exe/NativePrerequisitesSession-20261002/room41-level4-skip-fresh
dotnet run --project .verification/native-prerequisites-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/native-prerequisites-20261002/room41-level4-direct.json Exe/NativePrerequisitesSession-20261002/room41-level4-direct-fresh
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/source-progression-20261001/run-checks.ps1 -OutputFolder Exe/NativePrerequisitesSession-20261002/checks-fresh
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-source-audits.ps1 -Output Exe/NativePrerequisitesSession-20261002/audits-fresh
dotnet build SCUMMexplorer.slnx -c Release
```

`summarize.py` validates all recorded route outcomes and retains failed controls. Its default checkpoint directories are documented in the script; update those paths when comparing a new packet set. Full packets and binaries stay ignored under Exe. No full game text/code/images are tracked.
