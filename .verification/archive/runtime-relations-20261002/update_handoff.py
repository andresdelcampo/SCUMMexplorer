"""Update handoff after the separate committed completion task; keep historical constraints."""
from pathlib import Path
import sys

completion = sys.argv[1]
assert len(completion) >= 7 and all(c in '0123456789abcdef' for c in completion)
p = Path('docs/NEXT_SESSION_PROMPT.md')
text = p.read_text(encoding='utf-8')
marker = 'State at checkpoint (2 October 2026):\n'
assert text.count(marker) == 1
state = f'''
* Continuation from `a850740`: generic optional runtime relation lifetime/invalidation is committed in `8bad888`; generic selected completion correspondence and native audit are committed in `{completion}`. `LoomRuntimeRelationTracker` keeps historical copied lineage separate from live cell/index correspondence. Same-value/source/index/word-bit writes, changed/missing snapshots/alias estimates, filtered/unknown accesses, explicit external invalidation, bounds and slot cancellation/termination retire relations. Scoped locals survive resumes; ordered arguments do not infer callee binding/return. `LoomCompletionObserver` matches surviving snapshot-confirmed literal zero writers from the actual reader's own causal child, with independent estimates and a separate writer terminal observation. Stale earlier ends cannot describe later writes. Neither API changes playback, estimates or source-domain policy; the App installs neither.
* Final native capture: all13 whole packets and entire prior operand-binding/cancellation/expanded chronology are exact. All76 zero waits match their own observed literal child through the generic API, only in these13 recipes. Native031 child412/run12143 +031D/4552 →confirmation4553 →020 parent408/run12144 +0028/4556 →estimated increment4557 remain frame3036. All writer/read value/index/path estimates remain; terminal/self-stop is not protocol completion. Omitted/external/aliased/concurrent writers, same-value unobserved effects, cancellation/replacement and scheduler/callee limits remain. Early0019/D671 and D6C projection limits remain.
* Selected observations are unchanged: boot1018/live3018 →native input[1,4,1,1] →056..059/indexes0..3 →020 arguments →023 slot4/index8/current3018 and response03B/index9/4241 →026(4,0). Practice index0/local000 selects estimated052=1, then estimated030 argument1. No general runtime-domain transport or inherited path/effect proof. Numeric analysis stays source-only:162 worlds,52 flags,51 comparisons/14 overlaps,four fabricated matches and12 practice cases establish no runtime selection or090 completion.
* Fresh final Core348 (12 lifetime/10 completion cases);363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;numeric report exactly unchanged;clean final main Release;App0/no launch/no push. Raw4=41,15/14, raw6=41,12/11, separate direct633/actual204 Escape=43,15/15; both level4 orders,096/all four20 routes, palette/placement separation, all four known dye notes/count and every UI/unrelated-work constraint remain. [API](../.verification/archive/runtime-relations-20261002/api-validation.md), [validation](../.verification/archive/runtime-relations-20261002/validation.md), [relations](../.verification/archive/runtime-relations-20261002/relation-summary.json), [bindings](../.verification/archive/runtime-relations-20261002/binding-summary.json), [checks](../.verification/archive/runtime-relations-20261002/check-summary.json). Final capture base: `Exe/RuntimeRelationsSession-20261002/final-evidence`; final checks under its parent. Earlier successful captures and the corrected practice-index assertion are retained. Session token usage remains unavailable; account limits were not substituted.
* Read runtime-relations-20261002 validations, summaries, scripts and both Core APIs/tests first, then runtime-bindings-20261002, correlated-core/completion-observer and original certainty-protocol/provenance/correlated-drafts. Next: generic source-access coverage and external/same-value mutation notifications; observed arithmetic/index/argument lineage with independent path/effects; bounded writer/scheduler/callee/cancellation evidence. Do not turn surviving observed relations or matched zero waits into known numeric domains, certainty resets, prerequisite injections, helper summaries, room exceptions or promotion.

Earlier checkpoints below remain historical; only their statements that optional relation lifetime/completion observation is unimplemented are superseded. General domain/effect transport and completion proof remain unresolved.
'''
text = text.replace(marker, marker+state)
old = 'Read runtime-bindings-20261002 first. Continue generic provenance for relation lifetime and invalidation across writes, word/bit/index aliases, arguments, omitted/external/concurrent effects and slot replacement/cancellation.'
new = 'Read runtime-relations-20261002 first, then runtime-bindings-20261002. Optional copied relation lifetime/invalidation and selected completion correspondence are implemented. Continue generic source-access coverage, external/same-value mutation notifications and observed arithmetic/index/argument lineage, retaining independent inherited path/effects, omitted/external/concurrent writers and slot replacement/cancellation.'
assert old in text
text = text.replace(old, new)
start = text.index('Next-session request (ready to copy):')
prompt = f'''Next-session request (ready to copy):

Continue SCUMMexplorer from the latest handoff commit after runtime lifetime `8bad888` and completion precision `{completion}`, preserving operand API `82679ae`, provenance audit `e523f8a`, palette `e2d1729`, numeric `8a224a7`, observer `96b79f6` and cancellation `c91f860`.

Read docs/NEXT_SESSION_PROMPT.md and referenced guidance; then runtime-relations-20261002 validations, relation/binding/check summaries, scripts and Core APIs/tests; then runtime-bindings-20261002; then correlated-core/completion-observer and original certainty-protocol/provenance/correlated-drafts packets.

Prioritize generic source-access coverage and external/same-value mutation notifications, then observed arithmetic/index/argument lineage and independent completion precision. Optional copied relations expire on writes/index/word-bit aliases, changed/missing snapshots/alias estimates, filters/unknown access, external invalidation, bounds and slot cancellation/termination. Historical lineage remains distinct; arguments do not infer callee binding/return. Selected observations bind boot1018/current3018, native input[1,4,1,1], cells056..059/indexes0..3,023 slot4/index8 and response03B/index9, then026(4,0). Practice index0/local000 selects estimated052=1. Preserve all value/index/path estimates; these observations establish no general runtime-domain transport or inherited effects.

Numeric analysis stays source-only. The162 worlds,52 flags,51 comparisons/14 overlaps,four fabricated matches and12 practice cases prove no runtime selection or090 completion.

Native031 child412/run12143 writesD670 at031D before020 parent408/run12144 reads0028 in frame3036, sequences4552/4553/4556/4557. All76 zero waits match their own observed literal child only in these13 recipes. Keep the writer's separate terminal observation after confirmation/before read; stale earlier ends cannot describe a later write. Preserve omitted/external/aliased/concurrent writer, unchanged unobserved external effects, cancellation/replacement and scheduler/callee limits, early0019/D671 and D6C projection limits. Neither surviving relations nor selected writer matches prove protocol completion.

No estimate clearing, injected prerequisites, room exceptions, helper shortcuts or promotion. Preserve palette/placement separation, all four known dye notes/count, raw4=41,15/14, raw6=41,12/11, separate direct633/actual204 Escape=43,15/15, both level4 orders,096/all four20 routes and every recorded UI/unrelated-work constraint. Preserve the verb-area comment, two untracked audit directories and untracked test.

Fresh Core348;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;numeric report unchanged;clean final main Release. Run fresh appropriate regressions, rebuild main Release, commit tasks separately and update handoff. No App running/launched; no push. Session token usage unavailable; account limits are not session usage. Finish with a ready-to-copy continuation prompt; no separate chat unless requested.
'''
p.write_text(text[:start]+prompt, encoding='utf-8')
print('Handoff preserves historical constraints and records both committed tasks:', completion)
