# Actual boundary phases and inventory access preservation - 2 October 2026

Continues `f84460f`, preserving `3f32e5f`, `32f1ecb`, `a3a90ec` and every earlier handoff checkpoint and constraint. Implementation commits are `6ad392d` (initialization/host/caller phases), `4b7c646` (implicit ego/owner/inventory access and inventory notification), `3ed2da7` (child end/restoration phases),and `6c06692` (independent opaque caller effects).

## Bounded changes

[Boundary validation](order-validation.md): ordinary child initialization requires an aligned begin at offset0 before host return. Deferred chains require their observed host return and terminated chaining caller;opaque caller-end effects remain an independent rejection and do not suppress actual child assignment. Host site/instruction/path remains the original dispatch. Caller continuation requires instruction phase; caller/child end requires end phase; restored locals require begin phase. Nineteen new cases reproduce twelve previously accepted malformed copies and retain seven controls. The complete incoming dispatch from observed initialization and continuity before the outgoing call remains mandatory. Missing/native/omitted/scheduler/cancellation/replacement/exhausted-bound evidence cannot donate correspondence. An opaque caller-end effect barrier stays an independent caller rejection and does not suppress the actual child assignment. No stream field, ID, bound or execution policy changes.

[Inventory validation](inventory-validation.md): exact setOwnerOf keeps its explicit target/source bindings and appends the implicit ego read. Owner and inventory membership are independent copies with unknown provenance, explicit unresolved reasons and the original effect barrier. Selected target/index filters suppress native copies; ego remains an independent operand. Inventory additions/removals notify the selected family even for repeated additions/absent removals. The existing conservative owner notification remains. Raw/unattached/disposed limits remain; synthetic lifetime controls establish no loaded inventory lifetime. All five stringOps forms retain opaque contents/length/lifetime and inline text is not scanned for numeric reads. Fifteen new cases preserve estimates, notification boundaries and execution/legacy transparency.

Child end/yield/restoration, host return, caller continuation/end, restored locals and writer terminals remain independent. Argument equality supplies no local origin or successful return. Structural triples establish no completion. No domain/effect transport, estimate clearing, injected prerequisite, room exception or shortcut is implemented.

## Fresh current-source evidence

Authoritative output: `Exe/RuntimeBoundariesFinalSession4-20261002`. All completed report hashes and current-source manifests are required by [check-summary.json](check-summary.json).

| Check | Result |
| --- | --- |
| Core / focused | 622 / 296 pass;34 new cases:19 boundary phase and15 inventory/string |
| Primary evidence | All13 whole packets and entire latest instruction/access/binding/relation/cancellation/expression/call/selection/correspondence/completion streams exact;all inherited and expanded assertions pass |
| Primary correspondence | 366 bindings,55 triples,0 caller resumptions;76 original writer terminals,0 new original writer resumed-terminal chains |
| Primary native fields | 15 prior message fields retain unknown provenance;0 camera waits |
| Separate unchanged raw4 objects | 16 bindings,14 independent child terminals,9 triples,3 caller resumptions,195 child restorations,1 per-call128 rejection;entire call/selection/correspondence streams exact;loaded fallback0 |
| Separate camera-only raw4 | Exact recipe packet;55 waits/220 copied fields,unknown provenance,no loaded lifetime or completion claim |
| Roots / source | 363 exact statuses/frames/rooms/scopes/fingerprints;four memberships and both sequences exact;Blockers0 |
| Rendering | 76 unchanged Core renders,0 gaps,0 changed pixels;no new WPF/physical-input/listening claim |
| Numeric report | Entire162-world/52-flag/51-comparison/14-overlap/four-fabricated-match/12-practice report unchanged |
| Native challenge | 3 expressions/17 actual steps/36 frames;every input/frame exact;11 reads (1 input,5 x,5 y)/11 explicit invalidations |
| Main Release | Final solution build after all harnesses:0 warnings,0 errors |
| Protected work / App | All57 original hashes exact/unstaged;App0,no launch,no push |
| Literal-site coverage | 168/235=71.49%,unchanged;334 starts,50 outstanding owners,123 unresolved edges;broader plan has no fixed denominator |

Primary3036/4552->4553->4556->4557,practice201->030->031 and local201 continuation remain independent. Separate object chronology IDs/estimates are retained,including Script007 +002E->0034 in slots291/349/435 and incoming dispatch213/269/347. Neither the separate capture nor the synthetic controls add an original writer resumed-terminal chain or loaded-route completion. All bounds,estimates,historical origins,writer/alias/external/concurrent/cancellation/replacement/scheduler/callee limits,nested chronology,native lifetimes,raw same-value notifications and fully observed writer resumed-terminal chains remain independent. Native opening/090,early0019/D671,D6C projection and every route/UI/palette constraint remain.

## Diagnostics and reproduction

`Exe/RuntimeBoundariesSession-20261002/order-before` retains10 failures/5 controls;order-after passes77. child-phase-before retains2 further failures/1 control;child-phase-final passes80. inventory-1 passes60 with a nullable test warning;inventory-final passes60 without it. The first full runner passed Core618/focused292/native challenge then stopped at an unquoted generated path containing spaces;no aggregate success was claimed. The corrected second runner was stopped before aggregation for the subsequent child-phase correction. Both partial output bases remain dated evidence. The third runner then caught shifted separate raw4 Event IDs from an overconstrained chain guard. A tracker-only diagnostic at frame3323/dispatch405 established that opaque caller-end effects must remain independent of actual child assignment. independent-chain-final passes81;loaded-fixed restores every original record and Event ID,without renumbering or relaxed assertions. Only the fourth complete fresh runner supplies current-source preservation.

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`,run required RTK proxy PowerShell with `.verification/archive/runtime-boundaries-20261002/run-validation.ps1 -Output Exe/<fresh-folder>`. The runner refuses an existing output base,uses absolute LOOM/English Indy roots,excludes the protected untracked LoomObjectLookAuditTests,rebuilds every harness,and performs the full checks above. Analyses reuse inherited assertions without changing historical packets. Run this packet's `update_handoff.py` only after successful aggregation,then `validate_publication.py` to verify report hashes,all historical guidance/checkpoint ancestry,57 protected files and App0. Both scripts use the bundled Python with `-X utf8` and required RTK proxy. The initial `prepare_validation.py` is retained as scaffold history;the saved final runners are authoritative.

Final build command:`rtk proxy dotnet build SCUMMexplorer.slnx -c Release`;artifact:`Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`,rebuilt without launch. Session token usage is unavailable;account limits are not session usage.

Read this packet's validations/summaries/scripts/Core APIs/tests first,then runtime-restoration,runtime-object-correspondence,runtime-correspondence,runtime-calls,runtime-continuity,runtime-dependencies,runtime-access,runtime-relations,runtime-bindings,correlated-core/completion-observer and original certainty-protocol/provenance/correlated-drafts. Continue remaining bounded actual callee/return/yield/resumed-caller correspondence and implicit/string/native access/mutation coverage under every retained barrier.
