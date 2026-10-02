"""Copy established source recipes and add read-only per-note observations."""
from pathlib import Path
import json

base = Path(__file__).parent
prior = Path('.verification/native-prerequisites-20261002')
probe = base / 'ProgressionProbe'
probe.mkdir(parents=True, exist_ok=True)
(probe / 'ProgressionProbe.csproj').write_bytes((prior / 'ProgressionProbe/ProgressionProbe.csproj').read_bytes())
text = (prior / 'ProgressionProbe/Program.cs').read_text(encoding='utf-8-sig')
text = text.replace('snapshots.Add(new{stage,frame,estimatedVariables,',
    'snapshots.Add(new{stage,frame,eventCount=((ICollection)typeof(LoomExplorationSession).GetField("_visitEvents",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!).Count,estimatedVariables,')
text = text.replace('int slot=step.GetProperty("slot").GetInt32();',
    'int slot=step.GetProperty("slot").GetInt32();\n                Record($"draft-{slot}-before",0);')
text = text.replace('actions.Add(session.ClickVerbArea(note.X+1,note.Y+1));',
    'actions.Add(session.ClickVerbArea(note.X+1,note.Y+1));\n                    Record($"draft-{slot}-click-note-{i+1}",0);')
text = text.replace('int keySlot=step.GetProperty("slot").GetInt32();',
    'int keySlot=step.GetProperty("slot").GetInt32();\n                Record($"draft-{keySlot}-before",0);')
text = text.replace('actions.Add(session.PressKey("qwertyui"[noteId-1]));',
    'actions.Add(session.PressKey("qwertyui"[noteId-1]));\n                    Record($"draft-{keySlot}-key-note-{i+1}",0);')
assert text.count('Record($"draft-') == 4
(probe / 'Program.cs').write_text(text, encoding='utf-8')

def read(name):
    return json.loads((prior / name).read_text())

native4 = read('room41-level4-skip.json')[0]
native6 = read('room41-controls.json')[1]
recipes = []
for level, source in ((4, native4), (6, native6)):
    for mode in ('draft', 'draft-key'):
        recipe = json.loads(json.dumps(source))
        recipe['id'] = f'room41-raw{level}-' + ('click' if mode == 'draft' else 'key')
        cast = next(a for a in recipe['actions'] if a['kind'] == 'draft' and a['slot'] == 4)
        cast['kind'] = mode
        recipes.append(recipe)
recipes += read('room41-level4-direct.json')
(base / 'room41.json').write_text(json.dumps(recipes, indent=2)+'\n')
for name in ('level4-egg-first.json', 'level4-dye-last.json', 'preserved-recipes.json'):
    (base / name).write_bytes((prior / name).read_bytes())
