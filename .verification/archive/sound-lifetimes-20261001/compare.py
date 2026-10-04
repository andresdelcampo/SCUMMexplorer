import collections, json, pathlib, sys
from PIL import Image

old = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8'))
new = json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8'))
before = {r['owner']:r for r in old['results']}
changed = [dict(owner=r['owner'],before=before[r['owner']]['status'],after=r['status'],frames=r['frames'],sceneTrace=r['sceneTrace'])
    for r in new['results'] if before[r['owner']]['status'] != r['status']]
regressions = [r for r in changed if r['before']=='Validated state preview']
print('Status counts:',dict(collections.Counter(r['status'] for r in new['results'])))
print('Changed:',len(changed),'Regressions:',len(regressions))
pathlib.Path(sys.argv[3]).write_text(json.dumps(changed,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
pixel_changes = []
for bmp in pathlib.Path(sys.argv[4]).glob('room-*.bmp'):
    png = pathlib.Path('.verification/room-audit')/(bmp.stem+'.png')
    with Image.open(bmp) as a, Image.open(png) as b:
        if a.size != b.size or a.convert('RGB').tobytes() != b.convert('RGB').tobytes(): pixel_changes.append(bmp.name)
print('Room pixel changes:',pixel_changes)
assert not regressions
