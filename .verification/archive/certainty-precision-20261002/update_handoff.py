from pathlib import Path
import sys

commit = sys.argv[1]
record = '../.verification/archive/certainty-precision-20261002/validation.md'
summary = (
    f'2 October generic certainty precision after `5e86a68`: palette implementation `{commit}` separates palette-only actorOps provenance '
    'from placement/movement without clearing estimates. All four dye inputs/count now stay known; native gap snapshots bound the next020 '
    'path transition at D67 completion (known1→estimated0, pending0028→0060). Isolated slices preserve inherited path; source points to020 +0028, '
    'but snapshots do not observe the intermediate instruction. D6C instruction-only031 terminal paths project0; D67 has an early-stop0019 '
    'counterexample leaving1, so no blanket completion reset is justified. All seven023 masks derived;51 tuple comparisons still disjoint, '
    '14 first-component overlaps require correlated rejection proof. None is native090 completion. Raw4 native remains41,15/14 with final '
    'input/mode/target/count/raw4/D70 estimates; raw6 remains41,12/11; direct633/actual204 Escape remains43,15/15. Both level4 orders '
    'and096/all four20 routes preserve exact timing/rooms/scopes. Core298 (nine new palette cases),363 exact roots/fingerprints/memberships/'
    'sequences unchanged, Blockers0;76 unchanged zero-gap pixels; main Release clean. No App launched/running, no WPF claim, no promotion or push. '
    f'[Evidence and reproduction]({record}).'
)
paths = [
    ('docs/LOOM_EGA_PLAN.md', '## Current follow-up (2 October)\n'),
    ('docs/LOOM_SCRIPT_MAP.md', '## Follow-up findings'),
    ('docs/LOOM_CUTSCENE_DISCOVERY.md', '## Current replay coverage (2 October)\n'),
]
for name, marker in paths:
    p = Path(name)
    text = p.read_text(encoding='utf-8-sig')
    if summary in text:
        continue
    at = text.index(marker)
    end = text.index('\n', at) + 1
    text = text[:end] + '\n' + summary + '\n\nPrevious checkpoint follows; its palette-to-placement propagation is corrected above.\n' + text[end:]
    if name.endswith('LOOM_SCRIPT_MAP.md'):
        text = text.replace('Last updated 1 October 2026 (nested scope scheduling and source-derived game status).',
            'Last updated 2 October 2026 (Room41 certainty precision and source completion/domain research).', 1)
    p.write_text(text, encoding='utf-8')

p = Path('docs/NEXT_SESSION_PROMPT.md')
text = p.read_text(encoding='utf-8-sig')
marker = 'State at checkpoint (2 October 2026):\n'
if summary not in text:
    text = text.replace(marker, marker + '\n* ' + summary + '\n* New packet: `.verification/archive/certainty-precision-20261002/`. Read palette-validation.md, validation.md, precision-summary.json and HandshakeAudit before earlier packets. No production practice/domain/loop reset implemented. Additional unrelated tracked deletions/plan edits appeared during the session and remain unstaged/untouched.\n\nPrevious checkpoint follows.\n', 1)
start = text.index('2. Room41 native certainty remains priority.')
end = text.index('\n3. Native source-executed level4', start)
text = text[:start] + (
    '2. Room41 native certainty remains priority. Read the new certainty-precision packet first. Preserve palette/placement separation and '
    'its source reason; all four dye input notes/count now stay known. The next generic precision work is the source020/031 D67 completion '
    'protocol (0028 wait; source031 +031D/+0243, with early-stop0019 counterexample), practice D6C value domains, and correlated022 rejection. '
    'Do not confuse instruction-only terminal projection with reached/concurrent source effects: retain scheduler, cancellation, caller/index, '
    'movement, unknown external values and inherited path provenance.51 source-domain comparisons exclude collisions;14 have equal first '
    'components. Single masks or independent note domains cannot prove the complete rejection predicate. Raw4 final010/022 first estimated '
    'branch0000 and raw6 first022 branch007D remain. Native090 ends41,15/14 or12/11; direct633/204 override43,15/15 is separate. '
    'No blanket join/loop/completion resets, estimate clearing, injected state/flags/notes/placements, helper shortcuts, room exceptions or promotion. '
    'Fresh repeated recipes, exact roots/memberships/sequences, Blockers and Core-only RoomAudit remain required for execution changes. No App '
    'should run or be launched; prior WPF/navigation/input checks stay dated. Native opening remains unresolved.\n'
    ) + text[end:]
start = text.index('Next-session request:\n')
end = text.find('\n\n', start + len('Next-session request:\n\n'))
if end < 0:
    end = len(text)
text = text[:start] + 'Next-session request:\n\n' + (
    f'Continue from the2 October certainty precision continuation (palette `{commit}`; research hash from git log). Read the new '
    '.verification/archive/certainty-precision-20261002/ validation, summaries and HandshakeAudit. Prioritize generic source020/031 completion/practice '
    'precision and correlated randomized022 rejection. Preserve palette provenance separately from placement and all inherited estimates. '
    'Native raw4 remains41,15/14; raw6 remains41,12/11; no native090 completion. Keep native level4/096/20 routes and status/costume/scythe '
    'fixes. No clearing, injected prerequisites, room exceptions or catalog promotion. Fresh appropriate regressions and main Release; commit '
    'completed tasks separately and update handoff. Preserve unrelated untracked work and newly appeared unrelated tracked deletions. No App '
    'running or launched, no push. Finish with clickable continuation chip; no separate chat.'
    ) + text[end:]
p.write_text(text.rstrip() + '\n', encoding='utf-8')

p = Path('docs/CHANGELOG.md')
text = p.read_text(encoding='utf-8-sig')
marker = '## 2 October: generic palette certainty precision\n'
text = text.replace(marker,
    '## 2 October: Room41 completion protocol and correlated-domain research\n\n'
    '| Change | Result | Record |\n| --- | --- | --- |\n'
    '| Source completion/input audit | Four dye notes now known; gap snapshots bound020 D67 path taint; D6C terminal projection and D67 early-stop counterexample recorded without production resets | [evidence](../.verification/archive/certainty-precision-20261002/validation.md) |\n'
    '| Packed-domain precision | Seven loaded023 masks,51 disjoint tuple comparisons and14 first-component overlaps; no native090 completion/promotion | [summary](../.verification/archive/certainty-precision-20261002/precision-summary.json) |\n\n'
    + marker, 1)
p.write_text(text, encoding='utf-8')
