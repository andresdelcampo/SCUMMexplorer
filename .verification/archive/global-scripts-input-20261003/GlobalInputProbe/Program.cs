using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
if (Directory.Exists(args[1])) throw new IOException("Use a fresh evidence directory.");
Directory.CreateDirectory(args[1]);
bool failed = false;
foreach (var mode in new[] { "input" })
{
    var s = new LoomExplorationSession(game, objects);
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
    int frames = 0, initialTrace = s.SessionTrace.Count;
    var records = new List<object>();
    var actions = new List<object>();
    var rooms = new List<object>();
    var geometry = s.CurrentWalkBoxes;
    var directory = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 34).FilePath));
    int? lastRoom = 34;
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    void Advance()
    {
        s.AdvanceFrame(); frames++;
        if (lastRoom != s.Snapshot.RoomId) { lastRoom = s.Snapshot.RoomId; rooms.Add(new { frames, room = lastRoom }); }
    }
    void Settle(string stage, string? expected = null, int traceStart = 0, int expectedRoom = 34)
    {
        int quiet = 0;
        for (int f = 0; f < 3000; f++)
        {
            var boundaries = Boundaries(initialTrace);
            bool balanced = boundaries.Count(t => t.StartsWith("cutscene at ")) == boundaries.Count(t => t.StartsWith("endCutscene at "));
            if (s.GameStatus.CanEdit && !s.EgoMoving && !s.CameraMoving && s.Messages.HaveMsg == 0 && balanced && s.Snapshot.RoomId == expectedRoom &&
                (expected is null || Boundaries(traceStart).Any(t => t.StartsWith(expected))))
            { if (++quiet == 32) { Record(stage); return; } }
            else quiet = 0;
            Advance();
        }
        Record(stage + "-timeout");
        throw new InvalidOperationException($"{stage}: bounded settling failed or missing {expected}");
    }
    void Cast(int room, int objectId, int verb, string stage, string end, int destination)
    {
        if (s.Snapshot.RoomId != room) throw new InvalidOperationException("Required spell room is not current.");
        var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == room).FilePath)).Single(o => o.Id == objectId);
        actions.Add(new { stage = stage + "-approach", result = s.Click(target.WalkX, target.WalkY) });
        Settle(stage + "-approach", expectedRoom: room);
        actions.Add(new { stage = stage + "-target", result = s.Click(target.X + target.Width / 2, target.Y + target.Height / 2) });
        Settle(stage + "-target", expectedRoom: room);
        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == verb || d.ReverseVerb == verb);
        bool reverse = draft.ReverseVerb == verb;
        if (s.GameStatus.StaffHeld != true || draft.Learned != true || s.GameStatus.AvailableNoteCount < draft.Notes.Max() || reverse && draft.Reversible != true)
            throw new InvalidOperationException("Missing supported spell prerequisite.");
        var notes = reverse ? draft.Notes.Reverse().ToArray() : draft.Notes.ToArray();
        actions.Add(new { stage, verb, draft.Slot, reverse, notes, mode });
        Record(stage + "-before");
        int start = s.SessionTrace.Count;
        if (mode == "direct") actions.Add(s.ActivateObjectVerb(objectId, verb));
        else foreach (var note in notes)
        {
            actions.Add(s.PressKey("qwertyui"[note - 1]));
            for (int gap = 0; gap < 8; gap++) Advance();
        }
        Settle(stage, end, start, destination);
    }
    void ExitClick(int room, int id, int destination)
    {
        if (s.Snapshot.RoomId != room) throw new InvalidOperationException("Unexpected exit start room.");
        var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == room).FilePath)).Single(o => o.Id == id);
        actions.Add(new { stage = "exit-" + id, room, id, destination, target, result = s.Click(target.X + target.Width / 2, target.Y + target.Height / 2) });
        Settle("exit-" + id, expectedRoom: destination);
    }
    void WalkBox(int index, string? end = null, int room = 34)
    {
        var box = s.CurrentWalkBoxes!.Boxes.Single(b => b.Index == index);
        int x = (int)box.Corners.Average(p => p.X), y = (int)box.Corners.Average(p => p.Y);
        int start = s.SessionTrace.Count;
        actions.Add(new { stage = "walk-box-" + index, index, x, y, geometry = s.CurrentWalkBoxes, result = s.Click(x, y) });
        Settle("walk-box-" + index, end, start, room);
    }
    string? failure = null;
    try
    {
        Settle("arrival", expectedRoom: 34);
        void Key(int key, string stage, string end)
        {
            int start = s.SessionTrace.Count;
            actions.Add(new { stage, key, result = s.PressKey(key) });
            Settle(stage, end, start, 34);
        }
        void Aim(string stage)
        {
            var o = directory.Single(x => x.Id == 561);
            actions.Add(new { stage = stage + "-approach", result = s.Click(o.WalkX, o.WalkY) });
            Settle(stage + "-approach", expectedRoom: 34);
            actions.Add(new { stage = stage + "-aim", result = s.Click(o.X + o.Width / 2, o.Y + o.Height / 2) });
            Settle(stage + "-aim", expectedRoom: 34);
        }
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = 3 });
        s.UpdateStaffPossession(true); Settle("staff-ready");
        s.UpdateAvailableNotes(3); Settle("notes-ready");
        Key('q', "note-without-aim", "endCutscene at Script 13 ");
        Aim("aim1");
        Key('u', "note-too-high", "endCutscene at Script 11 ");
        // too far: walk away, then aim 561 and press a note mid-walk
        actions.Add(new { stage = "walk-away", result = s.Click(200, 125) });
        Settle("walk-away", expectedRoom: 34);
        var o561 = directory.Single(x => x.Id == 561);
        actions.Add(new { stage = "aim-far", result = s.Click(o561.X + o561.Width / 2, o561.Y + o561.Height / 2) });
        for (int w = 0; w < 3; w++) Advance();
        Key('q', "note-too-far", "endCutscene at Script 12 ");
        Settle("far-arrived", expectedRoom: 34);
        actions.Add(new { userStatusEdits = true, notes = 7 });
        s.UpdateAvailableNotes(7); Settle("notes-seven");
        // wrong four notes aimed at 561 -> "Ese no sera un hechizo" (script 022 +0138)
        Aim("aim2");
        {
            int start = s.SessionTrace.Count;
            var known = s.GameStatus.Drafts.SelectMany(d => new[] { string.Join(",", d.Notes), string.Join(",", d.Notes.Reverse()) }).ToHashSet();
            int[] wrong = Enumerable.Range(1, 7).SelectMany(a => Enumerable.Range(1, 7).Select(b => new[] { a, b, a, b })).First(c => !known.Contains(string.Join(",", c)));
            actions.Add(new { stage = "wrong-notes", wrong });
            foreach (var n in wrong) { actions.Add(s.PressKey("qwertyui"[n - 1])); for (int g = 0; g < 8; g++) Advance(); }
            Settle("not-a-spell", "endCutscene at Script 22 +0x0151", start, 34);
        }
        actions.Add(new { stage = "verb-items", items = s.VerbArea().Items });
        var hint = s.VerbArea().Items.FirstOrDefault(i => i.VerbId == 0x35);
        if (hint is not null)
        {
            int start = s.SessionTrace.Count;
            actions.Add(new { stage = "practice-hint", result = s.ClickVerbArea(hint.X + hint.Width / 2, hint.Y + hint.Height / 2) });
            Settle("practice-hint", "endCutscene at Script 9 ", start, 34);
        }
        // Escape while the first note's draft playback runs -> script 005 +01C0
        {
            int start = s.SessionTrace.Count;
            actions.Add(s.PressKey('q'));
            for (int g = 0; g < 2; g++) Advance();
            actions.Add(new { stage = "escape", result = s.PressKey(0x1B) });
            Settle("escape-skip", "endCutscene at Script 05 ", start, 34);
        }
        {
            int start = s.SessionTrace.Count;
            actions.Add(new { stage = "save-key-F5", result = s.PressKey(0x13F) });
            for (int w = 0; w < 120; w++) Advance();
            if (s.Snapshot.RoomId != 70) throw new InvalidOperationException("Save screen Room 70 not shown.");
            Record("save-screen");
            actions.Add(new { stage = "save-cancel", geometry = s.CurrentWalkBoxes, result = s.Click(272, 100) });
            for (int w = 0; w < 60; w++) Advance();
            if (!Boundaries(start).Any(t => t.StartsWith("endCutscene at Script 04 "))) throw new InvalidOperationException("Save screen did not end.");
            Record("save-cancel");
        }
    }
    catch (Exception ex) { failure = ex.ToString(); failed = true; Record("failure"); }
    var packet = new { mode, frames, failure, actions, records, rooms, geometry, directory, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
return failed ? 1 : 0;
