# Room41 instruction and completion provenance — 2 October 2026

Later slot-identity audit corrects the completion pairing below: the native031 +031D writer and020 +0028 wait both occur at3036, in child412/parent408. This report's3035 claim is superseded; the original analyzer excluded same-frame writers and the original summary actually chose practice completion2909/run11960. The original trace chronology is unchanged and compares exactly. Use the later instruction-order/parent-identity analysis for writer pairing. [Correction and fresh evidence](../certainty-protocol-20261002/validation.md).

Continued from `e8c1c75`, preserving palette implementation `e2d1729`. Production Core/App/catalog sources are unchanged. The new diagnostic observes an isolated compiled copy of Core; it supplies no prerequisites, removes no estimates and changes no recipe actions. [Compact instruction evidence](trace-summary.json), [fresh checks](check-summary.json).

## Directly observed dependency

The raw4 mouse recipe now establishes this instruction chronology (frames are the probe's elapsed source frames):

| Frame | Source | Observation |
| --- | --- | --- |
| 2870 | Room06 local201 +0028 →030 | A Reached call passes note1 with estimated argument0. Local201 read the candidate buffer at +0021 after023(2). |
| 2870 | 030 +0000 →031;031 +0026 | D6C is set to known1. Script031 enters with a known path and estimated note1, then its note-selection condition makes the path estimated. |
| 2870 | 031 +0227 | The literal D6C0 write retains path provenance and becomes estimated. |
| 3026 | 031 +000A | A known entered note1 reads the earlier estimated D6C0; the practice condition makes this new031 path estimated. |
| 3035 | 031 +031D | The completion write leaves D67 at estimated0. |
| 3036 | 020 +0028 →002D | The wait reads estimated D67, then the following increment is observed with `PathEstimated=true`. Cursor0 and entered notes/count are still known before the branch. |

These are consecutive instruction observations within the executor's actual session/scheduler runs, including resumed frames. They supplement the earlier gap snapshots; those older snapshots still do **not** observe an intermediate instruction. The trace does not remove inherited path provenance to recreate a first branch.

The dye's four entered notes/count remain known at their writes. Palette uncertainty remains separate from geometry. Later movement, final input/mode/target/index, raw4 and D70 provenance remain separate dependencies. Source-note value domains may constrain a numeric result while its conditional source effects remain estimated.

## Completion is distinct from cancellation

All observed031 completion writes in the thirteen recipes leave numeric D670; their source/path estimates remain intact. No031 early-stop0019 is observed in these recipes. That absence does not refute the source counterexample or establish cancellation safety.

The decoded early path requires D6C0 and note > raw07A (old-v3's following operand is compared against the first). It starts011 at +0016, then stops031 at +0019 with D671. Script011 +0013 starts021, whose +0000 stops020. This is a source cancellation edge, different from031's completion writes +0243/+031D. Decoded calls alone do not prove their reached scheduling or callee completion. Dynamic/recursive restarts, concurrent writes, changed limits and interrupted frames are not normalized away. D6C's prior instruction-only terminal projection is still not a scheduler/callee/cancellation proof.

The next implementation needs numeric domains and provenance for the actual writer/caller/index and protocol instance. A global flag or last-frame reset cannot substitute for those identities. No production protocol/domain/reset implementation is included here.

## Preservation and fresh verification

The isolated observer's **entire JSON recipe packets** equal the thirteen previous palette-precision packets: events, snapshots, estimates, timings, rooms, scopes, actions, traces and uncertainty lists. Raw4 mouse/key remain41,15/14; raw6 remain41,12/11, all native090 Conditional. Separate direct633/actual204 Escape remains43,15/15. Both native level4 orders remain9/9 in62/06 with estimated raw4, D6F0 and slots0/1/2 `7282/7400/7200`. Completed096 remains46,4/4; ordinary095 remains43,4/3. All four source sleep→20 routes remain18,7/7. This equality validates observer transparency on these recipes, not every possible execution.

Fresh production checks: Core298 with absolute LOOM/English Indy roots and the unrelated untracked audit excluded. All363 literal statuses/frames/rooms/scope traces and source fingerprints equal the scope baseline; four memberships and both sequences equal it. Blockers0. Core-only RoomAudit76 has zero gaps and all76 pixel hashes unchanged. Main Release has zero warnings/errors. No WPF/input check is newly claimed. No App was launched; final process count0. No push, catalog promotion, game/scummvm changes or worktree operation.

The original unrelated tracked deletions, verb-area change, plan trimming and untracked work remain untouched and unstaged. No restore was performed. Session token-usage tooling is unavailable; account limits were not used as session usage.

Failures retained: `observer-1` failed to compile because the generator initially named a nonexistent Core catalog resource; Core has no such resource. That unnecessary inclusion was removed and fresh `observer-2` compiled cleanly. An inspection command used incorrect nested PowerShell dollar escaping and printed errors; it made no source or process changes. The final check aggregation reads only completed reports, including Blockers.

## Reproduction

Main cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Prefix commands with required `rtk proxy`. Python here is `C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8`. Choose a fresh output directory; the generator refuses to overwrite one and verifies the exact production executor hash.

1. `python -X utf8 .verification/certainty-provenance-20261002/create_audit.py Exe/CertaintyProvenanceSession-20261002/observer-2`
2. Build the generated `observer-2/Audit/Audit.csproj -c Release`; run it with `--no-build -- Games/loom <recipe> <output-folder>`. Recipes are unchanged `room41.json`, `level4-egg-first.json`, `level4-dye-last.json`, `preserved-recipes.json` from `.verification/room41-certainty-20261002/`; outputs are `room41-1`, `egg-first-1`, `dye-last-1`, `preserved-1`.
3. `python -X utf8 .verification/certainty-provenance-20261002/analyze_trace.py Exe/CertaintyProvenanceSession-20261002`. Pass the corresponding output base on reruns; adjust its `observer-2` manifest reference when changing that folder name.
4. Use source-progression-20261001 `run-checks.ps1 -OutputFolder <base>/checks-1`, status-refinements-20261001 `run-source-audits.ps1 -Output <base>/audits-1`, Core `RoomAudit` with `<base>/room-audit-1`, and draft-scopes `compare-pixels.ps1` to `<base>/png-comparison.txt`.
5. Rebuild **main** `dotnet build SCUMMexplorer.slnx -c Release`, record `final-release-build.txt` and a fresh `app-state.txt`, then run `finalize_checks.py <base>`.

The observer passes copied dictionaries/estimate arrays to its recorder, never a runtime/session reference. Generated source, binaries, full packets and instruction logs stay ignored under Exe. Final executable: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`; it was rebuilt and not launched.
