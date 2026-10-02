from pathlib import Path

link='[Validation](../.verification/room23-input-20261002/validation.md)'
updates={
 'docs/NEXT_SESSION_PROMPT.md': ('## Latest:', '''## Latest: Room23 six watcher sites complete; session gain12

After Room25 commit6c39f96, Room23 actual clicks complete202/214 and all four200 terrain scopes:1393frames,6/6,139 recorded body events all Executed/Conditional. Source201 x<125 ->202,213 x>242 ->214; boxes6/9/10/7 trigger200 +0020/+0084/+00B3/+0058. Final23,ego180/120/box5. First centroid cycle had no scene because x181 did not cross125; preserved. Second endpoint cycle completes; stop diagnosis. These watcher sites have no direct object-verb control; no private direct-script/forced-placement comparison claimed. '''+link+'''.

Audited union **182→188/235=80.00%**,47 remaining; this session gains12 across Room25 and23. Separate fresh single-action **169/235=71.91%**,66 gaps/49 owners/335 starts unchanged. No production/catalog fix, state edits, observer expansion or promotion. All57 protected files exact/unstaged; historical captures untouched. Fresh Core-only probes/checkers pass; Core662/broad/main Release remain dated. Running App untouched: no launch, close, interaction or push.

Next practical candidate: Room10's four remaining sites (watcher200 +0039/+0110 via boxes3/7, local213 +0000, object296/3B +000C). Inspect incoming source prerequisites first. Prefer actual game clicks; if spells are required, use supported Game Status staff/enough notes/learned edits labelled exploration choices, preserve notes/reverse flags, and compare accepted keyboard dispatch and completed body against direct control. Do not count candidate/source-only/partial execution. At most two diagnostic cycles without a route-relevant implementation decision. Commit completed tasks separately. Keep Room25/23/34, sphere and Sharpening complete.

Ready-to-copy continuation: Continue SCUMMexplorer from the Room23 input checkpoint using docs/NEXT_SESSION_PROMPT.md. Start with Room10's four-site cluster by practical input coverage. Audited union188/235 (80.00%); separate fresh single-action169/235 (71.91%). Preserve every constraint, all history and protected57, source notes/reverse flags and the running App; no launch, close, interaction or push. Supported labelled Game Status spell setup is authorized. Verify accepted input and subsequent completion, use bounded diagnostics, and commit completed tasks separately.

'''),
 'docs/LOOM_EGA_PLAN.md': ('**Latest Room25', '**Latest Room23 input:** two shepherd scenes and four terrain transitions complete through actual walking,6/6 scopes,139 Conditional/Executed body events. Audited union188/235=80.00% (session+12); fresh single-action169/235 unchanged. Two bounded cycles, no production change. Next candidate: Room10 four-site cluster by source gates and actual input. Running App/history/protected57 untouched. '+link+'.\n\n'),
 'docs/LOOM_SCRIPT_MAP.md': ('Latest Room25', 'Latest Room23 input,2 October:201 +000D chains202 for x<125/end01BD;213 +0022 chains214 for x>242/end018A. Actual box6/9/10/7 input completes200 starts0020/0084/00B3/0058, ends004D/00A8/00EE/0079. Six scopes,139 Conditional/Executed body events,1393frames; no status/placement injection or direct-control equivalence. '+link+'.\n\n'),
 'docs/LOOM_CUTSCENE_DISCOVERY.md': ('**Latest input supplement:', '**Latest Room23 input supplement:** six completed watcher sites raise audited union188/235=80.00%,47 remaining. Session gain12 with Room25. Fresh single-action169/235=71.91%,66 gaps/49 owners/335 starts unchanged. No catalog promotion or execution fix. Failed first cycle retained; second source-point route6/6. '+link+'.\n\n'),
 'docs/CHANGELOG.md': ('## 2 October:', '## 2 October: verify Room23 shepherd and terrain watcher input\n\nSix actual walking inputs complete202/214 and four200 sites,6/6 scopes in1393frames;139 recorded body events remain Conditional/Executed. Audited union182→188/235 (80.00%), session+12; fresh single-action169/235 unchanged. Two bounded cycles, failed centroid route retained. No production change, App interaction or push; protected57 exact/unstaged. '+link+'.\n\n'),
}
for name,(marker,text) in updates.items():
 p=Path(name); s=p.read_text(encoding='utf-8')
 assert marker in s and text not in s
 p.write_text(s.replace(marker,text+marker,1),encoding='utf-8')
