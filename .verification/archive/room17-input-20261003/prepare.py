from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room17-input-20261003'); target=base/'Room17InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/archive/room15-input-20261003/Room15InputProbe')
p=(source/'Program.cs').read_text().replace('== 15','== 17').replace('= 15;','= 17;').replace('= 15)','= 17)').replace('Room 15','Room 17')
p=p.replace('WalkBox(3,','WalkBox(4,').replace('+0x0062','+0x0042').replace('+0x00C4','+0x006D')
(target/'Program.cs').write_text(p,encoding='utf-8')
(target/'Room17InputProbe.csproj').write_bytes((source/'Room15InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room15-input-20261003')
paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room15Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text())
preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
