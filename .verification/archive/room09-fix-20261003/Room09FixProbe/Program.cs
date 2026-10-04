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
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 50));
    int frames = 0, initialTrace = s.SessionTrace.Count;
    var records = new List<object>();
    var actions = new List<object>();
    var rooms = new List<object>();
    var geometry = s.CurrentWalkBoxes;
    var directory = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 50).FilePath));
    int? lastRoom = 50;
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    void Advance()
    {
        s.AdvanceFrame(); frames++;
        if (lastRoom != s.Snapshot.RoomId) { lastRoom = s.Snapshot.RoomId; rooms.Add(new { frames, room = lastRoom }); }
    }
    void Settle(string stage, string? expected = null, int traceStart = 0, int expectedRoom = 9)
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
    void Cast(int verb, string stage, string end)
    {
        if (s.Snapshot.RoomId != 9) throw new InvalidOperationException("Required Room09 is not current.");
        var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 9).FilePath)).Single(o => o.Id == 273);
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
        if (mode == "direct") actions.Add(s.ActivateObjectVerb(273, verb));
        else foreach (var note in notes)
        {
            actions.Add(s.PressKey("qwertyui"[note - 1]));
            for (int gap = 0; gap < 8; gap++) Advance();
        }
        Settle(stage, end, start);
    }
    void ExitClick(int room, int id, int destination, string stage)
    {
        if (s.Snapshot.RoomId != room) throw new InvalidOperationException("Unexpected exit start room.");
        var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == room).FilePath)).Single(o => o.Id == id);
        int start = s.SessionTrace.Count;
        actions.Add(new { stage, room, id, destination, target, result = s.Click(target.X + target.Width / 2, target.Y + target.Height / 2) });
        Settle(stage, traceStart: start, expectedRoom: destination);
    }
    string? failure = null;
    try
    {
        Settle("arrival", expectedRoom: 50);
        ExitClick(50, 721, 7, "leave-room50");
        ExitClick(7, 252, 9, "enter-room9");
        var loom = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 9).FilePath)).Single(o => o.Id == 272);
        actions.Add(new { stage = "look-loom", target = loom, result = s.Click(loom.X + loom.Width / 2, loom.Y + loom.Height / 2, doubleClick: true) });
        Settle("late-conversation", "endCutscene at Script 101 +0x08B3");
        var drafts = s.GameStatus.Drafts.Where(d => d.ForwardVerb == 0x4D || d.ReverseVerb is 0x4F or 0x51).ToArray();
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

    }
    catch (Exception ex) { failure = ex.ToString(); failed = true; Record("failure"); }
    var packet = new { mode, frames, failure, actions, records, rooms, geometry, directory, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
return failed ? 1 : 0;
