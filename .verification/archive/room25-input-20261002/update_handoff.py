from pathlib import Path

link = '[Validation](../.verification/archive/room25-input-20261002/validation.md)'
updates = {
    'docs/NEXT_SESSION_PROMPT.md': ('## Latest:', '''## Latest: Room25 six-site input route complete

After89a2211, actual clicks on decoded box4 then box3 trigger local200 and069; three lamb double-clicks dispatch447/36 through007 +002E and complete204/205/206; exit click dispatches446/37 through006 +015B and completes208/loads24. Six scopes,208 exactly equal recorded scene-body events versus the direct-object control, all Executed, estimates retained. Direct2634/input2650frames; no production correction, observer expansion, status edits or spell prerequisites. ''' + link + '''.

Audited union **176→182/235=77.45%**, six newly qualified sites,53 remain. Separate fresh single-action **169/235=71.91%** unchanged,66 gaps/49 owners/335 catalog starts. Preserve all historical evidence and57 protected files exact/unstaged. No App launch/close/interaction or push; Core662/broad/main Release remain dated, fresh Core-only probe/checker pass.

Next highest-value candidate: Room23's six-site cluster, using source walk-box transitions and actual input, then supported Game Status staff/notes/learned edits if spells are needed, explicitly labelled exploration choices. Do not rewrite source notes/reverse flags. Two bounded diagnostic cycles at most without a route-relevant implementation decision. Commit completed tasks separately. Room25, Room34, sphere and Sharpening are complete; do not repeat them.

Ready-to-copy continuation: Continue SCUMMexplorer from the Room25 input checkpoint using this handoff. Start with Room23's remaining cluster by source walking and actual input. Audited union182/235 (77.45%); fresh single-action169/235 (71.91%), distinct. Preserve all constraints/history/protected57 and the running App; no launch, close, interaction or push. Use supported labelled status edits for spell setup and preserve notes/reverse flags. Compare accepted input plus completion against direct controls, never substitute replay for input verification.

'''),
    'docs/LOOM_EGA_PLAN.md': ('**Current priority:', '**Latest Room25 input:** six source sites complete through box4→box3 walking, three lamb double-clicks and exit click. Six scopes/208 identical body events against the direct-object control; no execution change. Audited union182/235=77.45%, fresh single-action169/235 unchanged. Next candidate: Room23 six-site cluster with source walking/input. All constraints and protected57 preserved; running App untouched. ' + link + '.\n\n'),
    'docs/LOOM_SCRIPT_MAP.md': ('Latest Room34', 'Latest Room25 input,2 October: box4 watcher200 +0058/007E writesD92; box3 watcher +0046 starts069/end07ED. Actual447/36 dispatches at007 +002E start204(0)/205/206 through object offsets0053/0061/006C; ends03C5/00B0/010C. Exit446/37 dispatch at006 +015B starts208/end0060/load24 +0066. Six completed sites;208 recorded body events equal direct control, all estimates retained. ' + link + '.\n\n'),
    'docs/LOOM_CUTSCENE_DISCOVERY.md': ('**Interactive evidence supplement', '**Latest input supplement:** Room25 adds six sites with completed walking/double-click/exit evidence. Audited union182/235=77.45%,53 remaining; fresh single-action169/235=71.91%,66 gaps/49 owners/335 starts unchanged. Six scopes/208 exact recorded body events against direct-object control, no production/catalog change. ' + link + '.\n\n'),
    'docs/CHANGELOG.md': ('## 2 October:', '## 2 October: verify Room25 walking and repeated lamb input\n\nActual box4/box3 clicks, three lamb double-clicks and an exit click complete six previously unvalidated sites, returning24.208 recorded scene-body events exactly match the direct-object control; estimates preserved. Audited union176→182/235 (77.45%); fresh single-action169/235 unchanged. No production fix, catalog change, App interaction or push; protected57 exact/unstaged. ' + link + '.\n\n'),
}
for name,(marker,text) in updates.items():
    p=Path(name)
    s=p.read_text(encoding='utf-8')
    assert text not in s and marker in s
    p.write_text(s.replace(marker,text+marker,1),encoding='utf-8')
