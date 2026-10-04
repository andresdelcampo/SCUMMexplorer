import json,pathlib,sys
old=json.load(open('src/SCUMMexplorer.App/loom-replays.json',encoding='utf-8'))
new=json.load(open(sys.argv[1],encoding='utf-8'))
previous={r['id']:r for r in old['replays'] if 'steps' not in r}; updated={r['id']:r for r in new['replays']}
assert old['sourceFiles']==new['sourceFiles']
assert all(k in updated and r['title']==updated[k]['title'] and r['rooms']==updated[k]['rooms'] for k,r in previous.items())
pathlib.Path('src/SCUMMexplorer.App/loom-replays.json').write_text(json.dumps(new,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Previous literal titles and room memberships preserved; only three new starts added.')
