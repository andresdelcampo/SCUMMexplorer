"""Publish only verified preservation; retain all earlier guidance verbatim."""
from pathlib import Path
import hashlib
import json

base = Path(__file__).parent
checks = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
assert checks['core'] == 654 and checks['focused'] == 328
assert checks['newPhasePayloadCases'] == 20 and checks['newNativeObservationGapCases'] == 12
assert checks['primaryEntireLatestStreamsExact'] and checks['separateEntireLoadedObjectCallSelectionCorrespondenceExact']
for report in checks['reports']:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest() == report['sha256'], report['path']

validation = '''# Call phase payloads and native observation continuity - 2 October 2026

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

From `D:\\Documents\\Personal\\Code Projects\\SCUMMexplorer`, use required RTK proxy PowerShell with `.verification/archive/runtime-phase-payloads-20261002/run-validation.ps1 -Output Exe/<fresh-folder>`. Runners require fresh output, absolute LOOM/English Indy roots, exclusion of the protected untracked audit test and rebuilt harnesses. Inherited analyses write only this packet's new summaries. The finalizer verifies complete reports/current-source manifests before handoff publication; `validate_publication.py` also verifies frozen files, original documentation and checkpoint ancestry.

`Exe/RuntimePhasePayloadSession-20261002/phase-before` retains15 failures/5 controls;phase-final passes101. native-before retains5 failures/7 controls;native-final passes130. The first full output `Exe/RuntimePhasePayloadFinalSession-20261002` passed Core654/focused328 and both native challenges, then stopped at an unquoted generated absolute Python path containing spaces. No aggregate success was claimed. The generator now uses the intended repository-relative paths; the second fresh runner is authoritative. All historical failed/partial packets remain dated and untouched.

Final command: `rtk proxy dotnet build SCUMMexplorer.slnx -c Release`. Artifact: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`, rebuilt after harnesses without launch. No new WPF/physical-input/listening claim. Session token usage unavailable;account limits are not session usage.

Read this packet's validations/summaries/scripts/Core APIs/tests first, then runtime-boundaries,runtime-restoration and every earlier packet in the handoff's established order. Continue remaining bounded actual callee/return/yield/resumed-caller correspondence and implicit/string/native access/mutation coverage; broader completion/effects remain unresolved.
'''
(base/'validation.md').write_text(validation,encoding='utf-8')

short = '''2 October continuation from `f649ff1`: `02c9fc1` rejects stop/resume result payloads on actual dispatch/restoration/continuation instructions; `b65cb4e` retires previous native lifetimes across omitted observation sequences. Twenty call-phase cases reproduce15 false correspondences;12 native cases reproduce5 stale-lifetime failures. Complete incoming-dispatch initialization/continuity and every boundary/barrier remain;opaque caller-end effects stay independent of actual child assignment. String contents/length/lifetime and exhaustive raw/native effects remain unresolved. No equality-derived origin,successful return,domain/effect transport or completion promotion. Fresh Core654/focused328;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;13 exact primary packets/chronologies;primary366/55/0 and76 original writer terminals/0 new original writer resumed chains;15 unknown message fields/camera waits0. Separate raw4 object16/14/9/3/195/rejection1,fallback0 and chronology IDs/estimates exact;separate camera55 waits/220 unknown fields with no lifetime/completion claim. Numeric report and native challenge3 expressions/17 steps/36 exact frames/11 reads/11 invalidations unchanged. Clean final main Release;all57 protected files exact/unstaged;App0/no launch/no push. Literal-site coverage168/235=71.49%;broader plan has no fixed denominator. Evidence:`Exe/RuntimePhasePayloadFinalSession2-20261002`. [Validation](../.verification/archive/runtime-phase-payloads-20261002/validation.md),[checks](../.verification/archive/runtime-phase-payloads-20261002/check-summary.json). Continue remaining bounded callee/caller correspondence and implicit/string/native access/mutations under every retained constraint.

'''
anchors = {
    'docs/LOOM_EGA_PLAN.md':'## Current follow-up (2 October)\n\n',
    'docs/LOOM_SCRIPT_MAP.md':'## Follow-up findings (30 September)\n\n',
    'docs/LOOM_CUTSCENE_DISCOVERY.md':'## Current replay coverage (2 October)\n\n',
    'docs/CHANGELOG.md':'# Changelog\n\n'
}
for name, anchor in anchors.items():
    path = Path(name)
    text = path.read_text(encoding='utf-8')
    assert short not in text and text.count(anchor) == 1
    path.write_text(text.replace(anchor,anchor+short,1),encoding='utf-8')

path = Path('docs/NEXT_SESSION_PROMPT.md')
text = path.read_text(encoding='utf-8')
anchor = 'State at checkpoint (2 October 2026):\n\n'
assert text.count(anchor) == 1 and 'runtime-phase-payloads-20261002' not in text
current = '''* Continuation from `f649ff1`: `02c9fc1` rejects end-result payloads on call begin/instruction phases; `b65cb4e` retires native read lifetimes across missing observation sequences. Preserves `f84460f`,`6ad392d`,`4b7c646`,`3ed2da7`,`6c06692` and every earlier checkpoint/constraint. Twenty phase cases reproduce15 false correspondences/five controls;12 native cases reproduce5 stale lifetimes/seven controls. [Phases](../.verification/archive/runtime-phase-payloads-20261002/phase-validation.md),[native](../.verification/archive/runtime-phase-payloads-20261002/native-validation.md).
* Complete actual incoming-dispatch initialization/continuity remains mandatory. Dispatch/restored-local/continuation begin/instruction records cannot carry stop/resume results,even when both copies agree. Native gaps retire previous lifetime records before processing new observations;new complete reads are independent and cannot revive old history from equal values. Every estimate,bound and barrier remains. Opaque caller-end effects remain independent of actual child assignment. No origin,successful return or completion from argument equality/structural triples;no domain/effect transport.
* Fresh Core654/focused328;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;all13 primary packets and complete latest streams exact. Primary366 bindings/55 triples/0 caller resumptions/76 original writer terminals/0 new original writer resumed chains;15 unknown message fields;camera waits0. Numeric unchanged;native challenge3 expressions/17 steps/36 exact frames/11 reads/11 invalidations;clean final main Release. Evidence:`Exe/RuntimePhasePayloadFinalSession2-20261002`. [Validation](../.verification/archive/runtime-phase-payloads-20261002/validation.md),[checks](../.verification/archive/runtime-phase-payloads-20261002/check-summary.json).
* Separate unchanged raw4 objects remain16 bindings/14 independent child terminals/9 triples/3 caller resumptions/195 restorations/1 per-call128 rejection,loaded fallback0;every chronology ID/estimate remains exact. Separate camera remains55 waits/220 unknown fields,no loaded lifetime or completion claim. Native/string/raw/implicit effects and every writer/alias/external/concurrent/cancellation/replacement/scheduler/callee limit remain independent. All57 protected files exact/unstaged;all route/UI constraints retained;App0/no launch/no push.
* First full runner passed Core654/focused328/native challenges then stopped on an unquoted generated Python path;partial evidence retained,no aggregate claim. Relative path generation is corrected;the second fresh runner is authoritative. Read runtime-phase-payloads first,then runtime-boundaries,runtime-restoration and all earlier packets in their established order. Continue bounded actual callee/return/yield/resumed-caller correspondence and implicit/string/native access/mutations. Literal-site coverage168/235=71.49%;334 starts,50 outstanding owners,123 unresolved edges;broader plan has no fixed denominator. Session token usage unavailable;account limits are not session usage.

Earlier checkpoints remain historical. Only their narrower call phase-payload and native gap descriptions are superseded;all origin/return/domain/effect/completion,raw/native/external/concurrent/scheduler/cancellation/replacement,route/UI and opening/090 constraints remain current.

'''
text = text.replace(anchor,anchor+current,1)
marker = 'Next-session request (ready to copy):\n\n'
head, prompt = text.split(marker)
prompt = prompt.replace('Continue SCUMMexplorer from the latest runtime-boundaries verification/handoff after `6ad392d`, `4b7c646`, `3ed2da7` and `6c06692`, preserving `f84460f`, `3f32e5f`, `32f1ecb`, `a3a90ec`',
    'Continue SCUMMexplorer from the latest runtime-phase-payloads verification/handoff after `02c9fc1` and `b65cb4e`, preserving `f649ff1`, `f84460f`, `6ad392d`, `4b7c646`, `3ed2da7`, `6c06692`, `3f32e5f`, `32f1ecb`, `a3a90ec`')
prompt = prompt.replace('and runtime-boundaries-20261002 validations/summaries/scripts/Core APIs/tests first, then runtime-restoration',
    'and runtime-phase-payloads-20261002 validations/summaries/scripts/Core APIs/tests first, then runtime-boundaries, runtime-restoration')
prompt = prompt.replace('Prioritize remaining bounded',
    'Preserve the begin/instruction no-end-result guards and native sequence-gap retirement. Equal later values cannot bridge an omitted native mutation; fresh copied reads remain independent.\n\nPrioritize remaining bounded',1)
prompt = prompt.replace('Core622;focused296','Core654;focused328').replace('Exe/RuntimeBoundariesFinalSession4-20261002','Exe/RuntimePhasePayloadFinalSession2-20261002')
path.write_text(head+marker+prompt,encoding='utf-8')
print('Verified handoff and four guidance documents updated; all earlier text retained.')
