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
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 16));
    int frames = 0, initialTrace = s.SessionTrace.Count;
    var records = new List<object>();
    var actions = new List<object>();
    var rooms = new List<object>();
    var geometry = s.CurrentWalkBoxes;
    var directory = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 16).FilePath));
    int? lastRoom = 16;
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    void Advance()
    {
        s.AdvanceFrame(); frames++;
        if (lastRoom != s.Snapshot.RoomId) { lastRoom = s.Snapshot.RoomId; rooms.Add(new { frames, room = lastRoom }); }
    }
    void Settle(string stage, string? expected = null, int traceStart = 0, int expectedRoom = 16)
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
    string? failure = null;
    try
    {
        Settle("arrival");
        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x47);
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = draft.Notes.Max(), learnedSlot = draft.Slot });
        s.UpdateStaffPossession(true); Settle("staff-ready");
        s.UpdateAvailableNotes(draft.Notes.Max()); Settle("notes-ready");
        s.UpdateDraftLearned(draft.Slot, true); Settle("status-ready");
        Cast(16, 350, 0x47, "sleep", "endCutscene at Room 16 local script 204 +0x0128", 16);
        var exit = directory.Single(o => o.Id == 346);
        actions.Add(new { stage = "enter-room18", target = exit, result = s.Click(exit.X + exit.Width / 2, exit.Y + exit.Height / 2) });
        Settle("enter-room18", expectedRoom: 18);
        var room18 = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 18).FilePath));
        var conversation = room18.Single(o => o.Id == 372);
        actions.Add(new { stage = "look-conversation", target = conversation, result = s.Click(conversation.X + conversation.Width / 2, conversation.Y + conversation.Height / 2, doubleClick: true) });
        Settle("reveal-crystal", "endCutscene at Room 18 local script 205 +0x0316", expectedRoom: 18);
        var crystal = room18.Single(o => o.Id == 371);
        actions.Add(new { stage = "look-crystal", target = crystal, result = s.Click(crystal.X + crystal.Width / 2, crystal.Y + crystal.Height / 2, doubleClick: true) });
        Settle("enter-room20", expectedRoom: 20);
        Cast(20, 390, 0x48, "wake", "endCutscene at Room 20 local script 206 +0x00A9", 18);

    }
    catch (Exception ex) { failure = ex.ToString(); failed = true; Record("failure"); }
    var packet = new { mode, frames, failure, actions, records, rooms, geometry, directory, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
return failed ? 1 : 0;
