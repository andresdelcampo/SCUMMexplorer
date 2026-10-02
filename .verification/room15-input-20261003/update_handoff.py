from pathlib import Path
text='Room15 actual box3/5 clicks complete200 +001D/end0062 and006D/end00C4 in249frames,2/2 scopes,23 Executed/Conditional body events. Audited union199→201/235 (85.53%),34 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle, no production/catalog change. Protected57/prior evidence and running App untouched. [Validation](../.verification/room15-input-20261003/validation.md).'
updates={
'docs/NEXT_SESSION_PROMPT.md':('## Latest:', '## Latest: Room15 watcher pair complete — 3 October\n\nAfterd6dc64b. '+text+'\n\nNext candidate: Room17 watcher200 +0022/+004D via boxes4/5. Inspect source gates, then accepted clicks plus complete bodies. Continue successive bounded audits toward200K context per user, with exact telemetry unavailable; commit each completed task. Preserve all earlier constraints, estimates/history/protected57; no App launch, close, interaction, rebuild or push. Core662/broad/main Release remain dated. Keep completed Room15/24/36/10/23/25/34, sphere and Sharpening complete.\n\nReady-to-copy continuation: Continue SCUMMexplorer from Room15 using docs/NEXT_SESSION_PROMPT.md. Start Room17 watcher pair. Audited union201/235 versus fresh single-action169/235. Continue bounded audits toward200K context, commit each, preserve all constraints and the running App.\n\n'),
'docs/LOOM_EGA_PLAN.md':('**Latest Room24','**Latest Room15 input,3 October:** '+text+' Next: Room17 watcher pair.\n\n'),
'docs/LOOM_SCRIPT_MAP.md':('Latest Room24','Latest Room15 input,3 October: entry006B starts200; box3/5 gates0016/0066. '+text+'\n\n'),
'docs/LOOM_CUTSCENE_DISCOVERY.md':('**Latest Room24','**Latest Room15 supplement,3 October:** '+text+'\n\n'),
'docs/CHANGELOG.md':('## 3 October:','## 3 October: verify Room15 watcher input\n\n'+text+'\n\n')}
for name,(marker,addition) in updates.items():
 p=Path(name); old=p.read_text(encoding='utf-8'); assert marker in old and addition not in old
 p.write_text(old.replace(marker,addition+marker,1),encoding='utf-8')
