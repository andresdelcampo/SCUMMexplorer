"""Create one separate response18 experiment; never edit the preserved raw4 recipe."""
import json
from pathlib import Path

source = Path('Exe/Room41RouteSession-20261002/raw4-only.json')
cases = json.loads(source.read_text(encoding='utf-8-sig'))
case = cases[0]
case['id'] = 'room41-sharpening-reverse-full'
actions = case['actions']
index = next(i for i,a in enumerate(actions) if a.get('kind') == 'draft' and a.get('slot') == 4)
assert actions[index+1]['owner'] == 'Room 41 local script 204'
assert actions[index+2] == {'kind':'key','key':27}
actions[index] = {'kind':'draft-key','slot':9,'gap':8,'reverse':True}
del actions[index+1:index+3]
destination = Path('.verification/archive/estimated-room-loads-20261002/response18.json')
assert not destination.exists()
destination.write_text(json.dumps([case],indent=2)+'\n',encoding='utf-8')
print('Created separate slot9 reverse keyboard-input recipe with the full conversation; original untouched.')
