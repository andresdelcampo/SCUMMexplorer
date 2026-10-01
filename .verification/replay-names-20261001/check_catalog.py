import json, pathlib, subprocess
old=json.loads(subprocess.check_output(['git','show','HEAD:src/SCUMMexplorer.App/loom-replays.json']))
new=json.load(open('src/SCUMMexplorer.App/loom-replays.json',encoding='utf-8'))
assert old['sourceFiles']==new['sourceFiles'] and old['coverage']==new['coverage'] and old['summary']==new['summary']
assert len(old['replays'])==len(new['replays'])==287
for a,b in zip(old['replays'],new['replays']):
    assert {k:v for k,v in a.items() if k not in ('title','source')}=={k:v for k,v in b.items() if k not in ('title','source')}
    assert b['source'].startswith(a['source']) and 'Object ' not in b['title']
gaps=[r for r in new['replays'] if 'Naming gap:' in r['source']]
assert len(gaps)==39
path=pathlib.Path('.verification/replay-names-20261001/naming-gaps.md')
path.write_text('# Source naming gaps\n\nAll supplied room definitions and decoded literal setObjectName operands reviewed. No useful source name was found for these 39 targets. Descriptors use decoded verbs and validated reached rooms; source IDs remain here and in picker details. Conditional renames for other objects are recorded, not treated as reached names.\n\n| Source root | Shared title |\n|---|---|\n'+''.join(f"| {r['id']} | {r['title']} |\n" for r in gaps),encoding='utf-8')
print('PASS 287 recipes, fingerprints, coverage gaps, script IDs, room evidence, frame counts and actions unchanged; 39 explicit naming gaps')
