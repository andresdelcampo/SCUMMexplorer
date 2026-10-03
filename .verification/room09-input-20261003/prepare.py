from pathlib import Path
import hashlib,json
base=Path('.verification/room09-input-20261003'); target=base/'Room09InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/room10-input-20261003/Room10InputProbe')
p=(source/'Program.cs').read_text().replace('== 10','== 50').replace('= 10;','= 50;').replace('= 10)','= 9)').replace('!= 10','!= 9').replace('Room10','Room09')
p=p.replace('var target = directory.Single(o => o.Id == 296);','var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 9).FilePath)).Single(o => o.Id == 273);').replace('ActivateObjectVerb(296','ActivateObjectVerb(273')
start=p.index('    void WalkBox('); end=p.index('    void Cast(',start); p=p[:start]+p[end:]
start=p.index('        Settle("arrival");'); end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        Settle("arrival", expectedRoom: 50);
        ExitClick(50, 721, 7, "leave-room50");
        ExitClick(7, 252, 9, "enter-room9");
        Settle("late-conversation", "endCutscene at Script 101 +0x08B3");
        var drafts = s.GameStatus.Drafts.Where(d => d.ReverseVerb is 0x4D or 0x4F or 0x51).ToArray();
        if (drafts.Length != 3) throw new InvalidOperationException("Expected three source draft slots.");
        int noteCount = drafts.Max(d => d.Notes.Max());
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = noteCount, learnedSlots = drafts.Select(d => d.Slot).ToArray() });
        s.UpdateStaffPossession(true); Settle("staff-ready");
        s.UpdateAvailableNotes(noteCount); Settle("notes-ready");
        foreach (var draft in drafts) s.UpdateDraftLearned(draft.Slot, true);
        Settle("status-ready");
        Cast(0x4D, "healing-refusal", "endCutscene at Room 09 Object 273 Verb 0x4D +0x0076");
        Cast(0x4F, "unsilence", "endCutscene at Room 09 local script 223 +0x02C4");
        Cast(0x51, "unmake", "endCutscene at Room 09 local script 224 +0x041B");
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8')
(target/'Room09InputProbe.csproj').write_bytes((source/'Room10InputProbe.csproj').read_bytes())
prior=Path('.verification/room22-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room22Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
