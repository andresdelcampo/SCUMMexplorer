"""Finalize exact completed-task references and the next continuation prompt."""
from pathlib import Path
import sys

commit=sys.argv[1]
assert len(commit)==7 and all(c in '0123456789abcdef' for c in commit)
path=Path('docs/NEXT_SESSION_PROMPT.md')
text=path.read_text(encoding='utf-8-sig')
old='the generic production observation task is complete (exact hash from git log).'
assert text.count(old)==1
text=text.replace(old,f'the generic production observation task is complete in `{commit}`.')
marker='Next-session request (ready to copy):'
assert text.count(marker)==1
text=text.split(marker)[0]+marker+f'''

Continue SCUMMexplorer from the latest handoff commit after `{commit}` (hash from git log), preserving palette `e2d1729`, numeric task `8a224a7` and slot cancellation `c91f860`. Read docs/NEXT_SESSION_PROMPT.md and referenced guidance; then correlated-core-20261002 and completion-observer-20261002 validations, summaries, scripts and Core APIs/tests; then the original certainty-protocol/provenance/correlated-drafts packets.

Prioritize Room41/script090 source-bound selected runtime domain/index provenance and completion precision. Core numeric analysis now carries whole hypothetical worlds through masks/indexes/arguments, with opaque responses and explicit effect/yield/callee boundaries. All162 worlds/52 flags/51 comparisons/14 overlaps/four fabricated Cartesian matches and12 practice argument cases remain source-only; none selects runtime input/index, proves inherited path/effects or completes090. The production observer is opt-in and copies read-only snapshots; no Core patch is required. All13 whole recipes and prior instruction values remain exact. Native031 child412/run12143 writesD670 +031D before020 parent408/run12144 reads0028 in the same frame3036, ordered4552/4553/4556/4557. All76 observed zero waits bind their own child only in these selected recipes. No universal cancellation, omitted/aliased/external/concurrent writer or effect proof. Early031 stop0019 can retainD671; D6C terminal projection is not scheduler/callee/cancellation proof. Replacement instances remain distinct.

No estimate clearing, injected prerequisites, room exceptions, helper shortcuts or promotion. Preserve palette/placement separation, four known dye notes/count, raw4=41,15/14, raw6=41,12/11 and separate direct633/actual204 Escape=43,15/15; both level4 orders,096/all four20 routes and status/costume/scythe fixes. Approved deletions/plan trimming remain committed; preserve the unrelated verb-area comment, two untracked audit directories and untracked test.

Fresh Core321;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;clean final main Release. Run appropriate fresh regressions for new work, rebuild main Release at the main cwd, commit tasks separately and update handoff. No App running/launched; no push. Session token usage unavailable; account limits are not session usage. Finish with a ready-to-copy continuation prompt; do not create a separate chat unless asked.
'''
path.write_text(text,encoding='utf-8')
print(f'Final handoff after {commit}; prior history and unrelated work preserved.')
