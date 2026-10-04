"""Add this checkpoint while preserving unrelated working plan edits.

Only a patch against HEAD's plan is staged. The pre-update working plan is saved
under ignored Exe and checked after insertion; no trimming/deletions are restored.
"""
from pathlib import Path
import difflib
import subprocess

output=Path('Exe/CertaintyProvenanceSession-20261002')
assert 'Latest continuation: 2 October continuation from `e8c1c75`' not in Path('docs/NEXT_SESSION_PROMPT.md').read_text(encoding='utf-8-sig'), 'This historical handoff update has already been applied.'
paragraph='''2 October continuation from `e8c1c75`: instruction observer audit `2b91e50` directly observes Room06 local201 +0028 passing an estimated practice note to030/031;031 +0026 taints its path, +0227 writes estimatedD6C0, then native031 +000A →completion +031D →020 wait +0028 propagates provenance. All13 full recipe packets equal the previous palette-precision packets. Earlier gap snapshots remain frame bounds; new isolated observer records actual intermediate instructions without changing production. Early031 stop0019 can leaveD671; decoded011→021 stops020, distinct from completion and not a cancellation proof. Source-only complete022/023 numeric interpretation covers81 boot assignments/162 forward-reverse worlds, all selecting slot4 before026;52 optional flag cases and the51 earlier comparisons/14 overlaps reproduce. Independent input components fabricate54 tuples from3, including four earlier matches. Numeric domains do not establish selected runtime input/index, reached effects, scheduling, movement or native090 completion. Palette `e2d1729` unchanged; all dye notes/count known. Raw4 remains41,15/14; raw6 remains41,12/11; direct633/actual204 Escape separate43,15/15. Both level4 orders,096/all four20 routes preserved. Fresh Core298;363 exact roots/fingerprints; memberships/sequences unchanged; Blockers0;76 unchanged zero-gap renders; clean main Release; no App or push. [Instruction evidence](../.verification/archive/certainty-provenance-20261002/validation.md), [correlated predicate](../.verification/archive/correlated-drafts-20261002/validation.md).

'''
def insert(path,anchor,addition):
    p=Path(path)
    before=p.read_text(encoding='utf-8-sig')
    assert before.count(anchor)==1
    if addition in before:
        assert before.count(addition)==1
        return before.replace(addition,'',1),before
    after=before.replace(anchor,anchor+addition)
    p.write_text(after,encoding='utf-8')
    assert after.replace(addition,'',1)==before
    return before,after

plan_path=Path('docs/LOOM_EGA_PLAN.md')
plan_anchor='## Current follow-up (2 October)\n\n'
before,after=insert(plan_path,plan_anchor,paragraph)
snapshot=output/'plan-before-handoff.md'
if snapshot.exists():
    assert snapshot.read_text(encoding='utf-8')==before
else:
    snapshot.write_text(before,encoding='utf-8')
head=subprocess.check_output(['rtk','proxy','git','show','HEAD:docs/LOOM_EGA_PLAN.md']).decode('utf-8-sig')
assert head.count(plan_anchor)==1
desired=head.replace(plan_anchor,plan_anchor+paragraph)
patch=''.join(difflib.unified_diff(head.splitlines(keepends=True),desired.splitlines(keepends=True),
    fromfile='a/docs/LOOM_EGA_PLAN.md',tofile='b/docs/LOOM_EGA_PLAN.md'))
patch_path=output/'plan-handoff-only.patch'
patch_path.write_text(patch,encoding='utf-8',newline='\n')
subprocess.run(['rtk','proxy','git','apply','--cached',str(patch_path)],check=True)
insert('docs/LOOM_SCRIPT_MAP.md','## Follow-up findings (30 September)\n\n',paragraph)
insert('docs/LOOM_CUTSCENE_DISCOVERY.md','## Current replay coverage (2 October)\n\n',paragraph)
insert('docs/NEXT_SESSION_PROMPT.md','State at checkpoint (2 October 2026):\n\n',
    '* Latest continuation: '+paragraph.strip()+'\n'+
    '* Read `.verification/archive/certainty-provenance-20261002/validation.md`, `trace-summary.json`, `check-summary.json`, `create_audit.py` and `analyze_trace.py`, then `.verification/archive/correlated-drafts-20261002/validation.md`, `predicate-summary.json` and `analyze_predicate.py` before the prior certainty-precision packet. Production is unchanged; the observer is generated under ignored Exe. The numeric interpreter stops before026 and must not supply session state. Research hashes are in git log; instruction audit is `2b91e50`. No generic numeric-domain/completion implementation yet.\n\nPrevious checkpoint follows.\n\n')
next_path=Path('docs/NEXT_SESSION_PROMPT.md')
text=next_path.read_text(encoding='utf-8')
old_first='1. Inspect git/preserve unrelated work. Read '
assert text.count(old_first)==1
text=text.replace(old_first,'1. Inspect git/preserve unrelated work. Read the latest instruction-provenance and correlated-predicate packets above first, then ')
text=text.replace('Finish with clickable continuation chip; no separate chat unless requested.',
    'Finish with a ready-to-copy continuation prompt; no separate chat unless requested.')
text=text.replace('Read the new certainty-precision packet first.','Then read the prior certainty-precision packet.')
anchor='2. Room41 native certainty remains priority. '
assert text.count(anchor)==1
text=text.replace(anchor,anchor+'Start with the new instruction observer and complete predicate packets. Direct observations now establish the intermediate020 +0028 path transition; retain the earlier snapshots as frame-only evidence. Trace the practice-note/index domain through023→Room06 local201→030→031, then completion writer identity/scheduler/cancellation. Preserve independent numeric and path/effect provenance. The162 hypothetical numeric worlds and four fabricated Cartesian inputs are not known runtime input or a native090 proof. ')
old_request=text.index('Next-session request:\n\n')
text=text[:old_request]+'''Next-session request (ready to copy):

Continue SCUMMexplorer from the latest committed2 October instruction-provenance/correlated-predicate checkpoint (hash from git log), preserving palette e2d1729. Read docs/NEXT_SESSION_PROMPT.md and referenced guidance, then .verification/archive/certainty-provenance-20261002/validation.md, trace-summary.json, check-summary.json and the observer scripts; then .verification/archive/correlated-drafts-20261002/validation.md, predicate-summary.json and analyze_predicate.py; retain earlier certainty-precision/HandshakeAudit evidence.

Prioritize Room41/script090 generic numeric-domain and completion precision. Observed source chain: Room06 local201 +0028 estimated practice note→030/031 +0026→D6C0 write +0227→native031 +000A→D67 completion +031D→020 +0028. Earlier gap snapshots only bound the frame; the new isolated observer actually records intermediate instructions. Keep numeric, path/effect, caller/index, movement, scheduler/cancellation and concurrent provenance separate. Early031 stop0019 can leaveD671; decoded011→021 cancellation of020 is not a reached cancellation proof. Do not turn D6C's instruction-only terminal projection into a scheduler/callee/cancellation proof.

Continue correlated domain work:81 complete boot assignments/162 numeric forward-reverse worlds select slot4 before026;52 flag projections reproduce seven023 masks and51 earlier comparisons with14 first-component overlaps. Three source input tuples become54 if independent components are used, fabricating four earlier matches. This does not establish selected runtime input/index, path, dispatch or native090 completion. Preserve unknown external values and protocol/writer identity; no estimate clearing, injected prerequisites, room exceptions, helper shortcuts or catalog promotion.

Palette remains separate from placement; all four dye notes/count remain known. Native raw4 remains41,15/14, raw6 remains41,12/11; direct633/actual204 Escape remains separate43,15/15. Preserve both native level4 recipes, completed096/all four20 routes, status/costume/scythe fixes and unrelated work. Unrelated tracked deletions, verb-area change and plan trimming remain unstaged; do not restore or include them. Fresh checks: Core298;363 unchanged literal roots/fingerprints/memberships/sequences; Blockers0;76 unchanged zero-gap renders; clean main Release. Run appropriate fresh regressions for new changes, rebuild main Release, commit completed tasks separately and update handoff. No App running or launched, no push. Finish with a ready-to-copy prompt for the next session.
'''
next_path.write_text(text,encoding='utf-8')
assert plan_path.read_text(encoding='utf-8').replace(paragraph,'',1)==snapshot.read_text(encoding='utf-8')
print('Handoff updated; only the new Current follow-up paragraph staged in the plan. Unrelated working plan text preserved exactly.')
