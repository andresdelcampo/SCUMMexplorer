from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room08-input-20261003'); target=base/'Room08InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/archive/room24-input-20261003/Room24InputProbe')
p=(source/'Program.cs').read_text().replace('== 24','== 8').replace('!= 24','!= 8').replace('= 24;','= 8;').replace('= 24)','= 8)').replace('Room24','Room08')
start=p.index('    void WalkBox('); end=p.index('    void Cast(',start); p=p[:start]+p[end:]
p=p.replace('void Cast(int verb,','void Cast(int objectId, int verb,').replace('o.Id == 432','o.Id == objectId').replace('ActivateObjectVerb(432','ActivateObjectVerb(objectId')
start=p.index('        WalkBox('); end=p.index('        var draft',start); p=p[:start]+p[end:]
start=p.index('        if (s.Snapshot.KnownGlobalVariables'); end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        Cast(256, 0x3F, "bleach-left", "endCutscene at Room 08 local script 212 +0x0062");
        Cast(256, 0x3E, "restore-left", "endCutscene at Room 08 local script 211 +0x0066");
        Cast(257, 0x3E, "dye-right", "endCutscene at Room 08 local script 213 +0x0066");
        Cast(257, 0x3F, "restore-right", "endCutscene at Room 08 local script 214 +0x0062");
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8'); (target/'Room08InputProbe.csproj').write_bytes((source/'Room24InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room09-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room09Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
