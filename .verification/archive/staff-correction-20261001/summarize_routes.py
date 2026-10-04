from pathlib import Path
import json, hashlib, sys
packet_name=sys.argv[1] if len(sys.argv)>1 else 'routes-1'
out=Path('Exe/StaffCorrectionSession-20261001')/packet_name
summary=[]
for path in sorted(out.glob('*.json')):
    data=json.loads(path.read_text(encoding='utf-8'))
    stages=[]
    latest={s['stage']:s for s in data['snapshots']}
    for s in latest.values():
        v=s['snapshot']['KnownGlobalVariables']
        stages.append(dict(stage=s['stage'],frame=s['frame'],room=s['snapshot']['RoomId'],variables={k:v.get(k) for k in ('var[0x07A]','var[0x067]','var[0x0CF]','bit[0xDD6]','bit[0xDB4]')},pending=[dict(label=p['label'],offset=p['offset'],freeze=p['freeze']) for p in s['pending']]))
    focused=[t for t in data['trace'] if any(x in t for x in ('findObject at','chains to script 11','chains to script 13','chains to script 26','startObject runs object 633','startObject runs object 634','Script 96','script 96'))]
    item=dict(id=path.stem,actions=data['route']['actions'],prerequisiteStops=[a for a in data['actions'] if 'prerequisiteNotReached' in a],stages=stages,boundaries=data['boundaries'],inputTrace=focused,packet=str(path).replace('\\','/'),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    summary.append(item)
    print(path.stem)
    for s in stages:
        print(s['stage'],s['room'],s['variables'])
    for t in focused: print(t)
Path(f'.verification/archive/staff-correction-20261001/{packet_name}-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
