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
foreach (var mode in new[] { "direct", "input" })
{
    var s = new LoomExplorationSession(game, objects);
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 24));
    int frames = 0, initialTrace = s.SessionTrace.Count;
    var records = new List<object>();
    var actions = new List<object>();
    var rooms = new List<object>();
    var geometry = s.CurrentWalkBoxes;
    var directory = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 24).FilePath));
    int? lastRoom = 24;
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    void Advance()
    {
        s.AdvanceFrame(); frames++;
        if (lastRoom != s.Snapshot.RoomId) { lastRoom = s.Snapshot.RoomId; rooms.Add(new { frames, room = lastRoom }); }
    }
    void Settle(string stage, string? expected = null, int traceStart = 0, int expectedRoom = 24)
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
    void WalkBox(int index, string end)
    {
        var box = s.CurrentWalkBoxes!.Boxes.Single(b => b.Index == index);
        int x = (int)box.Corners.Average(p => p.X), y = (int)box.Corners.Average(p => p.Y);
        int start = s.SessionTrace.Count;
        actions.Add(new { stage = "walk-box-" + index, index, x, y, result = s.Click(x, y) });
        Settle("walk-box-" + index, end, start);
    }
    void Cast(int verb, string stage, string end)
    {
        if (s.Snapshot.RoomId != 24) throw new InvalidOperationException("Required Room24 is not current.");
        var target = directory.Single(o => o.Id == 432);
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
        if (mode == "direct") actions.Add(s.ActivateObjectVerb(432, verb));
        else foreach (var note in notes)
        {
            actions.Add(s.PressKey("qwertyui"[note - 1]));
            for (int gap = 0; gap < 8; gap++) Advance();
        }
        Settle(stage, end, start);
    }
    string? failure = null;
    try
    {
        Settle("arrival");
        WalkBox(3, "endCutscene at Room 24 local script 200 +0x007E");
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

    }
    catch (Exception ex) { failure = ex.ToString(); failed = true; Record("failure"); }
    var packet = new { mode, frames, failure, actions, records, rooms, geometry, directory, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
return failed ? 1 : 0;
