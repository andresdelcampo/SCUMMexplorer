from pathlib import Path
import hashlib,json
base=Path('.verification/room24-input-20261003')
target=base/'Room24InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/room10-input-20261003/Room10InputProbe')
p=(source/'Program.cs').read_text(encoding='utf-8').replace('== 10','== 24').replace('!= 10','!= 24').replace('= 10;','= 24;').replace('= 10)','= 24)').replace('Room10','Room24').replace('== 296','== 432').replace('ActivateObjectVerb(296','ActivateObjectVerb(432')
begin=p.index('    void ExitClick('); end=p.index('    string? failure',begin)
p=p[:begin]+p[end:]
begin=p.index('        WalkBox(3,'); end=p.index('\n    }\n    catch',begin)
p=p[:begin]+'''        WalkBox(3, "endCutscene at Room 24 local script 200 +0x007E");
        WalkBox(4, "endCutscene at Room 24 local script 200 +0x00D6");
        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x3E);
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = draft.Notes.Max(), learnedSlot = draft.Slot });
        s.UpdateStaffPossession(true);
        Settle("staff-ready");
        s.UpdateAvailableNotes(draft.Notes.Max());
        Settle("notes-ready");
        s.UpdateDraftLearned(draft.Slot, true);
        Settle("status-ready");
        if (s.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x0C5]") == 444)
            Cast(0x3E, "dye", "endCutscene at Room 24 local script 213 +0x0043");
        Cast(0x3E, "already-green", "endCutscene at Room 24 local script 211 +0x0045");
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8')
(target/'Room24InputProbe.csproj').write_bytes((source/'Room10InputProbe.csproj').read_bytes())
prior=Path('.verification/room36-input-20261003')
paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room36Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text())
preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
