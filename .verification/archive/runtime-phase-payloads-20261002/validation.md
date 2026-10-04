# Call phase payloads and native observation continuity - 2 October 2026

Continues `f649ff1`, preserving `f84460f`, `6ad392d`, `4b7c646`, `3ed2da7`, `6c06692` and every earlier checkpoint and constraint. Separate implementation commits: `02c9fc1` rejects end-result payloads on call begin/instruction phases; `b65cb4e` retires native lifetimes across omitted observation sequences.

## Changes and limits

[Call phases](phase-validation.md): twenty cases replay copied actual executor chronology. Fifteen defects previously admitted a binding, structural triple, child terminal or caller resumption when dispatch/restoration/continuation instructions carried end-result fields. Five valid controls remain. Begin/instruction records now require absent stop/resume results. Every incoming-dispatch initialization/continuity requirement, ordinary/deferred initialization order and host/child/caller alignment remains. Opaque caller-end effects remain independent of actual deferred child assignment. The101 call-focused cases pass.

[Native observations](native-validation.md): twelve cases reproduce five stale-lifetime failures and retain seven controls. A sequence gap retires previous native reads before processing any new instruction/begin/end/cancellation record. A new complete read may establish its own selected observation; equal storage cannot bridge the missing interval or revive its predecessor. Historical copies/estimates, existing event/sequence IDs and bounds remain. Arbitrary first sequence and repeated-sequence rejection remain. Explicit opaque string/owner notifications retain conservative scoped retirement with no changed-value claim. The130 native/access/implicit/string/mutation cases pass. All five string forms retain opaque contents/length/lifetime. No new loaded native field/lifetime or exhaustive effect claim.

These are observer-only guards. Actual assignments, host return, child end/yield/restoration, caller continuation/end/restored locals and original writer terminals stay independent. No argument-equality origin, successful return, numeric domain/effect transport, completion promotion, estimate clearing, injected prerequisite, room exception or shortcut.

## Fresh preservation

Authoritative output: `Exe/RuntimePhasePayloadFinalSession2-20261002`. [Checks and report hashes](check-summary.json), [primary](preservation-summary.json), [separate objects](loaded-object-summary.json), [camera](camera-summary.json), [native challenge](native-field-summary.json), [native chronology](native-continuity-summary.json). All13 primary native read/mutation/invalidation streams are also byte-exact to `f649ff1`, including every Event/Read ID; no loaded lifetime is inferred.

| Check | Result |
| --- | --- |
| Core / focused | 654 /328 pass;32 new cases |
| Primary packets and chronology | All13 whole packets and complete instruction/access/binding/relation/cancellation/expression/call/selection/correspondence/completion streams exact to `f649ff1`;all inherited/expanded assertions retained |
| Primary structural evidence | 366 bindings,55 triples,0 caller resumptions;76 original writer terminals,0 new original writer resumed chains |
| Primary native evidence | 15 unknown message fields;0 camera waits |
| Separate unchanged raw4 objects | 16 bindings,14 independent child terminals,9 triples,3 caller resumptions,195 restorations,1 per-call128 rejection;loaded fallback0;all call/selection/correspondence records and IDs exact |
| Separate camera-only raw4 | 55 waits/220 copied fields;exact chronology and packet;unknown provenance,no loaded lifetime or completion claim |
| Source roots | 363 exact roots/fingerprints/four memberships/both sequences;Blockers0 |
| Core rendering | 76 unchanged renders,0 gaps,0 changed pixels |
| Numeric source report | Entire162/52/51/14/four/12 report exact |
| Native challenge | 3 expressions/17 steps/36 exact frames;11 reads/11 explicit invalidations |
| Final main Release | 0 warnings,0 errors;App0,no launch,no push |
| Protected work | All57 original hashes exact and unstaged |
| Literal-site coverage | 168/235=71.49%;334 starts,50 outstanding owners,123 unresolved edges;broader plan has no fixed denominator |

Primary3036/4552->4553->4556->4557,practice201->030->031 and local201 continuation remain separate. Separate raw4 Script007 +002E->0034 retains slots291/349/435 and incoming dispatch213/269/347, all chronology IDs and estimates. No renumbering or relaxed comparison. No new original writer resumed-terminal chain or route completion. Native opening/090,early0019/D671,D6C projection and every route/UI/palette constraint remain. All original bounds, omitted/native/external/alias/concurrent/cancellation/replacement/scheduler/callee barriers and historical origins remain.

## Reproduction and diagnostics

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`, use required RTK proxy PowerShell with `.verification/archive/runtime-phase-payloads-20261002/run-validation.ps1 -Output Exe/<fresh-folder>`. Runners require fresh output, absolute LOOM/English Indy roots, exclusion of the protected untracked audit test and rebuilt harnesses. Inherited analyses write only this packet's new summaries. The finalizer verifies complete reports/current-source manifests before handoff publication; `validate_publication.py` also verifies frozen files, original documentation and checkpoint ancestry.

`Exe/RuntimePhasePayloadSession-20261002/phase-before` retains15 failures/5 controls;phase-final passes101. native-before retains5 failures/7 controls;native-final passes130. The first full output `Exe/RuntimePhasePayloadFinalSession-20261002` passed Core654/focused328 and both native challenges, then stopped at an unquoted generated absolute Python path containing spaces. No aggregate success was claimed. The generator now uses the intended repository-relative paths; the second fresh runner is authoritative. All historical failed/partial packets remain dated and untouched.

Final command: `rtk proxy dotnet build SCUMMexplorer.slnx -c Release`. Artifact: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`, rebuilt after harnesses without launch. No new WPF/physical-input/listening claim. Session token usage unavailable;account limits are not session usage.

Read this packet's validations/summaries/scripts/Core APIs/tests first, then runtime-boundaries,runtime-restoration and every earlier packet in the handoff's established order. Continue remaining bounded actual callee/return/yield/resumed-caller correspondence and implicit/string/native access/mutation coverage; broader completion/effects remain unresolved.
