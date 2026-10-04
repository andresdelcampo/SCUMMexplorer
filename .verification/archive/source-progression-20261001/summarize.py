from pathlib import Path
import hashlib
import json
import sys

folder = Path(sys.argv[1])
summary = []
for path in sorted(folder.glob('*.json')):
    data = json.loads(path.read_text(encoding='utf-8'))
    stages = []
    for snap in {s['stage']: s for s in data['snapshots']}.values():
        state = snap['snapshot']
        variables = state['KnownGlobalVariables']
        stages.append(dict(stage=snap['stage'], frames=snap['frame'], room=state['RoomId'],
            inCutscene=snap['InCutscene'],variables={key:variables.get(key) for key in
                ('var[0x07A]','var[0x067]','var[0x0CF]','bit[0xD6B]','bit[0xD6F]','bit[0xD7B]','bit[0xDA3]','bit[0xDD5]','bit[0xDD7]','bit[0xD66]','bit[0xDB4]')},
            estimatedVariables=[v for v in snap.get('estimatedVariables',[]) if v in ('var[0x07A]','bit[0xD7B]','bit[0xDA3]','bit[0xD61]')],
            classes={key:state['ObjectClasses'].get(key) for key in ('632','633','634')},
            status={k:snap.get('status',{}).get(k) for k in ('StaffHeld','StaffOrigin','AvailableNoteCount','NotesOrigin')},
            actors=[{k:a.get(k) for k in ('Number','Room','X','Y','Moving','WalkBox','Estimate')} for a in snap['actors'] if a['Number'] in (1,4)],
            pending=[dict(label=p['label'],offset=p['offset'],freeze=p['freeze']) for p in snap['pending']]))
    item=dict(id=path.stem, actions=data['route']['actions'],stages=stages,
        prerequisiteStops=[a for a in data['actions'] if 'prerequisiteNotReached' in a or 'missingTouchable' in a],
        boundaries=data['boundaries'],balancedScopes=sum(t.startswith('cutscene at ') for t in data['boundaries'])==sum(t.startswith('endCutscene at ') for t in data['boundaries']),roomChanges=data.get('roomChanges'),
        failure=data.get('failure'),
        focusedTrace=list(dict.fromkeys(t for t in data['trace'] if any(s in t for s in
            ('runs object 325','runs object 632','runs object 633','chains to script 11','chains to script 12','chains to script 26','script 29','0x07A','only possible')))),
        packet=str(path).replace('\\','/'),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    summary.append(item)
    print(item['id'],item['prerequisiteStops'])
    for s in stages:
        print(s['stage'],s['frames'],s['room'],s['inCutscene'],s['variables'],s['classes'])
    for t in item['focusedTrace'][-12:]: print(t)
Path('.verification/archive/source-progression-20261001',folder.name+'-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
