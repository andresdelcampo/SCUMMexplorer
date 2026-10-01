from pathlib import Path
import json
import sys

data=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
print('boundaries',data['boundaries'])
for action in data['actions']:
    if 'directory' not in action:
        print('action', {k:action.get(k) for k in ('SourceRoomId','ObjectId','VerbId','Input','Evidence')})
for t in data['trace']:
    if any(s in t for s in ('Script 10','Script 22','Script 26','Script 96','Object 639','only possible','seeding')):
        print(t)
last=data['snapshots'][-1]
print('status keys',list(last.get('status',{})))
for actor in last['actors']:
    if actor['Number'] in (1,4):
        print('actor', {key:actor.get(key) for key in ('Number','Room','X','Y','Moving','WalkBox','Estimate')})
for action in data['actions']:
    if 'directory' in action:
        print('objects', [{key:o.get(key) for key in ('Id','X','Y','Width','Height','WalkX','WalkY')} for o in action['directory'] if o['Id'] in (632,633,634,325)])
print('uncertainties',*data['uncertainties'][-15:],sep='\n')
