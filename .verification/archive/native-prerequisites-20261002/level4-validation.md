# Native level 4 prerequisites — 2 October continuation

Continued after `575d458`. Production code, source recipes096/20, status controls, costumes and the scythe background are unchanged. These probes execute loaded source and synthetic native input, with seed0 and explicit catalog starting rooms/direct teaching requests. They are explorer source routes, not an original-game playthrough. No flags, actor placements, note sequences or known-state sets are injected; catalog staff floors are disabled.

## Both final-use gates execute029(4)

The existing `level4-skip.json` recipe first executes the Elders' trial046, reaches the egg270, plays loaded slot0 through native keyboard input, waits for047's actual override and presses Escape. Source270/3A then marks slot0 used through034. Its clean trial must precede the estimated dye/gold casts in these recipes.

| Remaining source action | Loaded native input | Executed source result |
| --- | --- | --- |
| Room06/205 look requests local201's lesson | Walk to207's decoded walk point, aim207, play loaded slot2 | 207/3E → local206 → local200; local200 +00CF loads slot2 and +00DA runs034(2) |
| Room62/810 look requests local200's lesson | Walk to808's decoded walk point, aim808, play loaded slot1 **backwards** | 808/3D → local201 → local205; local205 +000B runs034(1) |

Forward slot1 at808 reaches the already-transformed-pile response207 and does **not** mark slot1 used. The reverse action is established from its actual loaded reverse response3D and capability1000, not a fixed note sequence. All four808/809 forward/reverse controls are retained in [lesson evidence](lessons-summary.json).

Two fresh orders reach raw4 with nine balanced scopes:

* [egg → dye → reverse gold](level4-egg-first.json): final Room62,3472 total frames; local205 +002C chains029(4,5).
* [egg → reverse gold → dye](level4-dye-last.json): final Room06; local200 +0111 chains029(4,3).

Both retain D6F=0 and source-written learned2000/used4000 in all three slots. Final packed words are0x7282,0x7400,0x7200 for slots0/1/2. The final level4 and slots1/2 remain estimated;029's chain is Conditional because its prerequisite checks read words derived from randomized drafts. This establishes source execution of the missing use actions and the level4 gate, without claiming known original-game prerequisites or changing certainty policy. See [state/events/packet hashes](level4-summary.json).

The earlier opposite setup (`level4-combined.json`: dye/gold before046) marks slots1/2 used but leaves046 open6/5 and never obtains047's override. This failed control remains hashed; it is not a successful level4 recipe. The initial forward-wheel control is also retained. No failed packet was removed.

## Reproduction

Main cwd: `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Prefix every shell command with `C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe proxy`. Python is the bundled runtime at `C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`, with `-X utf8`.

```powershell
python -X utf8 .verification/archive/native-prerequisites-20261002/create_probes.py
dotnet run --project .verification/archive/native-prerequisites-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/archive/native-prerequisites-20261002/level4-egg-first.json Exe/NativePrerequisitesSession-20261002/egg-first-fresh
dotnet run --project .verification/archive/native-prerequisites-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/archive/native-prerequisites-20261002/level4-dye-last.json Exe/NativePrerequisitesSession-20261002/dye-last-fresh
```

The generator copies the prior read-only probe and adds only optional reverse ordering of the four loaded notes. Reflection reads pending slots, runtime estimates and actual overrides. Inputs and source execution remain the only mutations. Required-room/override failures stop dependent actions. Settling requires32 idle frames, inactive draft and balanced scopes.

No replay promotion:334 starts/168 of235 sites/50 outstanding owners/123 unresolved edges remain. [Continuation and fresh checks](validation.md).
