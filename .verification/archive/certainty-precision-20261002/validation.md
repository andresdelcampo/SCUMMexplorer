# Room41 certainty precision continuation — 2 October 2026

Continued from `5e86a68`. A separately committed [generic palette fix](palette-validation.md) removes a false geometry dependency while retaining appearance, placement and source-path provenance. The remaining work here is read-only source research and isolated diagnostics. No practice/input/level/learned/used/actor prerequisites were injected, no estimate sets were cleared, and no catalog entries were promoted.

## Fresh native result

The thirteen established recipes were rerun in fresh sessions. Raw4 mouse/key still end41,15/14; raw6 mouse/key still end41,12/11. Script090 is Conditional in every native control. The separate direct633/actual204 Escape control ends43,15/15. Both source level4 orders still end9/9 in62/06, raw4 estimated, D6F0 and packed slots0/1/2 `7282/7400/7200`. Completed096 remains46,4/4; its ordinary full095 control remains43,4/3. All four sleep→369/371→390/391 routes return18,7/7. Every recipe retains its exact prior timing, reached-room history and source scope sequence. Native mouse/key downstream events still agree exactly. [Compact results, provenance deltas and packet hashes](precision-summary.json).

## Input is more precise; the completion handshake remains estimated

At the prior checkpoint, dye's second note/count became estimated because `031 +0254` marked ego geometry at a palette operation. With the fix, all four dye note writes056..059 and count062 remain known. `PaletteEstimate` keeps the exact operation's source reason, and no placement estimate is erased. Later actual object walks still carry their own estimate.

Native gap snapshots locate a remaining propagation point. After dye's second input, `draft-2-gap-2-5` has script020 paused at0028, `PathEstimated=false` and known D67=1. One frame later, `draft-2-gap-2-6` has D67=0 estimated and script020 paused at0060 with `PathEstimated=true`; entered notes/count remain known. Source020 +0028 reads D67, then +002D increments its note cursor. Source031 begins by setting D67=1 (+0000), and its later completion writes D67=0 (+031D or +0243) behind the estimated practice path. This source and frame evidence points to the completion-wait predicate, not note entry, as the next propagation dependency. Snapshots bound the frame; they do not observe the intermediate instruction.

[Gap recipe](room41-gaps.json) adds only `recordDraftGaps:true` to the bytecode-input recipe. The opt-in probe records32 additional snapshots and changes no input, timing, event sequence, room or scope. [HandshakeAudit](HandshakeAudit/Program.cs) copies captured globals, local values, estimated locals and **the inherited path** into an isolated runtime, and executes only the pending020 +0028 condition. Thirty slices retain the captured path. They deliberately do not clear it to manufacture a new first-branch trace. Earlier slices from the four final casts still locate010/022's first estimated branch at0000 for raw4, and022 +007D for raw6.

By the final raw4 cast, mode064, target067, count062, input056..059, raw07A and D70 still have estimates. The palette fix therefore does not establish native090 completion. Direct633 remains a separate exploration control.

## Practice completion and masked randomized drafts

[Read-only analysis](analyze_precision.py) pins the same disassembly SHA256 and derives these bounded facts:

* D6C has two literal writers:030 +0000 sets1,031 +0227 sets0. An instruction-only031 CFG, tracking D6C while following both edges of all other conditions, produces0 at every source terminal for initial0 and1 (97/117 visited states). This is a potential numeric completion summary, not a scheduler/callee/cancellation or original-game proof. It is not used by production.
* The analogous D67 CFG has a counterexample to blanket completion clearing:031 can stop itself at0019 with D67=1, while its later stops leave0. A generic protocol refinement must distinguish the actual completion site and concurrent/callee effects. A path reaching the last frame and a script stopping somewhere else are different evidence.
* All seven023 masks are extracted from loaded code. The boot alternatives have singleton learned/reverse/used projections in several masks while notes can vary. An optional later learned/use OR can also produce `{0,1}`; proving boot bits does not prove a Conditional source write happened. An estimated slot/index can still select a different word or response.
* The51 established slot4/earlier forward and source-capable reverse comparisons still have zero full-sequence collisions. **Fourteen** have an equal first component before a later mismatch. Whole-tuple correlation therefore matters: independent per-note certainty cannot prove all earlier rejections at the first conditional. OR2000/4000 preserves the low12 notes for every derived alternative.

No source-domain result establishes the currently selected notes, runtime learned/use state, input/movement history or branch provenance. The next generic solution needs separate numeric value domains and effect/path certainty, complete tuple/predicate comparisons, and source-established synchronization. Resetting flags/loops or removing estimates would lose those distinctions. Native opening remains unresolved.

## Fresh regression and preserved work

Core298 including nine palette cases passes with absolute game roots and the unrelated untracked audit excluded. All363 literal statuses/frames/rooms/scopes and source fingerprints match the scope baseline; four memberships and both sequences match. The final literal packet is `literal-final`: the first sweep began before the last unknown-color regression case was added, so literal roots were repeated against final code. Blockers0. RoomAudit76 has zero remaining gaps; all76 current renders have identical pixels. Main Release rebuild is clean. [Checks and hashes](check-summary.json). No WPF run is claimed: the user required no app running, and no App was launched. No game/scummvm changes, catalog promotion, worktree operation or push.

The initial unrelated object-click/room-object-interaction work and untracked test were excluded from staging. Additional unrelated tracked deletions and plan trimming appeared during this session; no command in this task removed those paths. They were left unstaged and were not restored or incorporated into either completed task. Only this task's new Current follow-up paragraph was staged in the plan, using a patch against the captured pre-handoff working text. Session token-usage tool unavailable; account limits were not used as session usage.

Diagnostic failures are retained/explained: the initial focused helper asserted null for two expected limitation strings, then was corrected; an isolated CertaintyAudit attempt ran before its packet directory existed and failed under HarnessFailurePolicy, then passed at `certainty-2`; an early analysis assertion expected the first handshake branch between frame snapshots, but it had already occurred inside a frame, so the final record preserves that limit rather than removing inherited path certainty. An early final-check aggregation preceded the still-running Blockers output; the final aggregation waits for its completed report. Console command quoting/read-only inspection errors made no source changes.

## Reproduction

Main cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Prefix every command below with required `rtk proxy`. Use fresh output folders and update the Python scripts' packet paths before another run. Run each dependent diagnostic after its input finishes.

```powershell
dotnet run --project .verification/archive/room41-certainty-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/archive/room41-certainty-20261002/room41.json Exe/CertaintyPrecisionSession-20261002/room41-1
dotnet run --project .verification/archive/room41-certainty-20261002/CertaintyAudit/CertaintyAudit.csproj -c Release -- Games/loom Exe/CertaintyPrecisionSession-20261002/room41-1 Exe/CertaintyPrecisionSession-20261002/certainty-2/traces.json
python -X utf8 .verification/archive/certainty-precision-20261002/create_gap_probe.py
dotnet run --project .verification/archive/room41-certainty-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/archive/certainty-precision-20261002/room41-gaps.json Exe/CertaintyPrecisionSession-20261002/gaps-1
dotnet run --project .verification/archive/certainty-precision-20261002/HandshakeAudit/HandshakeAudit.csproj -c Release -- Games/loom Exe/CertaintyPrecisionSession-20261002/gaps-1/room41-raw4-click.json Exe/CertaintyPrecisionSession-20261002/handshake-gaps-1/traces.json
python -X utf8 .verification/archive/certainty-precision-20261002/analyze_precision.py
python -X utf8 .verification/archive/certainty-precision-20261002/finalize_checks.py
```

Run the prior ProgressionProbe likewise with unchanged `level4-egg-first.json`, `level4-dye-last.json` and `preserved-recipes.json` into `egg-first-1`, `dye-last-1` and `preserved-1`. Regression commands are in the palette validation. Final main command: `dotnet build SCUMMexplorer.slnx -c Release`; executable `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. Full packets/binaries/renders remain ignored under Exe; only compact source-grounded evidence is tracked.
