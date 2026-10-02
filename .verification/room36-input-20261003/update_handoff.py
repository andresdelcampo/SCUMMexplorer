from pathlib import Path

link='[Validation](../.verification/room36-input-20261003/validation.md)'
text='Room36 accepted box10→4→3→9 clicks complete all four watcher200 sites001D/0048/0077/00A6,547frames,4/4 scopes,29 Executed/Conditional body events. Audited union192→196/235 (83.40%),39 remaining; separate fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. Two bounded cycles; failed first long walk retained/excluded. No production/catalog change. Protected57 and prior evidence exact; running App untouched. '+link+'.'
updates={
'docs/NEXT_SESSION_PROMPT.md':('## Latest:', '''## Latest: Room36 four watcher sites complete — 3 October

Continues84da7df. '''+text+'''

User now requests continuing successive audit tasks in each session toward200K context, committing each completed task, instead of stopping at the first completed room. Session context/token telemetry is unavailable; do not claim an exact threshold was measured. Keep each route to at most two diagnostic cycles without a route-relevant implementation decision. Preserve all prior constraints and estimates. No App launch, close, interaction, rebuild or push. Fresh Core-only probe/checker pass; Core662/broad/main Release remain dated.

Next candidate: Room24 watcher200 +001D/+0089 and211 +0018. Inspect incoming source prerequisites before actual clicks; supported labelled staff/notes/learned setup remains authorized if needed, source notes/reverse flags unchanged. No fabricated private-script control for watchers. Keep Room36/10/23/25/34, sphere and Sharpening complete.

Ready-to-copy continuation: Continue SCUMMexplorer from the Room36 input checkpoint using docs/NEXT_SESSION_PROMPT.md. Start with Room24's three remaining sites. Audited union196/235; fresh single-action169/235, distinct. Continue successive bounded audit tasks toward200K context, commit each, preserve all history/protected57/estimates and leave the running App untouched. No push.

'''),
'docs/LOOM_EGA_PLAN.md':('**Latest Room10','**Latest Room36 input,3 October:** '+text+' Next: Room24 three-site cluster. Continue successive bounded audit tasks toward200K context per user; exact session telemetry unavailable.\n\n'),
'docs/LOOM_SCRIPT_MAP.md':('Latest Room10','Latest Room36 input,3 October: entry00E5 starts200; changed-box gates0016/0041/0070/009F select3/4/9/10. Actual10→4→3→9 clicks complete00A6/end00CA,0048/end006C,001D/end003D,0077/end009B. Source201/202 alter boxes0–3; no status/placement injection. '+text+'\n\n'),
'docs/LOOM_CUTSCENE_DISCOVERY.md':('**Latest Room10','**Latest Room36 input supplement,3 October:** '+text+' No fresh-root promotion or native full-game claim.\n\n'),
'docs/CHANGELOG.md':('## 3 October:','## 3 October: verify Room36 terrain watcher input\n\n'+text+'\n\n')}
for name,(marker,addition) in updates.items():
    p=Path(name); old=p.read_text(encoding='utf-8')
    assert marker in old and addition not in old
    p.write_text(old.replace(marker,addition+marker,1),encoding='utf-8')
