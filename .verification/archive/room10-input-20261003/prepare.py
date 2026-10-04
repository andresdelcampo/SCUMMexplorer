"""Create an isolated Core input probe; refuse to replace evidence or source."""
from pathlib import Path
base = Path('.verification/archive/room10-input-20261003/Room10InputProbe')
base.mkdir(parents=True, exist_ok=False)
source = Path('.verification/archive/room25-input-20261002/Room25InputProbe')
p = (source/'Program.cs').read_text()
p = p.replace('== 25','== 10').replace('= 25;', '= 10;').replace('= 25)', '= 10)')
begin = p.index('    void Use(')
end = p.index('    string? failure', begin)
p = p[:begin] + '''    void Cast(int verb, string stage, string end)
    {
        if (s.Snapshot.RoomId != 10) throw new InvalidOperationException("Required Room10 is not current.");
        var target = directory.Single(o => o.Id == 296);
        actions.Add(new { stage = stage + "-approach", result = s.Click(target.WalkX, target.WalkY) });
        Settle(stage + "-approach");
        actions.Add(new { stage = stage + "-target", result = s.Click(target.X + target.Width / 2, target.Y + target.Height / 2) });
        Settle(stage + "-target");
        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == verb || d.ReverseVerb == verb);
        bool reverse = draft.ReverseVerb == verb;
        if (s.GameStatus.StaffHeld != true || draft.Learned != true || s.GameStatus.AvailableNoteCount < draft.Notes.Max() || reverse && draft.Reversible != true)
            throw new InvalidOperationException("Missing supported spell prerequisite.");
        var notes = reverse ? draft.Notes.Reverse().ToArray() : draft.Notes.ToArray();
        actions.Add(new { stage, verb, draft.Slot, reverse, notes, mode });
        Record(stage + "-before");
        int start = s.SessionTrace.Count;
        if (mode == "direct") actions.Add(s.ActivateObjectVerb(296, verb));
        else foreach (var note in notes)
        {
            actions.Add(s.PressKey("qwertyui"[note - 1]));
            for (int gap = 0; gap < 8; gap++) Advance();
        }
        Settle(stage, end, start);
    }
''' + p[end:]
begin = p.index('        WalkBox(4,')
end = p.index('\n    }\n    catch',begin)
p = p[:begin] + '''        WalkBox(3, "endCutscene at Room 10 local script 200 +0x0102");
        WalkBox(7, "endCutscene at Room 10 local script 200 +0x01AB");
        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x3A && d.ReverseVerb == 0x3B);
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = draft.Notes.Max(), learnedSlot = draft.Slot });
        s.UpdateStaffPossession(true);
        Settle("staff-ready");
        s.UpdateAvailableNotes(draft.Notes.Max());
        Settle("notes-ready");
        s.UpdateDraftLearned(draft.Slot, true);
        Settle("status-ready");
        Cast(0x3A, "open", "endCutscene at Room 10 local script 212 +0x007C");
        Cast(0x3B, "close", "endCutscene at Room 10 local script 213 +0x002F");
''' + p[end:]
(base/'Program.cs').write_text(p,encoding='utf-8')
(base/'Room10InputProbe.csproj').write_bytes((source/'Room25InputProbe.csproj').read_bytes())
