from pathlib import Path
import hashlib, json, sys

summaries=[]
for directory in sys.argv[1:]:
    for path in sorted(Path(directory).glob('*.json')):
        p=json.loads(path.read_text(encoding='utf-8'))
        if 'snapshots' not in p: continue
        stages={}
        for s in p['snapshots']:
            snap=s['snapshot']; variables=snap['KnownGlobalVariables']
            stages[s['stage']]=dict(frame=s['frame'],room=snap['RoomId'],estimated=s['estimatedVariables'],
                variables={k:variables.get(k) for k in ('var[0x07A]','bit[0xD6F]','bit[0xD6B]','bit[0xD8F]','bit[0xD7B]','bit[0xDA3]','var[0x052]','var[0x053]','var[0x054]','var[0x055]')},
                drafts=[dict(slot=d['Slot'],packed=d['Packed'],learned=d['Learned'],used=bool(d['Packed']&0x4000)) for d in s['status']['Drafts'][:5]],
                ego=next((a for a in s['actors'] if a['Number']==1),None),pending=s['pending'])
        summary=dict(id=p['route']['id'],recipe=p['route'],stages=stages,elapsed=p['elapsed'],roomChanges=p['roomChanges'],
            stops=[a for a in p['actions'] if any(k in a for k in ('prerequisiteNotReached','missingScript','missingOverride','missingTouchable'))],boundaries=p['boundaries'],
            events=[{k:e.get(k) for k in ('SourceLabel','Offset','Operation','Certainty','TargetId','ResolvedArguments','EstimatedArgumentIndexes')} for e in p.get('events',[]) if e['Operation'] not in ('print','setState') and any(t in e['SourceLabel'] for t in ('Script 26','Script 34','Script 90','Script 96','270 Verb 0x3A','Room 20 local script 206','Room 69 local script 203','Room 69 local script 205'))],
            certainty=[u for u in p['uncertainties'] if any(term in u for term in ('possible Room','not modeled','override','getActorWalkBox reads actor 4'))],
            packet=path.as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        summaries.append(summary)
        print(summary['id'],[(n,s['frame'],s['room']) for n,s in stages.items()],summary['stops'])
Path('.verification/archive/source-progression-20261002/route-summary.json').write_text(json.dumps(summaries,indent=2)+'\n',encoding='utf-8')
