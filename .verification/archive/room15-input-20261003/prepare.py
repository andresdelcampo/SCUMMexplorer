from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room15-input-20261003'); target=base/'Room15InputProbe'
target.mkdir(exist_ok=False)
source=Path('.verification/archive/room36-input-20261003/Room36InputProbe')
p=(source/'Program.cs').read_text().replace('== 36','== 15').replace('= 36;','= 15;').replace('= 36)','= 15)')
begin=p.index('        // Arrival'); end=p.index('\n    }\n    catch',begin)
p=p[:begin]+'''        WalkBox(3, "endCutscene at Room 15 local script 200 +0x0062");
        WalkBox(5, "endCutscene at Room 15 local script 200 +0x00C4");'''+p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8')
(target/'Room15InputProbe.csproj').write_bytes((source/'Room36InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room24-input-20261003')
paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room24Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text())
preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
