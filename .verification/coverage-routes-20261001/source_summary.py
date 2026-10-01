import collections, json, pathlib, re, sys

source=collections.defaultdict(list)
for line in pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig').splitlines():
    fields=line.split('\t',3)
    if len(fields)==4: source[fields[0]].append(dict(offset=fields[1],operation=fields[2],operands=fields[3]))
roots=json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8'))
catalog=json.loads(pathlib.Path(sys.argv[3]).read_text(encoding='utf-8'))
outstanding={r['title']:r for r in catalog['coverage']}
inventory=[]
audit=json.loads(pathlib.Path(sys.argv[5]).read_text(encoding='utf-8'))
statuses={r['owner']:r['status'] for r in audit['results']}
no_completed=[]
for candidate in roots['candidateOwners']:
    if candidate['owner'] not in outstanding: continue
    if not candidate['roots']:
        inventory.append(dict(owner=candidate['owner'],coverage=outstanding[candidate['owner']],sites=candidate['sites']))
    elif not any(statuses[root]=='Validated state preview' for root in candidate['roots']):
        no_completed.append(dict(owner=candidate['owner'],coverage=outstanding[candidate['owner']],sites=candidate['sites'],
            roots=[dict(owner=root,status=statuses[root]) for root in candidate['roots']]))
pathlib.Path(sys.argv[4]).write_text(json.dumps(dict(noLiteralRoots=inventory,noCompletedLiteralRoots=no_completed,unresolved=roots['unresolved']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for candidate in inventory:
    print(candidate['owner'],candidate['coverage']['status'])
    if match:=re.fullmatch(r'script (\d+)',candidate['owner']):
        target=int(match[1])
        for owner,rows in source.items():
            for row in rows:
                if row['operation'] in ('startScript','chainScript') and row['operands'].split(',',1)[0]==f'0x{target:02X}':
                    print(' ',owner,row)
print('Candidate owners with roots but no completed literal preview:')
for candidate in no_completed:
    print(candidate['owner'],candidate['roots'])
