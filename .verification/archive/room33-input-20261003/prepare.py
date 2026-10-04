from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room33-input-20261003'); target=base/'Room33InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/archive/room62-input-20261003/Room62InputProbe')
p=(source/'Program.cs').read_text().replace('== 62','== 33').replace('!= 62','!= 33').replace('= 62;','= 33;').replace('= 62)','= 33)').replace('Room62','Room33').replace('d.ForwardVerb == 0x3C','d.ForwardVerb == 0x41')
start=p.index('        Cast(808,'); end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        Cast(544, 0x42, "untwist", "endCutscene at Room 33 local script 200 +0x005E");
        Cast(544, 0x41, "twist", "endCutscene at Room 33 local script 202 +0x005E");
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8'); (target/'Room33InputProbe.csproj').write_bytes((source/'Room62InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room62-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room62Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
