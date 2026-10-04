"""Publish only completed, hash-checked preservation facts; retain all earlier checkpoints."""
from pathlib import Path
import hashlib
import json

base = Path(__file__).parent
checks = json.loads((base / 'check-summary.json').read_text(encoding='utf-8'))
assert checks['core'] == 501 and checks['focused'] == 175
assert checks['fullRecipePacketsUnchanged'] == 13 and checks['rootCount'] == 363
assert checks['changedPixels'] == 0 and checks['remainingGaps'] == 0
assert checks['releaseWarnings'] == checks['releaseErrors'] == checks['blockedRandomSites'] == 0
for report in checks['reports'] + [checks['sourceManifest']]:
    assert hashlib.sha256(Path(report['path']).read_bytes()).hexdigest() == report['sha256'], report['path']
manifest = json.loads(Path(checks['sourceManifest']['path']).read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p, h in manifest.items())

status = """2 October continuation from `b1381ff`: `5fcc2a0` records actual caller dispatch, executor local initialization, child end/yield, host return, caller continuation and restored locals in an independent bounded stream; `995e97e` copies remaining loadRoomWithEgo/string/owner/wait dependencies with all native/effect barriers; `b3ee09e` notifies executed object/actor mutation lifetimes, including identical writes and attempted no-op operations. Actual supplied/default/restored local assignments establish no numeric origin; host return establishes no successful callee return. The raw4 practice path separately records local201 +0028 -> actual030 slot378/parent373/run11824, estimated local000=1 -> chained031 slot382/parent378/run11828; local201 +002E continuation is independent. All13 packets/latest/prior/expanded legacy chronologies, completion records and76 original own-child matches/own-invocation terminals remain exact; no writer resumed chain is newly claimed for these76. Primary3036/4552->4553->4556->4557, every selected boot/input/index/practice value, historical origin and estimate remain. Fresh Core501/focused175;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;source-only numeric report unchanged;native challenge3 expressions/17 steps/every input-frame transparent;clean final main Release;App0/no launch/no push/promotion. Next: bounded actual callee/return/yield/resumed-caller correspondence under omitted/filtered/native/scheduler/cancellation/replacement boundaries, remaining implicit/native accesses and explicit mutation coverage. No general domain/effect transport, completion promotion, estimate clearing, injected prerequisites, room exceptions or shortcuts; native opening/090 and all route/palette/UI/unrelated-work constraints remain. [Validation](../.verification/archive/runtime-calls-20261002/validation.md)."""

top = """* Continuation from `b1381ff`: actual callee/caller observations are separately committed as `5fcc2a0`; remaining copied room/string/owner/wait access as `995e97e`; explicit executed native mutation notification as `b3ee09e`. Preserves `ec9a525`, `75d9cd7`, `9593435`, `17450ff`, `6442e4e`, `133b4d4` and every earlier checkpoint/constraint below.
* Optional `receiveCall` adds independent `LoomCallObservation` records/IDs without consuming legacy sequences. The executor copies its actual26 supplied/default-zero local assignments and actual restored locals on resume; the session retains the dispatch on the actual child frame, including deferred chain initialization. Caller argument equality gives no callee-local numeric origin. Child end/yield, host dispatch return, surviving caller's next instruction, stopped/frozen caller end and resumed locals remain independent. Host return can follow missing, partial, yielded or chained children and proves no successful callee return. Filters redact omitted boundaries/locals with barriers. Call4096 boundaries per invocation plus one bound barrier do not stop execution; finished scopes release accounting. Existing expression4096/subcommand128/index32/native4096/writer4096/boundary128 remain.
* Exact decoded loadRoomWithEgo access copies object/room, implicit var001 ego and signed literal coordinates with original operand spelling. Malformed/out-of-range/variable-looking coordinates remain barriers; native placement/room/walk/camera effects stay incomplete. String-storage and message/camera timing dependencies remain opaque. Selected pre-instruction getObjectOwner and actor wait moving fields are copied independently; owner estimate/inventory/effect lifetime remains unresolved. No callback is invoked or inline text scanned; no copied native field becomes a numeric origin.
* Object state/class/owner Set/Remove APIs notify scoped/session sinks for identical writes and absent removals. Executor setState/setClass/setOwnerOf/actor init/drawObject peer/target writes and catalog-source class writes use them. Actor-table placement/room/replacement/init/forget/estimate/ignore-box/elevation/speed/frame/talk-color/animation/direction/face/walk/hide/show/step operations notify their conservative actor lifetime; attempted no-ops retire correspondence without asserting a changed value. Palette/palette-only estimates remain a separate actor-palette lifetime. Raw dictionaries still need explicit same-value notification; unknown owner-domain invalidation remains conservative. Native/raw/complex/callback/effect coverage is not exhaustive; no estimate, alias or numeric relation is cleared or promoted.
* Fresh raw4 practice evidence independently observes Room06 local201 +0028 supplying estimated1 to actual030 local000 (slot378,parent373,run11824), then030 +0012 chaining to actual031 (slot382,parent378,run11828,estimated local000=1). Actual local201 +002E in run11819 is its own next-instruction observation, not inferred successful child return. All13 whole packets, latest/prior/expanded legacy instruction/access/relation/binding/cancellation chronologies and completion records remain exact except the separately permitted additional optional copied dependencies. All76 original zero waits and separate own-invocation/parent terminals remain; no resumed-terminal chain is newly claimed for these76. Primary031 child412/run12143 +031D/4552 -> confirmation4553 ->020 parent408/run12144 +0028/4556 -> estimated increment4557 remains frame3036. Early0019/D671,D6C projection,native opening/090 and all historical limits remain.
* Fresh Core501 (41 new cases:13 call,12 remaining access,16 native mutation);focused175;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;13 exact packets/legacy chronologies/completion records;76 separate original writer terminals;source-only numeric162/52/51/14/four/12 report unchanged;native challenge3 expressions/17 actual steps/36 frames with every input/frame transparent;clean final main Release zero warnings/errors;App0/no launch/no push. Evidence: `Exe/RuntimeCallsFinalSession-20261002`. [Calls](../.verification/archive/runtime-calls-20261002/call-validation.md), [access](../.verification/archive/runtime-calls-20261002/access-validation.md), [native](../.verification/archive/runtime-calls-20261002/native-validation.md), [validation](../.verification/archive/runtime-calls-20261002/validation.md), [calls](../.verification/archive/runtime-calls-20261002/call-summary.json), [checks](../.verification/archive/runtime-calls-20261002/check-summary.json). Initial fixture/build diagnostics are retained under `Exe/RuntimeCallsSession-20261002`. Protected verb-area comment,two untracked audit directories and untracked test are untouched/unstaged. Session token usage unavailable;account limits are not substituted.
* Covered percentage:168/235 validated literal cutscene sites =71.49% (71.5% rounded), unchanged. Coverage remains334 starts,50 outstanding owners,123 unresolved edges. This is the measurable replay-site denominator, not a percentage of the broader product/research plan, whose tasks have no fixed denominator. Report covered, not remaining.
* Read runtime-calls-20261002 validations/summaries/scripts/Core APIs/tests first, then runtime-continuity, runtime-dependencies, runtime-access, runtime-relations, runtime-bindings, correlated-core/completion-observer and original certainty-protocol/provenance/correlated-drafts in order. Prioritize bounded actual callee initialization/binding and return/yield/resumed-caller correspondence, keeping host return, actual callee end and actual caller continuation independent; continue remaining implicit/string/native accesses and explicit native mutation coverage. Preserve every estimate, bound, barrier, historical origin and writer/alias/external/concurrent/cancellation/replacement/scheduler/callee limit. Keep nested chronology, native lifetimes, raw same-value notifications and fully observed writer resumed-terminal chains separate. Argument equality gives no callee-local origin or return. No domain/effect transport, completion promotion, estimate clearing, injected prerequisites, room exceptions or shortcuts.

Earlier checkpoints remain historical. Their absence of actual initialization/caller chronology and narrower notified mutation coverage is superseded only by these optional copied observations/invalidations. Their general successful callee return, scheduler/cancellation, omitted/external/native/raw effects, domain/completion and opening/090 limits remain current.

"""

prompt = """Continue SCUMMexplorer from the latest verification/handoff following `5fcc2a0` (actual callee/caller observations), `995e97e` (remaining room/string/owner/wait access) and `b3ee09e` (explicit native mutation notification), preserving `b1381ff`, `ec9a525`, `75d9cd7`, `9593435`, `17450ff`, `6442e4e`, `133b4d4` and every earlier checkpoint and constraint in docs/NEXT_SESSION_PROMPT.md.

Read that handoff and all referenced guidance. Read runtime-calls-20261002 validations/summaries/scripts/Core APIs/tests first, then runtime-continuity, runtime-dependencies, runtime-access, runtime-relations, runtime-bindings, correlated-core/completion-observer and original certainty-protocol/provenance/correlated-drafts in order.

Prioritize bounded actual callee initialization/binding and return/yield/resumed-caller correspondence under omitted/filtered/native/scheduler/cancellation/replacement boundaries. Host return, child end/yield, actual caller continuation and restored locals remain independent; argument equality gives no callee-local numeric origin or successful return. Continue remaining implicit/string/native access and explicit native mutation coverage. Preserve all estimates, barriers, bounds, historical origins and writer/alias/external/concurrent/cancellation/replacement/scheduler/callee limits. Keep nested chronology, native lifetimes, raw same-value notifications and fully observed writer resumed-terminal chains separate. Preserve expression4096/subcommand128/index32/native4096/writer4096/boundary128/call4096. No domain/effect transport, completion promotion, estimate clearing, injected prerequisites, room exceptions or shortcuts.

Baseline:Core501;focused175;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;13 exact packets/legacy chronologies/completion records;76 separate own-invocation writer/terminal observations;numeric unchanged;3 native challenge expressions/17 actual steps/36 frames with every input/frame transparent;clean main Release. Evidence:Exe/RuntimeCallsFinalSession-20261002. Actual practice201->030->031 initialization and local201 continuation remain separate from primary3036/4552->4553->4556->4557,early0019/D671,D6C projection and native opening/090 limits. Selected boot/input/index/practice values and source-only162/52/51/14/four/12 numeric report stay independent.

Preserve raw4=41,15/14;raw6=41,12/11;separate direct633/actual204 Escape=43,15/15;both level4 orders,096/all four20 routes,palette/placement separation,all four known dye notes/count and every route/UI constraint. Keep protected verb-area comment,two untracked audit directories and untracked test untouched/unstaged. Run fresh appropriate regressions,rebuild main Release,commit tasks separately,update handoff. No App running/launched;no push. Session token usage unavailable;account limits are not session usage. Finish with a ready-to-copy continuation prompt and an objective percentage covered (not left), with its denominator. Current measured replay coverage:168/235 literal sites=71.49%,unchanged;the broader plan has no fixed percentage denominator.
"""

validation = """# Actual callee/native preservation — 2 October 2026

Continues `b1381ff` with three separate tasks: `5fcc2a0` actual callee/caller observation, `995e97e` remaining copied room/string/owner/wait access, and `b3ee09e` explicit native mutation notification. All earlier checkpoints and handoff constraints remain.

[Call API](call-validation.md), [access](access-validation.md) and [native mutation](native-validation.md) describe the separate changes and limits. Final fresh evidence is `Exe/RuntimeCallsFinalSession-20261002`; initial focused/full-source diagnostics remain under `Exe/RuntimeCallsSession-20261002`. No game data, full packets, images or binaries are tracked.

| Fresh check | Result |
| --- | --- |
| Core / focused | 501 /175 passed;41 new cases (13 call,12 access,16 native mutation) |
| Whole packets / legacy chronology / completion | All13 exact against runtime-continuity; earlier binding/relation/access/dependency/continuity comparisons also pass |
| Original own-child literal zero waits / own-invocation terminals | 76 /76 unchanged;0 new fully observed resumed chains for these recipes |
| Literal roots / source fingerprints | 363 exact,10 compared result fields;330 validated previews,19 no-start,4 incomplete,10 brief scopes |
| Established membership / sequences | Four memberships and both whole sequence results exact |
| Blockers | 0 instruction sites |
| Room rendering | 76 rooms,0 gaps,0 changed pixels |
| Numeric source report | Entire162-world/52-flag/51-comparison/14-overlap/four-fabricated-match/12-practice report exact |
| Native challenge | Entire prior passive JSON exact;3 source expressions,17 steps,36 frames,every input/frame transparent |
| Final main Release / process | 0 warnings,0 errors;SCUMMexplorer processes0;App not launched |

Optional call records consume no legacy sequence. New selected dependencies are permitted only for formerly empty loadRoomWithEgo/getObjectOwner bindings and formerly empty native dependency lists on loadRoomWithEgo/getObjectOwner/stringOps/waitFor; all original fields, estimates, barriers, limits and old dependencies remain exact. Other legacy streams and whole packets compare in full. New call/native notification records are separate. Source manifests for the observer, expanded observer and native challenge match current production sources; report hashes are tracked in [checks](check-summary.json). Per-route call/native record hashes and actual selected chronology are in [calls](call-summary.json). The other copied summary files retain inherited analyses of the new packet without changing any earlier packet.

Raw4 independently records Room06 local201 +0028 supplying estimated note1 to actual030 initialization (slot378,parent373,run11824,local000=1). Its chain +0012 creates actual031 (slot382,parent378,run11828,local000 estimated1); local201's actual +002E in run11819 is an independent continuation. Host dispatch return is not callee success. The primary031 child412/run12143 +031D/4552 ->confirmation4553 ->020 parent408/run12144 +0028/4556 ->estimated increment4557 remains frame3036,with a separate original own-invocation terminal. Arguments/local assignment numbers provide no callee-local numeric origin or domain/effect transport.

All native field/value/index/path estimates and historic origins remain. Filtered/omitted/missing/partial boundaries, aliased/external/concurrent writers, raw identical writes, cancellation/replacement and scheduler/callee/effect limits remain independent. Notifications conservatively invalidate correspondence and do not establish write origins or changed values. Raw dictionaries require explicit notifications; owner/inventory/complex/callback/native effect coverage is not exhaustive. Palette uncertainty/notification remains separate from placement. Actual child end/yield, host return, caller continuation, restored locals and fully observed writer resumed-terminal chains do not promote completion.

Raw4=Room41,15/14;raw6=Room41,12/11;separate direct633/actual204 Escape=Room43,15/15;both level4 orders;096/all four20 routes;all four known dye notes/count;every route/UI/status/costume/scythe constraint remains in the exact inherited packets and root/render results. Early0019/D671 and D6C projection remain limited;native opening/090 stays unresolved. No estimates cleared,prerequisites injected,room exception,shortcut,promotion,App launch or push. Protected verb-area comment,two untracked audit directories and untracked test remain untouched/unstaged. Session token usage unavailable;account limits are not session usage.

Measured covered percentage is168/235 validated literal cutscene sites=71.49% (71.5% rounded),unchanged. Coverage remains334 starts,50 outstanding owners,123 unresolved edges. The broader product/research plan has no fixed task denominator,so this percentage describes replay sites only.

Reproduce from the main checkout with required RTK:

```powershell
& 'C:\\Users\\adelc\\AppData\\Local\\Microsoft\\WinGet\\Links\\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/runtime-calls-20261002/run-validation.ps1 -Output Exe/RuntimeCallsRecheck-20261002
```

Use a fresh output base. The runner sets absolute LOOM/Indy English roots, excludes the unrelated untracked test, rebuilds inherited harnesses, runs full/focused tests, native challenge and13 recipes, all-root/membership/sequence/Blockers/render checks, and final main Release/process checks. `analyze_calls.py` compares all inherited records and checks actual selected initialization/continuation without equality-derived origin claims; `finalize_checks.py` requires completed reports/current sources before writing facts. `update_handoff.py` additionally verifies hashes before publishing handoff text. No WPF/App launch is required by these Core-only checks.

Final build command:required RTK proxy `dotnet build SCUMMexplorer.slnx -c Release`,cwd `D:\\Documents\\Personal\\Code Projects\\SCUMMexplorer`. Final artifact:`Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe` (built without launch).
"""

changes = {}
handoff = Path('docs/NEXT_SESSION_PROMPT.md')
old = handoff.read_text(encoding='utf-8')
assert '* Continuation from `b1381ff`:' not in old
head, _old_prompt = old.split('Next-session request (ready to copy):\n\n', 1)
anchor = 'State at checkpoint (2 October 2026):\n\n'
assert head.count(anchor) == 1
changes[handoff] = head.replace(anchor, anchor + top, 1) + 'Next-session request (ready to copy):\n\n' + prompt
for name, heading in (
    ('LOOM_EGA_PLAN.md', '## Current follow-up (2 October)\n\n'),
    ('LOOM_SCRIPT_MAP.md', '## Follow-up findings (30 September)\n\n'),
    ('LOOM_CUTSCENE_DISCOVERY.md', '## Current replay coverage (2 October)\n\n'),
):
    path = Path('docs') / name
    old = path.read_text(encoding='utf-8')
    assert old.count(heading) == 1 and status not in old
    paragraph = status
    if name == 'LOOM_CUTSCENE_DISCOVERY.md':
        paragraph += ' Coverage remains334 starts,168/235 sites (71.49% covered),50 outstanding owners,123 unresolved edges;the percentage is literal-site coverage,not broader plan completion.'
    changes[path] = old.replace(heading, heading + paragraph + '\n\n', 1)
changelog = Path('docs/CHANGELOG.md')
old = changelog.read_text(encoding='utf-8')
entry = """## 2 October: actual callee/caller boundaries and notified native mutations

| Change | Result | Record |
| --- | --- | --- |
| `5fcc2a0` | Actual supplied/default/restored locals,dispatch,child end/yield,host return and caller continuation;no equality-derived numeric origins | [calls](../.verification/archive/runtime-calls-20261002/call-validation.md) |
| `995e97e` | Copied loadRoomWithEgo/implicit ego/string/owner/wait dependencies;native/effect barriers and estimates intact | [access](../.verification/archive/runtime-calls-20261002/access-validation.md) |
| `b3ee09e` | Executed object/actor native mutation notifications,including identical/attempted no-op writes;palette lifetime separate | [native](../.verification/archive/runtime-calls-20261002/native-validation.md) |
| Final preservation | Core501/focused175;13 exact packets/legacy chronologies/completion records;76 original terminals;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;numeric/native challenge exact;clean final main Release;App0/no launch/no push/promotion;71.49% literal-site coverage unchanged | [validation](../.verification/archive/runtime-calls-20261002/validation.md) |


"""
assert old.startswith('# Changelog\n\n') and entry not in old
changes[changelog] = old.replace('# Changelog\n\n', '# Changelog\n\n' + entry, 1)
validation_path = base / 'validation.md'
assert not validation_path.exists()
changes[validation_path] = validation
for path, value in changes.items():
    path.write_text(value, encoding='utf-8', newline='\n')
print('Published checked call/native preservation facts;all earlier checkpoints retained.')
