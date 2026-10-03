from pathlib import Path
import hashlib,json
base=Path('.verification/room47-input-20261003'); target=base/'Room47InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/room09-input-20261003/Room09InputProbe')
p=(source/'Program.cs').read_text().replace('== 50','== 47').replace('= 50;','= 47;').replace('expectedRoom = 9','expectedRoom = 47')
start=p.index('    void Cast('); end=p.index('    void ExitClick',start)
p=p[:start]+p[end:]
start=p.index('        Settle("arrival"'); end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        Settle("arrival", expectedRoom: 47);
        var cygna = directory.Single(o => o.Id == 697);
        int conversationStart = s.SessionTrace.Count;
        if (mode == "direct") actions.Add(s.ActivateObjectVerb(697, 0x36));
        else actions.Add(new { stage = "look-cygna", target = cygna, result = s.Click(cygna.X + cygna.Width / 2, cygna.Y + cygna.Height / 2, doubleClick: true) });
        Settle("conversation", "endCutscene at Script 100 +0x0A7B", conversationStart);
        ExitClick(47, 696, 46, "leave-room47");
        int returnStart = s.SessionTrace.Count;
        ExitClick(46, 681, 47, "return-room47");
        Settle("return-response", "endCutscene at Room 47 local script 205 +0x03F1", returnStart);
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8'); (target/'Room47InputProbe.csproj').write_bytes((source/'Room09InputProbe.csproj').read_bytes())
prior=Path('.verification/room33-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room33Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
