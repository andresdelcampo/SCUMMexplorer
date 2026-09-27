# For each room that shows the "first stored image" fallback (and Room 58),
# lists its object records and every decoded instruction whose operands name
# one of those objects as a literal. Input: code.tsv and objects.tsv from CodeDump.
import csv, re, sys
# The 16 rooms that used the first-stored-image fallback on 27 September, plus Room 58.
rooms = [1,3,4,8,9,16,18,20,23,25,36,37,41,42,44,64,58]
objs = {}
for r in csv.DictReader(open('objects.tsv'), delimiter='\t'):
    objs.setdefault(int(r['room']), []).append(r)
code = [l.rstrip('\n').split('\t') for l in open('code.tsv', encoding='utf-8')]
lit = re.compile(r'0x([0-9A-F]+)')
for room in rooms:
    ids = {int(o['id']) for o in objs.get(room, [])}
    print(f"== Room {room:02}")
    for o in objs[room]:
        img = f"image ({o['x']},{o['y']}) {o['w']}x{o['h']}" if o['hasImage']=='True' else "no image"
        par = f" parent local {o['parentLocal']} state {o['parentState']}" if o['parentLocal']!='0' else ""
        print(f"   object {o['id']} local {o['localIndex']} {img}{par}")
    for src, off, op, operands in (c + ['']*(4-len(c)) for c in code):
        if op in ('print','setObjectName','verbOps','stringOps','DECODE-FAILURE'): continue
        parts = [p.strip() for p in operands.split(',')]
        if op in ('setState','drawObject','setClass','ifClassOfIs','startObject'): cand = parts[:1]
        elif op in ('putActorAtObject','walkActorToObject'): cand = parts[1:2]
        elif op == 'startScript': cand = re.findall(r'0x[0-9A-F]+', operands.split('[',1)[1]) if '[' in operands else []
        else: cand = []
        vals = {int(v,16) for v in cand if re.fullmatch(r'0x[0-9A-F]+', v)}
        hit = vals & ids
        if hit and op not in ('isEqual','isNotEqual','isLess','isGreater','isLessEqual','isGreaterEqual','move','add','subtract','increment','decrement','breakHere','delay','setVarRange','walkActorTo','putActor','animateActor','actorOps','jumpRelative','equalZero','notEqualZero','startSound','resourceRoutines','roomOps','setBoxFlags','lights','cursorCommand','setCameraAt','panCameraTo','getRandomNr','loadRoom','loadRoomWithEgo','putActorInRoom','waitFor','stopScript','chainScript','isScriptRunning','delayVariable','oldRoomEffect','faceActor','drawBox'):
            print(f"   {src} {off} {op} {operands}   [{','.join(map(str,sorted(hit)))}]")
