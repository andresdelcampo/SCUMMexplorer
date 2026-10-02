from pathlib import Path
import hashlib,json
base=Path('.verification/room22-input-20261003'); target=base/'Room22InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/room15-input-20261003/Room15InputProbe')
p=(source/'Program.cs').read_text().replace('== 15','== 22').replace('= 15;','= 22;').replace('= 15)','= 22)').replace('Room 15','Room 22')
p=p.replace('WalkBox(3,','WalkBox(10,').replace('WalkBox(5,','WalkBox(11,').replace('+0x0062','+0x0078').replace('+0x00C4','+0x00D3')
(target/'Program.cs').write_text(p,encoding='utf-8')
(target/'Room22InputProbe.csproj').write_bytes((source/'Room15InputProbe.csproj').read_bytes())
prior=Path('.verification/room17-input-20261003')
paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room17Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text())
preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
