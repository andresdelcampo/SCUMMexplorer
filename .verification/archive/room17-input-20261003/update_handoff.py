from pathlib import Path
text='Room17 visible exit353 center312,72 then upper-path300,90 clicks complete200 +0022/end0042 and004D/end006D.241frames,2/2 scopes,15 Executed/Conditional body events. Audited union201→203/235 (86.38%),32 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. First off-picture click rejected/excluded; second cycle passes. No production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room17-input-20261003/validation.md).'
updates={
'docs/NEXT_SESSION_PROMPT.md':('## Latest:', '## Latest: Room17 watcher pair complete — 3 October\n\nAfterceb395c. '+text+'\n\nNext candidate: Room22 watcher200 +002F/+0083 via boxes10/11. Source incoming gates first; accepted input and completion required. Continue successive bounded audits toward200K context per user; exact telemetry unavailable. Commit each task, keep two-cycle bound and all history/protected57/estimates. No App launch/close/interaction/rebuild or push. Core662/broad/main Release dated. Completed Room17/15/24/36/10/23/25/34, sphere and Sharpening stay complete.\n\nReady-to-copy continuation: Continue SCUMMexplorer from Room17 using docs/NEXT_SESSION_PROMPT.md. Start Room22 watcher pair. Audited union203/235 versus fresh single-action169/235. Continue bounded tasks toward200K context, commit each, preserve constraints and running App.\n\n'),
'docs/LOOM_EGA_PLAN.md':('**Latest Room15','**Latest Room17 input,3 October:** '+text+' Next: Room22 watcher pair.\n\n'),
'docs/LOOM_SCRIPT_MAP.md':('Latest Room15','Latest Room17 input,3 October: entry0080 starts200; box4/5 gates001B/0046. Off-picture geometry reached by source353 walking target, not forced placement. '+text+'\n\n'),
'docs/LOOM_CUTSCENE_DISCOVERY.md':('**Latest Room15','**Latest Room17 supplement,3 October:** '+text+'\n\n'),
'docs/CHANGELOG.md':('## 3 October:','## 3 October: verify Room17 watcher input\n\n'+text+'\n\n')}
for name,(marker,addition) in updates.items():
 p=Path(name); old=p.read_text(encoding='utf-8'); assert marker in old and addition not in old
 p.write_text(old.replace(marker,addition+marker,1),encoding='utf-8')
