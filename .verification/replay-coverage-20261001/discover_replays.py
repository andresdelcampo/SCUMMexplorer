"""Resolve literal call scopes from decoded resources. Static edges are possibilities.
python discover_replays.py code.tsv roots.json
The audit must then validate the exact catalog/direct-verb recipe; no state writes.
"""
import collections, hashlib, json, re, sys
owners = collections.defaultdict(list)
for line in open(sys.argv[1], encoding='utf-8-sig'):
    cols = line.rstrip('\n').split('\t', 3)
    if len(cols) == 4:
        owners[cols[0]].append(dict(offset=cols[1], operation=cols[2], operands=cols[3]))
edges = collections.defaultdict(list)
unresolved = []
for owner, rows in owners.items():
    room = re.match(r'Room (\d+)', owner)
    for row in rows:
        if row['operation'] not in ('startScript', 'chainScript'): continue
        literal = re.match(r'^(0x[\dA-Fa-f]+|\d+)(?:\s*,|$)', row['operands'])
        if not literal:
            unresolved.append(dict(owner=owner, **row)); continue
        number = int(literal[1], 0)
        # The dump indexes globals from 00.LFL's declared script directory and
        # locals from each room using that declared boundary, not a fixed ID.
        global_owner = f'script {number:03d}'
        target = global_owner if global_owner in owners else f'Room {room[1]} local {number}' if room else None
        if target in owners: edges[owner].append(target)
        else: unresolved.append(dict(owner=owner, **row))

def closure(owner):
    queue, seen = [owner], set()
    while queue:
        key = queue.pop()
        if key in seen: continue
        seen.add(key); queue += edges[key]
    return sorted(seen)

sites = {k: [r for r in rows if r['operation'] == 'cutscene'] for k, rows in owners.items()}
sites = {k: v for k,v in sites.items() if v}
roots = []
for owner in owners:
    match = re.fullmatch(r'Room (\d+) object (\d+) Verb 0x([\dA-Fa-f]+)', owner)
    entry = re.fullmatch(r'Room (\d+) entry', owner)
    if not match and not entry: continue
    reachable = [k for k in closure(owner) if k in sites]
    if not reachable: continue
    roots.append(dict(owner=owner, room=int((match or entry)[1]), objectId=int(match[2]) if match else 0,
        verb=int(match[3],16) if match else 0, candidates=reachable,
        source=[dict(owner=k, **r) for k in reachable for r in sites[k]]))
result = dict(inputSha256=hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest(), roots=roots,
    candidateOwners=[dict(owner=k, sites=v, roots=[r['owner'] for r in roots if k in r['candidates']]) for k,v in sites.items()],
    unresolved=unresolved)
with open(sys.argv[2],'w',encoding='utf-8') as f: json.dump(result,f,indent=2,ensure_ascii=False)
print(f'{len(roots)} literal object/entry recipes to probe; {len(sites)} candidate owners; {len(unresolved)} unresolved edges')
