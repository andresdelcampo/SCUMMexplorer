from pathlib import Path
text='Room34 watcher prerequisite investigation stopped after two failed cycles: fresh38 Look610 reproduces incomplete087 in37; fresh36 exit599 is hidden by class24 and never dispatches. Four failed captures retained/excluded; no coverage gain or production defect established. Next source step is Room36 box9 watcher200 +0077/end009B, which clears599 class24 at+0094. Audited215/235 (91.49%),20 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. Protected57/history/App untouched. [Validation and precise next experiment](../.verification/archive/room34-watcher-20261003/validation.md).'
updates={
'docs/NEXT_SESSION_PROMPT.md':('## Latest:', '''## Parked by user: Room34 prerequisites retained — 3 October

After410b76c. '''+text+'''

The user explicitly parked this session after at least two compactions. Do not continue runtime work in this session. Session gain from84da7df is23 audited sites (192→215); fresh single-action coverage remains169. Completed Room36/24/15/17/22/08/28/62/33/47/20 routes are committed. Room09 and Room34 watcher investigations are retained failures with no credit. Exact context/token telemetry remains unavailable; no claim of hitting200K. User prefers sustained successive bounded audits in future sessions, committing each, until a context checkpoint or explicit stop.

Next session, start a fresh task folder. Inspect the Room34 watcher validation and the successful Room36 input validation. From fresh36, actual box9 click349,99 must complete200 +0077/end009B and remove599 class24; then actual599→37, firewood109,102→086/end055C in38, actual610 Look→087/end09B9. Follow source Opening617 and continuous exits609→37,601→36,598→35,587→34, Healing586 and camera watcher prerequisites. Do not repeat the bare hidden599 click or fresh38/087 attempt. This is a candidate requiring complete verification, not a proven recipe. Room34 local207 +0079 remains a separate sourceD9D/DC7-dependent candidate. Later Room09: fix the probe selection to match ForwardVerb OR ReverseVerb (4D is forward-only), then rerun its complete source route; do not credit the earlier failed prefix101.

Preserve all historical constraints and all uncertainty/value/index/path estimates. Supported labelled staff/notes/learned setup remains authorized; source notes/reverse flags unchanged. No story flags, class/box/actor placement injections or fabricated private-script controls. Spell routes need actual keys, accepted dispatch and complete settled bodies against paired direct controls. At most two diagnostic cycles per route without a route-relevant implementation decision; retain failures and commit each bounded task. No App launch/close/interaction/rebuild or push. Core662/broad/main Release remain dated. Keep unrelated dirty files untouched.

Ready-to-copy prompt:

> Continue SCUMMexplorer from the latest parked checkpoint using docs/NEXT_SESSION_PROMPT.md. Start with the Room34 watcher prerequisite: Room36 box9 input opens exit599 before the source086→087 route. Read the retained failures and successful Room36 evidence first; do not repeat either known failed shortcut. Preserve all constraints and uncertainty labels. Keep215/235 audited distinct from169/235 fresh single-action coverage. Work through successive bounded audits, commit each completed checkpoint, and prepare a handoff before repeated compaction; exact200K telemetry is unavailable. Leave the running App and unrelated dirty files untouched. No push.

'''),
'docs/LOOM_EGA_PLAN.md':('**Latest Room20','**Parked by user,3 October:** '+text+' Session gain23 audited sites from192 to215; exact200K threshold not measured.\n\n'),
'docs/LOOM_SCRIPT_MAP.md':('Latest Room20','Latest parked investigation,3 October:36/200 +0094 clears599 class24; entry36 +003C sets it when arriving from the default catalog route. '+text+'\n\n'),
'docs/LOOM_CUTSCENE_DISCOVERY.md':('**Latest Room20','**Parked supplement,3 October:** '+text+'\n\n'),
'docs/CHANGELOG.md':('## 3 October:','## 3 October: park after Room34 prerequisite investigation\n\n'+text+' User explicitly requested parking and a next prompt after compactions; session gain23 audited sites from84da7df. No exact200K claim.\n\n')}
for name,(marker,addition) in updates.items():
 p=Path(name); old=p.read_text(encoding='utf-8'); assert marker in old and addition not in old
 p.write_text(old.replace(marker,addition+marker,1),encoding='utf-8')
