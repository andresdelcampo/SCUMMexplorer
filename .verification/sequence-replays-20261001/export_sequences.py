"""Merge audited generic sequences, retaining literal-root gaps and previous titles."""
import collections, json, pathlib, re, sys, hashlib
sys.path.insert(0, str(pathlib.Path('.verification/replay-coverage-20261001').resolve()))
from replay_names import ReplayNames
catalog_path, recipes_path, audit_path, game_path, code_path, report_path = sys.argv[1:]
catalog=json.load(open(catalog_path,encoding='utf-8'))
recipes=json.load(open(recipes_path,encoding='utf-8'))
audit=json.load(open(audit_path,encoding='utf-8'))
assert catalog['sourceFiles']==audit['sourceFiles']
names=ReplayNames(game_path,code_path)
ids={r['id'] for r in recipes}
catalog['replays']=[r for r in catalog['replays'] if r['id'] not in ids]
catalog['membership']=[m for m in catalog['membership'] if m['id'] not in ids]
for recipe,result in zip(recipes,audit['results'],strict=True):
    assert recipe['id']==result['id'] and result['startRoom']==recipe['room']
    boundaries=result['boundaries']
    assert sum(t.startswith('cutscene at ') for t in boundaries)==sum(t.startswith('endCutscene at ') for t in boundaries)
    assert all(any(t.startswith(s['endBoundary']) for t in boundaries) for s in recipe['steps'] if s['kind']=='scene')
    scripts=sorted({int(m[1]) for t in boundaries if (m:=re.match(r'cutscene at Script (\d+) ',t))})
    if recipe['id']=='sequence-086-087':
        title,evidence=names.describe(dict(room=38,objectId=610,verb=54,rooms=result['rooms']))
        recipe['title']='The firewood scene, then '+title[:1].lower()+title[1:]
    else: evidence='Title describes only audited start and watcher rooms.'
    source='Fresh post-boot seed-0 sequence. Catalog arrival, direct verbs and click timing are explicit exploration assumptions. No prerequisite writes. '+evidence+'\n'
    source+='Steps: '+json.dumps(recipe['steps'])+'\nReached scene boundaries:\n'+'\n'.join(boundaries)
    catalog['replays'].append(dict(**recipe,scriptIds=scripts,rooms=result['rooms'],frames=result['frames'],source=source))
    catalog['membership'].append(dict(id=recipe['id'],startRoom=result['startRoom'],rooms=result['rooms'],frames=result['frames'],source='Start plus rooms observed with the shared sequence runner.'))
counts=collections.Counter(r['category'] for r in catalog['replays'])
catalog['summary']=f"{counts['Scenes']+4} scene starts and {counts['Dialogue and reactions']} dialogue/reaction previews, including {len(recipes)} audited sequences. Literal-root gaps are retained; sequence completion does not resolve failed fresh-entry roots or static edges."
pathlib.Path(catalog_path).write_text(json.dumps(catalog,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
compact=dict(auditSha256=hashlib.sha256(pathlib.Path(audit_path).read_bytes()).hexdigest(),results=[{k:v for k,v in r.items() if k not in ('trace','uncertainties')}|dict(uncertaintyCount=len(r['uncertainties'])) for r in audit['results']])
pathlib.Path(report_path).write_text(json.dumps(compact,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(catalog['summary'])
