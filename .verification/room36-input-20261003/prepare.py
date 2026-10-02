"""Create a fresh Core-only watcher probe from the established bounded recorder."""
import hashlib
import json
from pathlib import Path

base = Path('.verification/room36-input-20261003')
target = base/'Room36InputProbe'
target.mkdir(exist_ok=False)
source = Path('.verification/room23-input-20261002/Room23InputProbe')
program = (source/'Program.cs').read_text(encoding='utf-8')
program = program.replace('== 23', '== 36').replace('= 23;', '= 36;').replace('= 23)', '= 36)')
begin = program.index('    void WalkBox(')
end = program.index('    string? failure', begin)
program = program[:begin] + '''    void WalkBox(int index, string end)
    {
        var box = s.CurrentWalkBoxes!.Boxes.Single(b => b.Index == index);
        int x = (int)box.Corners.Average(p => p.X), y = (int)box.Corners.Average(p => p.Y);
        int start = s.SessionTrace.Count;
        actions.Add(new { stage = "walk-box-" + index, index, x, y, result = s.Click(x, y) });
        Settle("walk-box-" + index, end, start);
    }
''' + program[end:]
begin = program.index('        WalkBox(3,')
end = program.index('\n    }\n    catch', begin)
program = program[:begin] + '''        WalkBox(3, "endCutscene at Room 36 local script 200 +0x003D");
        WalkBox(4, "endCutscene at Room 36 local script 200 +0x006C");
        WalkBox(9, "endCutscene at Room 36 local script 200 +0x009B");
        WalkBox(10, "endCutscene at Room 36 local script 200 +0x00CA");''' + program[end:]
(target/'Program.cs').write_text(program, encoding='utf-8')
(target/'Room36InputProbe.csproj').write_bytes((source/'Room23InputProbe.csproj').read_bytes())
paths = []
for topic in ('room10-input-20261003','room23-input-20261002','room25-input-20261002','room34-sequences-20261002','sphere-catalog-20261002','sharpening-reverse-20261002'):
    paths.extend(p for p in (Path('.verification')/topic).rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts)
protected = json.loads(Path('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json').read_text(encoding='utf-8-sig'))
paths.extend(map(Path,protected))
for topic in ('Room10Input-20261003','Room23Input-20261002','Room25Input-20261002','Room34Sequences-20261002'):
    paths.extend(p for p in (Path('Exe')/topic).rglob('*') if p.is_file())
(base/'preserved-before.json').write_text(json.dumps({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},indent=2)+'\n',encoding='utf-8')
