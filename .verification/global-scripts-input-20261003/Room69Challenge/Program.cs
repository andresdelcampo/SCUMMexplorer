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
    var s = new LoomExplorationSession(game, objects, stopAtBootInput: true);
    int frames = 0, initialTrace = s.SessionTrace.Count;
    var records = new List<object>();
    var actions = new List<object>();
    var rooms = new List<object>();
    var geometry = s.CurrentWalkBoxes;
    var directory = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 69).FilePath));
    int? lastRoom = 69;
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    void Advance()
    {
        s.AdvanceFrame(); frames++;
        if (lastRoom != s.Snapshot.RoomId) { lastRoom = s.Snapshot.RoomId; rooms.Add(new { frames, room = lastRoom }); }
    }
    void Settle(string stage, string? expected = null, int traceStart = 0, int expectedRoom = 69)
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
        for (int w = 0; w < 300; w++) Advance();
        Record("arrival");
        actions.Add(new { directory });
        int V(string k) => s.Snapshot.KnownGlobalVariables.TryGetValue(k, out var v) ? v : -1;
        void ClickObj(int id, string stage)
        {
            var o = directory.Single(x => x.Id == id);
            actions.Add(new { stage, id, target = o, result = s.Click(o.X + o.Width / 2, o.Y + o.Height / 2) });
        }
        ClickObj(876, "practice-click");
        for (int w = 0; w < 600; w++) Advance();
        Record("after-practice");
        for (int q = 1; q <= 4; q++)
        {
            int idx = V("var[0x062]");
            int ans = V($"var[0x{0x52 + idx - 1:X3}]");
            actions.Add(new { stage = "question", q, var062 = idx, answers = new[] { V("var[0x052]"), V("var[0x053]"), V("var[0x054]"), V("var[0x055]") }, ans, room = s.Snapshot.RoomId });
            if (idx < 1 || ans < 0 || ans > 11) throw new InvalidOperationException($"No known answer for question {q} (var062={idx}, answer={ans}).");
            ClickObj(886 + ans, $"answer-{q}");
            for (int w = 0; w < 900; w++) Advance();
            Record($"after-answer-{q}");
        }
        for (int w = 0; w < 5200; w++) Advance();
        Settle("boot-done", expectedRoom: 2);
        actions.Add(new { stage = "boot-done", room = s.Snapshot.RoomId, var07B = V("var[0x07B]"), var020 = V("var[0x020]") });
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = 7 });
        s.UpdateStaffPossession(true); Settle("staff-ready", expectedRoom: 2);
        s.UpdateAvailableNotes(7); Settle("notes-ready", expectedRoom: 2);
        var dir2 = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 2).FilePath));
        var o154 = dir2.Single(x => x.Id == 154);
        actions.Add(new { stage = "aim-154", target = o154, result = s.Click(o154.X + o154.Width / 2, o154.Y + o154.Height / 2) });
        Settle("aim-154", expectedRoom: 2);
        actions.Add(new { stage = "note-q", result = s.PressKey('q') });
        for (int g = 0; g < 2; g++) Advance();
        actions.Add(new { stage = "note-w", result = s.PressKey('w') });
        for (int g = 0; g < 2; g++) Advance();
        Record("two-notes");
        actions.Add(new { stage = "verb-items", var063 = V("var[0x063]"), var07B = V("var[0x07B]"), items = s.VerbArea().Items });
        var hint = s.VerbArea().Items.FirstOrDefault(i => i.VerbId == 0x35);
        if (hint is null) throw new InvalidOperationException("Hint verb 0x35 is not on.");
        int hs = s.SessionTrace.Count;
        actions.Add(new { stage = "practice-hint", result = s.ClickVerbArea(hint.X + hint.Width / 2, hint.Y + hint.Height / 2) });
        Settle("practice-hint", "endCutscene at Script 09 ", hs, 2);
    }
    catch (Exception ex) { failure = ex.ToString(); failed = true; Record("failure"); }
    var packet = new { mode, frames, failure, actions, records, rooms, geometry, directory, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
return failed ? 1 : 0;
