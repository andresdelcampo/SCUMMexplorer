"""Summarize exact pending offsets; classifications are observations, not promotions.
Usage: summarize.py gaps.json code.tsv report.md compact.json
"""
import collections, hashlib, json, pathlib, re, sys
gaps=json.load(open(sys.argv[1],encoding='utf-8'))
source=collections.defaultdict(list)
for line in open(sys.argv[2],encoding='utf-8-sig'):
    cols=line.rstrip('\n').split('\t',3)
    if len(cols)==4:source[cols[0]].append(dict(offset=cols[1],operation=cols[2],operands=cols[3]))
def owner(label):
    return re.sub(r'^Script (\d+)$',lambda m:f'script {int(m[1]):03d}',label).replace(' local script ',' local ').replace(' Object ',' object ')
compact=[]
for r in gaps:
    c=r['checkpoints'][-1]
    pending=[]
    for p in c['pending']:
        if p['freeze']: continue
        key=owner(p['label']);offset=int(p['offset'][3:],16)
        # Small decoded context documents the actual polling loop and its source.
        context=[i for i in source[key] if offset-12<=int(i['offset'][3:],16)<=offset+12]
        pending.append(dict(owner=key,offset=p['offset'],number=p['number'],delay=p['delay'],context=context))
    flag=any(any(i['operation']=='equalZero' and i['operands'].startswith('var[0x0A4]') for i in p['context']) for p in pending)
    sound=any(p['number']==40 for p in pending)
    if flag: kind='Sound completion flag var0A4 remains nonzero'
    elif sound: kind='Following helper waits for script040 sound completion'
    elif not c['InCutscene']: kind='Scope ended/stopped without all matching end boundaries; review required'
    else: kind='Walk-box/script polling; source route or executor review required'
    compact.append(dict(owner=r['owner'],classification=kind,room=c['room'],inCutscene=c['InCutscene'],egoMoving=c['EgoMoving'],sounds=c['sounds'],completionFlag=c['variables'].get('var[0x0A4]'),pending=pending,sceneTrace=r['sceneTrace']))
counts=collections.Counter(r['classification'] for r in compact)
lines=['# Incomplete replay diagnostics, 1 October','',f"45 incomplete roots rerun with fresh seed-0 catalog/direct-verb setup and 3000 frames each. No variable, class, inventory, position or global-script injection. Decoded input SHA256 `{hashlib.sha256(pathlib.Path(sys.argv[2]).read_bytes()).hexdigest()}`.",'',
       'Checkpoint evidence at 0/100/1000/2500/3000 frames; classifications describe reached stalls, not original-game prerequisites. The bound was not extended blindly and no root was promoted. Full snapshots/traces remain ignored in Exe/VerificationSession-20261001/replay-gap-probe-1.','', '| Observed family | Roots |','|---|---|']
lines.extend(f'| {k} | {v} |' for k,v in counts.items())
lines+=['','| Root | Observed family | Live pending sources at frame 3000 |','|---|---|---|']
for r in compact:
    active=[p for p in r['pending'] if p['number'] is None or p['number']>=29]
    lines.append(f"| {r['owner']} | {r['classification']} | "+'; '.join(f"{p['owner']} {p['offset']}" for p in active)+' |')
pathlib.Path(sys.argv[3]).write_text('\n'.join(lines)+'\n',encoding='utf-8')
pathlib.Path(sys.argv[4]).write_text(json.dumps(compact,ensure_ascii=False,indent=2),encoding='utf-8')
print(dict(counts))
