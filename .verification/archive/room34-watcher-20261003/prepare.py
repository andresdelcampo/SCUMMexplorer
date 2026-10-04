from pathlib import Path
import hashlib,json
base=Path('.verification/archive/room34-watcher-20261003'); target=base/'Room34WatcherProbe'; target.mkdir(exist_ok=False)
source=Path('.verification/archive/room20-input-20261003/Room20InputProbe')
p=(source/'Program.cs').read_text().replace('== 16','== 38').replace('= 16;','= 38;').replace('expectedRoom = 16','expectedRoom = 38')
start=p.index('    string? failure = null;')
p=p[:start]+'''    void ExitClick(int room, int id, int destination)
    {
        if (s.Snapshot.RoomId != room) throw new InvalidOperationException("Unexpected exit start room.");
        var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == room).FilePath)).Single(o => o.Id == id);
        actions.Add(new { stage = "exit-" + id, room, id, destination, target, result = s.Click(target.X + target.Width / 2, target.Y + target.Height / 2) });
        Settle("exit-" + id, expectedRoom: destination);
    }
    void WalkBox(int index, string? end = null)
    {
        var box = s.CurrentWalkBoxes!.Boxes.Single(b => b.Index == index);
        int x = (int)box.Corners.Average(p => p.X), y = (int)box.Corners.Average(p => p.Y);
        int start = s.SessionTrace.Count;
        actions.Add(new { stage = "walk-box-" + index, index, x, y, geometry = s.CurrentWalkBoxes, result = s.Click(x, y) });
        Settle("walk-box-" + index, end, start, 34);
    }
''' + p[start:]
start=p.index('        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x47);'); end=p.index('\n    }\n    catch',start)
p=p[:start]+'''        var straw = directory.Single(o => o.Id == 610);
        actions.Add(new { stage = "look-straw", target = straw, result = s.Click(straw.X + straw.Width / 2, straw.Y + straw.Height / 2, doubleClick: true) });
        Settle("source-087", "endCutscene at Script 87 +0x09B9");
        var drafts = s.GameStatus.Drafts.Where(d => d.ForwardVerb is 0x3A or 0x4D).ToArray();
        if (drafts.Length != 2) throw new InvalidOperationException("Expected Opening and Healing.");
        int notes = drafts.Max(d => d.Notes.Max());
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes, learnedSlots = drafts.Select(d => d.Slot).ToArray() });
        s.UpdateStaffPossession(true); Settle("staff-ready");
        s.UpdateAvailableNotes(notes); Settle("notes-ready");
        foreach (var draft in drafts) s.UpdateDraftLearned(draft.Slot, true);
        Settle("status-ready");
        Cast(38, 617, 0x3A, "opening", "endCutscene at Room 38 local script 203 +0x00B2", 38);
        ExitClick(38, 609, 37);
        ExitClick(37, 601, 36);
        ExitClick(36, 598, 35);
        ExitClick(35, 587, 34);
        Cast(34, 586, 0x4D, "healing", "endCutscene at Room 34 local script 209 +0x03C0", 34);
        WalkBox(6);
        WalkBox(8, "endCutscene at Room 34 local script 200 +0x0056");
''' + p[end:]
(target/'Program.cs').write_text(p,encoding='utf-8'); (target/'Room34WatcherProbe.csproj').write_bytes((source/'Room20InputProbe.csproj').read_bytes())
prior=Path('.verification/archive/room20-input-20261003'); paths=[p for p in prior.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
paths.extend(Path('Exe/Room20Input-20261003').rglob('*.json'))
preserved=json.loads((prior/'preserved-before.json').read_text()); preserved.update({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
(base/'preserved-before.json').write_text(json.dumps(preserved,indent=2)+'\n')
