from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room20-input-20261003'); target=base/'Room20InputProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/archive/room33-input-20261003/Room33InputProbe')
p=(source/'Program.cs').read_text().replace('== 33','== 16').replace('= 33;','= 16;').replace('expectedRoom = 33','expectedRoom = 16')
p=p.replace('void Cast(int objectId, int verb, string stage, string end)','void Cast(int room, int objectId, int verb, string stage, string end, int destination)')
p=p.replace('s.Snapshot.RoomId != 33','s.Snapshot.RoomId != room').replace('Required Room33 is not current.','Required spell room is not current.').replace('var target = directory.Single(o => o.Id == objectId);','var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == room).FilePath)).Single(o => o.Id == objectId);')
p=p.replace('Settle(stage + "-approach");','Settle(stage + "-approach", expectedRoom: room);').replace('Settle(stage + "-target");','Settle(stage + "-target", expectedRoom: room);').replace('Settle(stage, end, start);','Settle(stage, end, start, destination);')
start=p.index('        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x41);')
end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x47);
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = draft.Notes.Max(), learnedSlot = draft.Slot });
        s.UpdateStaffPossession(true); Settle("staff-ready");
        s.UpdateAvailableNotes(draft.Notes.Max()); Settle("notes-ready");
        s.UpdateDraftLearned(draft.Slot, true); Settle("status-ready");
        Cast(16, 350, 0x47, "sleep", "endCutscene at Room 16 local script 204 +0x0128", 16);
        var exit = directory.Single(o => o.Id == 346);
        actions.Add(new { stage = "enter-room18", target = exit, result = s.Click(exit.X + exit.Width / 2, exit.Y + exit.Height / 2) });
        Settle("enter-room18", expectedRoom: 18);
        var bell = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 18).FilePath)).Single(o => o.Id == 369);
        actions.Add(new { stage = "look-bell", target = bell, result = s.Click(bell.X + bell.Width / 2, bell.Y + bell.Height / 2, doubleClick: true) });
        Settle("enter-room20", expectedRoom: 20);
        Cast(20, 390, 0x48, "wake", "endCutscene at Room 20 local script 206 +0x00A9", 18);
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8'); (target/'Room20InputProbe.csproj').write_bytes((source/'Room33InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room47-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room47Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
