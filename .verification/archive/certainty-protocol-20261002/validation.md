# Generic script cancellation and Room41 writer identity — 2 October 2026

Continued from `213ea06`, preserving palette implementation `e2d1729`. Cancellation now belongs to an individual script slot. Numeric domains and completion certainty remain conservative. [Fresh checks](check-summary.json), [slot evidence](protocol-summary.json).

## Generic cancellation correction

Three isolated old-v3 fixtures reproduced incorrect behavior before the change: a nonrecursive restart left an old copy queued for later in the frame; a child stopped its executing caller but the caller continued into its next write; a script stopped itself by number and still continued. Three other controls already passed. The seven final cases now pass, including recursive copies, replacement continuation, immediate `isScriptRunning`, and cancellation before a producer's completion write. These fixtures use temporary synthetic resources and an empty boot, not game prerequisites or route shortcuts.

`ScriptFrame.Stopped` replaces the frame-wide set of stopped script numbers. `StopRunningScript` cancels matching existing global/local slots in the executing, pending and upcoming collections. A later replacement with the same number remains distinct. A canceled caller returns from the current event without further source instructions, and canceled slots cannot resume, report running or participate in freezing. Existing estimates and palette/placement behavior are untouched.

This follows ScummVM's [`runScript`, `stopScript` and `runScriptNested`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp): nonrecursive starts stop existing copies, stops also invalidate nested callers, and a returning child resumes only a surviving caller. [`o5_stopScript`](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp) distinguishes target0 self-stop from a numbered stop. Target0 was already implemented; this task adds no replacement completion/reset behavior. No room, draft, flag or note exception is involved.

## Writer identity and correction to the prior pairing

The isolated copied-Core observer now gives each session slot a stable identity across resumes, records its causal parent, and logs scheduler turns/cancellations. Instruction dictionaries and estimate arrays remain copied; the recorder receives no mutable runtime/session reference. No new recipe input is added. All thirteen whole recipe JSON packets and every prior instruction record (projected to the prior fields) compare identical.

For raw4 mouse input, the first estimated practice031 is slot382, started by030 slot378, itself called by Room06 local201 slot373. At frame2870 it enters with estimated note1 and a known path; +0026 taints the path and +0227 writes estimatedD6C0. The later native031 slot412 starts at3026 with known note1, and its +000A condition reads that practice estimate. Its parent is020 slot408.

**The native completion write and wait both occur in frame3036:**031 slot412/run12143 writes estimatedD670 at +031D and stops at +0322; then020 slot408/run12144 reads D67 at +0028 and becomes estimated before +002D. This is the same trace chronology in the prior audit. The prior validation's frame3035 completion claim is superseded. Its analyzer filtered out same-frame writes and its summary actually selected an earlier practice completion at2909/run11960. The new analysis selects by instruction order and checks the actual writer's parent slot, rather than pairing by frame alone. Original packets and prior analysis are retained as dated evidence.

All **76 observed zero-valued020 waits** across these recipes bind to the last observed literalD670 write from their own031 child, at +031D, with no recorded cancellation of that child. This is evidence about the selected seeded explorer runs. It does not prove all paths, unknown external/aliased writers, concurrency under other inputs, cancellation safety, or that any estimate can be cleared. No early031 +0019 is observed here; the source counterexample leavingD671 remains valid. D6C's instruction-only terminal projection remains separate from scheduling/callee/cancellation proof.

The correlated source predicate remains hypothetical:162 numeric worlds select slot4 before026;52 flag cases reproduce51 comparisons/14 first-component overlaps. Splitting the three complete input tuples into54 Cartesian combinations still fabricates four earlier matches. No selected runtime input/index, path, target, dispatch, effect or native090 completion is established by those numeric domains. Source-bound domains through masked words/indexed reads/argument passing remain the next precision task.

## Fresh preservation

Core305 passes with absolute LOOM/English Indy roots and the unrelated `LoomObjectLookAuditTests` excluded. All363 literal statuses, frame counts, rooms, scope traces and resource fingerprints match the scope baseline; all four memberships and both sequences match. Blockers0. Core-only RoomAudit76 has zero gaps and all76 pixel hashes are unchanged. Final **main** Release has zero warnings/errors. No new WPF/navigation/input claim; no App launched or running, no push or catalog promotion.

All four dye notes/count remain known. Raw4 mouse/key remain41,15/14; raw6 remain41,12/11; native090 stays Conditional. Separate direct633/actual204 Escape remains43,15/15. Both level4 orders retain9/9 in62/06, estimated raw4, D6F0 and slots0/1/2 `7282/7400/7200`. Completed096 remains46,4/4; the full095 control remains43,4/3. All four source sleep→20 routes return18,7/7. All thirteen packets preserve values, estimates, actions, events, timings, rooms and scopes exactly.

The user subsequently authorized including the pre-existing tracked deletions and plan trimming in this task's commit. Their contents were not restored or edited. The unrelated verb-area change, two untracked audit directories and untracked test remain outside staging. No game/scummvm edit or worktree operation. Session token-usage tooling unavailable; account limits were not substituted.

Retained diagnostic failures: the initial six cancellation fixtures failed3/passed3 against the previous implementation, then passed6/6; the added executing-restart case passes in the full305 suite. The first protocol assertions reused the previous completion-frame assumption and failed, exposing the faulty previous-frame pairing; the final assertions require same-frame3036 and instruction-order/parent identity. One analysis run passed a string to a Path-only loader; corrected without source-state changes. A RoomAudit shell wrapper had an incorrectly escaped exit-code guard; its completed reports remain under `room-audit-1`, and fresh `room-audit-2` repeats the render/pixel checks with a saved, correctly guarded runner. No failed packet was promoted.

## Reproduction

Main cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Prefix all commands with `C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe proxy`. Python is `C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8`. Use a fresh output base instead of overwriting this checkpoint.

1. `python -X utf8 .verification/archive/certainty-protocol-20261002/create_audit.py <base>/observer-1` then `powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/certainty-protocol-20261002/run-recipes.ps1 -Output <base>`.
2. `python -X utf8 .verification/archive/certainty-protocol-20261002/analyze_protocol.py <base>` checks thirteen whole packets, all prior instruction records, slot relations and production source hashes. The generator reuses the prior pinned executor and exact unchanged recipes.
3. Source-progression-20261001 `run-checks.ps1 -OutputFolder <base>/checks-1`; status-refinements-20261001 `run-source-audits.ps1 -Output <base>/audits-1`; this packet's `run-rendering.ps1 -Output <base>`.
4. Final main `dotnet build SCUMMexplorer.slnx -c Release`, fresh process-state report, then `finalize_checks.py <base>`. Before/after defect logs are retained historical inputs to the aggregation; new runs can retain those pinned files or adjust the report list explicitly.

Rebuilt executable: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`; not launched. Generated Core copies, binaries, full recipes and instruction/scheduler logs remain ignored under Exe. Compact scripts/summaries and this record are tracked.
