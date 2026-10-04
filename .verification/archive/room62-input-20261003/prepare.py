from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room62-input-20261003'); target=base/'Room62InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/archive/room08-input-20261003/Room08InputProbe')
p=(source/'Program.cs').read_text().replace('== 8','== 62').replace('!= 8','!= 62').replace('= 8;','= 62;').replace('= 8)','= 62)').replace('Room08','Room62').replace('d.ForwardVerb == 0x3E','d.ForwardVerb == 0x3C')
start=p.index('        Cast(256,'); end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        Cast(808, 0x3D, "left-to-straw", "endCutscene at Room 62 local script 201 +0x007B");
        Cast(808, 0x3C, "left-to-gold", "endCutscene at Room 62 local script 202 +0x007B");
        Cast(809, 0x3D, "right-to-straw", "endCutscene at Room 62 local script 204 +0x007B");
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8'); (target/'Room62InputProbe.csproj').write_bytes((source/'Room08InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room28-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room28Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
