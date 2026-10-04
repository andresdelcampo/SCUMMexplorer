import hashlib, json, pathlib, sys

folder=pathlib.Path(sys.argv[1])
result=[]
for file in sorted(folder.glob('*.json')):
    data=json.loads(file.read_text(encoding='utf-8'))
    stages={}
    for snap in data['snapshots']:
        stages[snap['stage']]=dict(frame=snap['frame'],room=snap['snapshot']['RoomId'],inCutscene=snap['InCutscene'],
            knownBits={k:v for k,v in snap['snapshot']['KnownGlobalVariables'].items() if k in ('bit[0xD61]','bit[0xD7B]','bit[0xDA3]','bit[0xD66]','bit[0xD67]')},
            pending=[dict(label=p['label'],offset=p['offset'],freeze=p['freeze'],delay=p['delay'],locals={k:v for k,v in p['locals'].items() if v}) for p in snap['pending']])
    unique=list(dict.fromkeys(data['uncertainties']))
    # This is a focused route digest; the unchanged full diagnostic packet is retained
    # under ignored Exe with its hash, including all boot/presentation uncertainties.
    focused=[u for u in unique if any(t in u for t in ('possible Room','untouchable','activated directly','assumed input','Player input assumed','getActorWalkBox','walkActorToObject'))]
    result.append(dict(id=data['route']['id'],actions=data['route']['actions'],stages=stages,boundaries=data['boundaries'],
        loads=[t for t in data['trace'] if 'loadRoom' in t],focusedUncertainties=focused,totalUncertaintyCount=len(data['uncertainties']),
        uniqueUncertaintyCount=len(unique),fullPacket=file.as_posix(),fullPacketSha256=hashlib.sha256(file.read_bytes()).hexdigest()))
pathlib.Path(sys.argv[2]).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for route in result:
    print(route['id'],route['boundaries'])
    print({s:(v['frame'],v['room'],v['inCutscene']) for s,v in route['stages'].items()})
