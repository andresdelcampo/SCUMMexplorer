import collections, json, pathlib, sys

catalog, audit, roots, established, old = [json.loads(pathlib.Path(p).read_text(encoding='utf-8')) for p in sys.argv[1:6]]
assert catalog['sourceFiles'] == audit['sourceFiles'] == established['sourceFiles']
assert roots['inputSha256'] == audit['inputSha256']
originals = {r['id']:r for r in established['results']}
expected = {r['owner']:r for r in audit['results'] if r['status']=='Validated state preview' and r['owner'] not in originals}
recipes = {r['id']:r for r in catalog['replays']}
assert recipes.keys() == expected.keys()
members = {m['id']:m for m in catalog['membership']}
assert members.keys() == recipes.keys() | originals.keys()
for key, recipe in recipes.items():
    probe = expected[key]
    assert recipe['frames'] == probe['frames']
    assert recipe['rooms'] == probe['rooms']
    assert members[key]['startRoom'] == probe['room']
    assert set(members[key]['rooms']) == set(probe['rooms']) | {probe['room']}
for key, result in originals.items():
    assert members[key]['rooms'] == result['rooms'] and members[key]['frames'] == result['frames']
old_recipes = {r['id']:r for r in old['replays']}
assert old_recipes.keys() <= recipes.keys()
assert all(recipes[key]['title']==r['title'] for key,r in old_recipes.items())
assert not any('Object ' in r['title'] for r in recipes.values())
assert len(catalog['coverage'])==55
assert any(c['title']=='script 053' for c in catalog['coverage'])
assert any(c['title']=='script 087' for c in catalog['coverage'])
changes = [r for key,r in recipes.items() if key not in old_recipes]
pathlib.Path(sys.argv[6]).write_text(json.dumps(changes,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('PASS',len(members),'memberships;',len(changes),'new recipes; all old names retained; exact file/input fingerprints')
print(collections.Counter(r['category'] for r in changes))
print('Category counts:',collections.Counter(r['category'] for r in recipes.values()))
