"""Record completed API/audit commits and preserve the earlier constraint/history sections."""
from pathlib import Path
import sys

api, audit = sys.argv[1:3]
assert all(len(h)==7 and all(c in '0123456789abcdef' for c in h) for h in (api,audit))
p=Path('docs/NEXT_SESSION_PROMPT.md')
text=p.read_text(encoding='utf-8-sig')
marker='State at checkpoint (2 October 2026):\n\n'
assert text.count(marker)==1
text=text.replace(marker,marker+f'''* Latest continuation from `4a11fbc`: optional pre-instruction operand bindings completed in `{api}`; selected Room41 provenance audit completed in `{audit}`. Bindings use the executor resolver and copied immutable records, with selected cell/index, separate value/index estimates, bit backing word and ordered call arguments. Missing/filtered indexes remain unresolved; default App has no observer. No execution policy or estimate changes. Read [API validation](../.verification/runtime-bindings-20261002/api-validation.md), [selected-run validation](../.verification/runtime-bindings-20261002/validation.md), [bindings](../.verification/runtime-bindings-20261002/binding-summary.json), [checks](../.verification/runtime-bindings-20261002/check-summary.json), generators/analyzers and Core tests first.
* Selected-run evidence now records boot local211 +0349 index0/celllocal002=1018 →+0350/var03A; final live snapshot3018 →the complete native input[1,4,1,1] →010's actual056..059 cells/indexes0..3 →020 arguments →022/023's actual slot4/index8/current3018 and response03B/index9/4241 →026 arguments4/0. Values/indexes/inherited path retain every original estimate; this is not general runtime-domain transport or independent path/effect certainty. First practice local201 +0016→023(2) reads036=1200, tuple[1,1,1,2]; known practice index0 reads estimated052=1 and passes estimated1 at0028. Numeric worlds remain separate source hypotheses. No native090 completion: raw4=41,15/14, raw6=41,12/11; direct633/actual204 Escape separate43,15/15.
* All13 whole packets and prior observer records/sequences/cancellations are exact. Native031 child412/run12143 writesD670 +031D before020 parent408/run12144 reads0028 in frame3036, still4552/4553/4556/4557. The expanded boot capture has extra sequence numbers and does not replace this primary chronology. All76 zero waits bind their own observed literal child only in these13 recipes; omitted/aliased/external/concurrent writer and cancellation limits remain. Early0019/D671 and D6C projection limits remain. Fresh Core326;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;source-only numeric report exactly unchanged;clean final main Release;App0/no launch/no push. All palette/route/UI/unrelated-work constraints below remain current. Session token-usage tooling unavailable; account limits not substituted.

Earlier checkpoints follow; statements that selected operand bindings are unimplemented are superseded only for this optional copied observation. General runtime source-domain transport, inherited path/effect and scheduler/callee/cancellation proof remain unresolved.

''')
marker='Ordered next work:\n\n'
assert text.count(marker)==1
text=text.replace(marker,marker+'''Read runtime-bindings-20261002 first. Continue generic provenance for relation lifetime and invalidation across writes, word/bit/index aliases, arguments, omitted/external/concurrent effects and slot replacement/cancellation. The selected input/index values are now recorded for these seeded recipes; they remain estimated where recorded and do not authorize a loop/join/helper reset. Completion remains a separate writer/path/effect/scheduler/callee/cancellation problem. Preserve the exact historical evidence below and use its current corrections.

''')
marker='Next-session request (ready to copy):'
assert text.count(marker)==1
text=text.split(marker)[0]+marker+f'''

Continue SCUMMexplorer from the latest handoff commit after operand API `{api}` and selected Room41 audit `{audit}` (hash from git log), preserving palette `e2d1729`, numeric `8a224a7`, observer `96b79f6` and cancellation `c91f860`.

Read docs/NEXT_SESSION_PROMPT.md and referenced guidance; then runtime-bindings-20261002 validations, binding/check summaries, scripts and Core APIs/tests; then correlated-core/completion-observer-20261002 and original certainty-protocol/provenance/correlated-drafts packets.

Prioritize generic selected runtime source relations and completion precision. Optional bindings record pre-instruction selected cells/indexes/arguments, with independent value/index/path estimates and unresolved missing/filtered values. Actual boot local211 index0/cell002=1018 →03A; live3018 →native input[1,4,1,1] →056..059/indexes0..3 →020 arguments →023 slot4/index8/current3018,03B/index9/4241 →026(4,0). First practice023(2) reads036=1200, tuple[1,1,1,2]; local201 known index0 selects estimated052=1 and passes estimated1. These are selected seeded observations, not known runtime-domain transport or inherited effect/path proof. Numeric analysis stays source-only:162 worlds,52 flags,51 comparisons/14 overlaps,four fabricated matches,12 practice argument cases prove no runtime selection or090 completion.

Native031 child412/run12143 writesD670 at031D before020 parent408/run12144 reads0028 in frame3036, still sequences4552/4553/4556/4557. All76 zero waits bind their own observed literal child only in these13 recipes. Preserve omitted/external/aliased/concurrent writers, cancellation/replacement and scheduler/callee limits, early0019/D671 and D6C terminal projection limits. Bindings are snapshots, not an exhaustive access log or completion summary.

No estimate clearing, injected prerequisites, room exceptions, helper shortcuts or promotion. Preserve palette/placement separation, all four known dye notes/count, raw4=41,15/14, raw6=41,12/11, separate direct633/actual204 Escape=43,15/15, both level4 orders,096/all four20 routes and all status/costume/scythe/UI/unrelated-work constraints. Preserve the verb-area comment, two untracked audit directories and untracked test.

Fresh Core326;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;numeric report unchanged;clean final main Release. Run fresh appropriate regressions, rebuild main Release at the main cwd, commit tasks separately and update handoff. No App running/launched; no push. Session token usage unavailable; account limits are not session usage. Finish with a ready-to-copy continuation prompt; no separate chat unless requested.
'''
p.write_text(text,encoding='utf-8')
print('Completed API/audit references and continuation recorded; prior history and constraints preserved.')
