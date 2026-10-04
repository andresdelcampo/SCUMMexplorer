from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room28-input-20261003'); target=base/'Room28InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/archive/room08-input-20261003/Room08InputProbe')
p=(source/'Program.cs').read_text().replace('== 8','== 28').replace('!= 8','!= 28').replace('= 8;','= 28;').replace('= 8)','= 28)').replace('Room08','Room28')
start=p.index('        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x3E);'); end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        var cameraTarget = directory.Single(o => o.Id == 466);
        actions.Add(new { stage = "camera-click", result = s.Click(cameraTarget.X + cameraTarget.Width / 2, cameraTarget.Y + cameraTarget.Height / 2) });
        Settle("camera-click", "endCutscene at Room 28 local script 203 +0x0007");
        var drafts = s.GameStatus.Drafts.Where(d => d.ForwardVerb == 0x43 || d.ReverseVerb == 0x3D).ToArray();
        int noteCount = drafts.Max(d => d.Notes.Max());
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = noteCount, learnedSlots = drafts.Select(d => d.Slot).ToArray() });
        s.UpdateStaffPossession(true); Settle("staff-ready");
        s.UpdateAvailableNotes(noteCount); Settle("notes-ready");
        foreach (var draft in drafts) s.UpdateDraftLearned(draft.Slot, true);
        Settle("status-ready");
        Cast(470, 0x3D, "gold-to-straw", "endCutscene at Room 28 local script 200 +0x0262");
        Cast(466, 0x43, "sleep", "endCutscene at Room 28 local script 210 +0x0099");
        Settle("fire-aftermath", "endCutscene at Script 76 +0x0086");
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8'); (target/'Room28InputProbe.csproj').write_bytes((source/'Room08InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room08-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room08Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
