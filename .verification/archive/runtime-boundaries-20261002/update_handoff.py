"""Append current verified work; retain every historical guidance block verbatim."""
from pathlib import Path
import hashlib
import json

base = Path(__file__).parent
checks = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
assert checks['core'] == 622 and checks['focused'] == 296
assert checks['primaryEntireLatestStreamsExact'] and checks['separateEntireLoadedObjectCallSelectionCorrespondenceExact']
for report in checks['reports']:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest() == report['sha256'], report['path']

validation = '''# Actual boundary phases and inventory access preservation - 2 October 2026

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

From `D:\\Documents\\Personal\\Code Projects\\SCUMMexplorer`,run required RTK proxy PowerShell with `.verification/archive/runtime-boundaries-20261002/run-validation.ps1 -Output Exe/<fresh-folder>`. The runner refuses an existing output base,uses absolute LOOM/English Indy roots,excludes the protected untracked LoomObjectLookAuditTests,rebuilds every harness,and performs the full checks above. Analyses reuse inherited assertions without changing historical packets. Run this packet's `update_handoff.py` only after successful aggregation,then `validate_publication.py` to verify report hashes,all historical guidance/checkpoint ancestry,57 protected files and App0. Both scripts use the bundled Python with `-X utf8` and required RTK proxy. The initial `prepare_validation.py` is retained as scaffold history;the saved final runners are authoritative.

Final build command:`rtk proxy dotnet build SCUMMexplorer.slnx -c Release`;artifact:`Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`,rebuilt without launch. Session token usage is unavailable;account limits are not session usage.

Read this packet's validations/summaries/scripts/Core APIs/tests first,then runtime-restoration,runtime-object-correspondence,runtime-correspondence,runtime-calls,runtime-continuity,runtime-dependencies,runtime-access,runtime-relations,runtime-bindings,correlated-core/completion-observer and original certainty-protocol/provenance/correlated-drafts. Continue remaining bounded actual callee/return/yield/resumed-caller correspondence and implicit/string/native access/mutation coverage under every retained barrier.
'''
(base/'validation.md').write_text(validation, encoding='utf-8')

short = '''2 October continuation from `f84460f`: `6ad392d`, `3ed2da7` and `6c06692` require actual initialization/host/caller/child phases, including a terminated chaining caller before deferred initialization; `4b7c646` copies implicit ego plus independently unknown owner/inventory membership and notifies executed inventory mutations. Preserves `3f32e5f`, `32f1ecb`, `a3a90ec` and every earlier checkpoint. Nineteen boundary tests reproduce twelve former failures; fifteen owner/inventory/string controls preserve independent provenance and opaque string storage. Entire incoming-dispatch initialization/continuity remains required for caller restoration. No argument-derived origin, successful return, domain/effect transport or completion follows. Fresh Core622/focused296;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;13 exact primary packets/chronologies;primary366 bindings/55 triples/0 caller resumptions/76 original writer terminals/0 new original writer resumed chains;15 unknown message fields and0 camera waits. Separate raw4 objects remain16/14/9/3/195 with one per-call128 rejection,loaded fallback0;separate camera55 waits/220 unknown fields. Numeric report and native challenge3 expressions/17 steps/36 exact frames/11 reads/11 invalidations remain exact. Clean final main Release,all57 protected files exact/unstaged,App0/no launch/no push. Literal coverage168/235=71.49%;broader plan has no fixed denominator. [Validation](../.verification/archive/runtime-boundaries-20261002/validation.md),[checks](../.verification/archive/runtime-boundaries-20261002/check-summary.json). Evidence:`Exe/RuntimeBoundariesFinalSession4-20261002`. Continue remaining actual callee/return/yield/resumed correspondence and implicit/string/native access/mutations under every retained bound and barrier.

'''
anchors = {
    'docs/LOOM_EGA_PLAN.md': '## Current follow-up (2 October)\n\n',
    'docs/LOOM_SCRIPT_MAP.md': '## Follow-up findings (30 September)\n\n',
    'docs/LOOM_CUTSCENE_DISCOVERY.md': '## Current replay coverage (2 October)\n\n',
    'docs/CHANGELOG.md': '# Changelog\n\n'
}
for name, anchor in anchors.items():
    path = Path(name)
    text = path.read_text(encoding='utf-8')
    assert text.count(anchor) == 1 and short not in text
    path.write_text(text.replace(anchor, anchor+short, 1), encoding='utf-8')

prompt = '''Continue SCUMMexplorer from the latest runtime-boundaries verification/handoff after `6ad392d`, `4b7c646`, `3ed2da7` and `6c06692`, preserving `f84460f`, `3f32e5f`, `32f1ecb`, `a3a90ec` and every earlier checkpoint/constraint in docs/NEXT_SESSION_PROMPT.md.

Read the handoff/referenced guidance and runtime-boundaries-20261002 validations/summaries/scripts/Core APIs/tests first, then runtime-restoration, runtime-object-correspondence, runtime-correspondence, runtime-calls, runtime-continuity, runtime-dependencies, runtime-access, runtime-relations, runtime-bindings, correlated-core/completion-observer and original certainty-protocol/provenance/correlated-drafts in order.

Prioritize remaining bounded actual callee binding and return/yield/resumed-caller correspondence, including scheduler, cancellation, replacement, native and omitted boundaries. Ordinary initialization must precede host return; deferred chains require the observed host and terminated chaining caller. Opaque caller-end effects remain independent of actual deferred child assignment. Caller restoration requires the entire incoming dispatch from observed initialization and continuity before the outgoing call. Preserve unobserved incoming-dispatch barriers. Keep host return, child end/yield/restoration, caller continuation/end, restored locals and writer terminals independent. Argument equality supplies no local origin or successful return; structural triples establish no completion.

Continue implicit/string/native accesses and explicit mutation coverage. Owner/inventory copies retain unknown provenance and the original effect barrier; implicit ego has its own estimate. Raw inventory edits need notification; identical/no-op notifications prove no changed value or loaded lifetime. All five string forms retain opaque contents/length/lifetime. Preserve every estimate, bound, barrier, historical origin and writer/alias/external/concurrent/cancellation/replacement/scheduler/callee limit. Keep nested chronology, native lifetimes, raw same-value notifications and fully observed writer resumed-terminal chains independent. No domain/effect transport, completion promotion, estimate clearing, injected prerequisites, room exceptions or shortcuts.

Primary remains366 bindings,55 triples,0 caller resumptions,76 original writer terminals,0 new original writer resumed chains;15 message fields retain unknown provenance;camera waits0. Separate unchanged raw4 object capture remains16 bindings,14 independent child terminals,9 triples,3 caller resumptions,195 child restorations and1 per-call128 rejection;preserve separate chronology IDs/estimates;loaded fallback0. Separate camera-only raw4 remains55 waits/220 copied fields,unknown provenance,no loaded lifetime or completion claim.

Baseline:Core622;focused296;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;13 exact primary packets/chronologies;numeric unchanged;native challenge3 expressions/17 steps/36 exact frames;11 field reads/11 invalidations;clean main Release. Evidence:Exe/RuntimeBoundariesFinalSession4-20261002. Primary3036/4552->4553->4556->4557,practice201->030->031,early0019/D671,D6C projection,native opening/090 and every route/UI/palette limit remain.

Keep all57 protected files untouched/unstaged. Run fresh appropriate regressions,rebuild main Release,commit tasks separately and update handoff. No App running/launched;no push. Finish with a ready-to-copy continuation prompt and percentage with its denominator. Literal-site coverage168/235=71.49%;broader plan has no fixed denominator. Session token usage unavailable;account limits are not session usage.
'''

latest = '''* Continuation from `f84460f`: `6ad392d`, `3ed2da7` and `6c06692` add observed initialization/host/caller/child phase guards; `4b7c646` adds implicit ego/owner/inventory access and explicit inventory notifications. Preserves `3f32e5f`, `32f1ecb`, `a3a90ec` and every earlier checkpoint/constraint below.
* Ordinary initialization requires aligned begin0 before host return. Deferred chain initialization requires its actual host return and terminated chaining caller. Host site/instruction/path must retain the dispatch; begin records cannot masquerade as caller continuation/end. Nineteen new cases reproduce twelve former false correspondences and retain seven controls. The complete incoming-dispatch initialization/continuity requirement remains unchanged; every native/omitted/cancellation/replacement/scheduler/bound barrier remains. [Phases](../.verification/archive/runtime-boundaries-20261002/order-validation.md).
* Exact setOwnerOf retains explicit target/source and appends implicit var001 ego. Owner and membership are independent copies with unknown field provenance,even when they disagree;filters preserve disclosure barriers. Executed inventory additions/removals notify the selected family including repeated additions/absent removals. Existing owner notification remains conservative. Raw/unattached/disposed limits remain;all five stringOps forms retain opaque contents/length/lifetime. Fifteen new tests include exact execution/legacy transparency and synthetic notification controls only;no loaded inventory lifetime is claimed. [Inventory](../.verification/archive/runtime-boundaries-20261002/inventory-validation.md).
* Fresh Core622/focused296;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;all13 primary whole packets and complete latest instruction/access/binding/relation/cancellation/expression/call/selection/correspondence/completion streams exact. Primary366 bindings/55 triples/0 caller resumptions/76 original writer terminals/0 new original writer resumed chains;15 unknown message fields;camera waits0. Numeric162/52/51/14/four/12 unchanged;native challenge3 expressions/17 steps/36 exact frames/11 reads/11 invalidations;clean final main Release. Evidence:`Exe/RuntimeBoundariesFinalSession4-20261002`. [Validation](../.verification/archive/runtime-boundaries-20261002/validation.md),[checks](../.verification/archive/runtime-boundaries-20261002/check-summary.json).
* Separate unchanged raw4 object recipe/whole packet/call/selection/correspondence streams remain exact:16 bindings,14 independent child terminals,9 triples,3 caller resumptions,195 child restorations,1 per-call128 rejection;loaded fallback0. Preserve its separate chronology IDs and estimates. Separate camera-only raw4 remains55 waits/220 fields,unknown provenance,no loaded lifetime or completion claim. Neither capture adds an original writer resumed-terminal chain.
* All57 protected files hash-identical/unstaged;App0/no launch/no push. Initial order-before retains ten failures/five controls;inventory-1 retains its corrected nullable test warning. The first full runner passed Core618/focused292/native challenge then stopped on an unquoted generated path;its evidence is retained separately and no aggregate success was claimed. The second runner was stopped before aggregation for the child-phase correction;child-phase-before retains two further failures/one control and child-phase-final passes80. The third runner caught shifted separate raw4 Event IDs caused by an overconstrained chain guard. The corrected guard keeps opaque caller-end effects independent of actual child assignments;independent-chain-final passes81 and loaded-fixed restores every original Event ID without renumbering. The fourth fresh final runner supplies current evidence. All route/UI/palette constraints and prior packets remain untouched. Session token usage unavailable;account limits are not session usage.
* Read runtime-boundaries first,then runtime-restoration and all packets below in their established order. Continue remaining bounded actual callee/return/yield/resumed correspondence and implicit/string/native accesses/mutations. Keep every estimate,bound,barrier,historical origin and writer/alias/external/concurrent/cancellation/replacement/scheduler/callee limit. Keep nested chronology,native lifetimes,raw same-value notifications and fully observed writer resumed-terminal chains independent. No domain/effect transport,completion promotion,estimate clearing,injected prerequisites,room exceptions or shortcuts. Literal-site coverage168/235=71.49%,334 starts,50 outstanding owners,123 unresolved edges;broader plan has no fixed denominator.

Earlier checkpoints remain historical. Only their narrower selected boundary-phase and implicit owner/inventory descriptions are superseded;every general origin/return/domain/effect/completion,raw/native/external/concurrent/scheduler/cancellation/replacement,route/UI and opening/090 constraint remains current.

'''
path = Path('docs/NEXT_SESSION_PROMPT.md')
text = path.read_text(encoding='utf-8')
anchor = 'State at checkpoint (2 October 2026):\n\n'
marker = 'Next-session request (ready to copy):\n\n'
assert text.count(anchor) == text.count(marker) == 1 and latest not in text
head = text.split(marker)[0].replace(anchor, anchor+latest, 1)
path.write_text(head+marker+prompt, encoding='utf-8')
print('Verified latest guidance appended; historical text retained; continuation prompt updated.')
