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
    var directory = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 36).FilePath));
    int? lastRoom = 34;
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    bool track = false; var boxLog = new List<object>();
    void Advance()
    {
        s.AdvanceFrame(); frames++;
        if (track) { var a0 = s.Actors.First(); boxLog.Add(new { frames, box = a0.WalkBox, a0.X, a0.Y, camera = s.Snapshot.KnownGlobalVariables.TryGetValue("var[0x002]", out var cam) ? (int)cam : -1 }); }
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
    LoomObjectActionResult ClickObject(int room, int id, bool dbl, string stage)
    {
        if (s.Snapshot.RoomId != room) throw new InvalidOperationException($"Unexpected start room {s.Snapshot.RoomId} for {stage}.");
        var all = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == room).FilePath));
        var t = all.Single(o => o.Id == id);
        var classes = s.Snapshot.ObjectClasses; var earlier = all.TakeWhile(o => o.Id != id).Where(o => !(classes.TryGetValue(o.Id, out var c) && (c & 0x800000) != 0)).ToArray();
        (int X, int Y)? pick = null;
        for (int yy = t.Y; yy < t.Y + t.Height && pick is null; yy++)
            for (int xx = t.X; xx < t.X + t.Width; xx++)
                if (!earlier.Any(o => xx >= o.X && xx < o.X + o.Width && yy >= o.Y && yy < o.Y + o.Height)) { pick = (xx, yy); break; }
        if (pick is null) throw new InvalidOperationException($"No unshadowed point for {id}.");
        var r = s.Click(pick.Value.X, pick.Value.Y, dbl);
        actions.Add(new { stage, room, id, dbl, target = t, result = r });
        return r;
    }
    void FloorBox(int index, string? end = null, int room = 34)
    {
        var box = s.CurrentWalkBoxes!.Boxes.Single(b => b.Index == index);
        var all = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == room).FilePath));
        var classes = s.Snapshot.ObjectClasses;
        var live = all.Where(o => !(classes.TryGetValue(o.Id, out var c) && (c & 0x800000) != 0)).ToArray();
        int x0 = box.Corners.Min(p => p.X), x1 = box.Corners.Max(p => p.X), y0 = box.Corners.Min(p => p.Y), y1 = box.Corners.Max(p => p.Y);
        (int X, int Y)? pick = null;
        var c0 = box.Corners.First(); if (!live.Any(o => c0.X >= o.X && c0.X < o.X + o.Width && c0.Y >= o.Y && c0.Y < o.Y + o.Height)) pick = (c0.X, c0.Y);
        for (int yy = y0; yy <= y1 && pick is null; yy++) for (int xx = x0; xx <= x1; xx++)
            if (!live.Any(o => xx >= o.X && xx < o.X + o.Width && yy >= o.Y && yy < o.Y + o.Height)) { pick = (xx, yy); break; }
        if (pick is null) throw new InvalidOperationException($"No object-free point in box {index}.");
        int start = s.SessionTrace.Count;
        actions.Add(new { stage = "floor-box-" + index, index, pick, geometry = s.CurrentWalkBoxes, live = live.Select(o => new { o.Id, o.X, o.Y, o.Width, o.Height }), result = s.Click(pick.Value.X, pick.Value.Y) });
        Settle("floor-box-" + index, end, start, room);
    }
    string? failure = null;
    try
    {
        Settle("arrival", expectedRoom: 34);
        var wanted = new[] { 0x44, 0x4C, 0x3A, 0x4D };
        var drafts = s.GameStatus.Drafts.Where(d => wanted.Contains(d.ForwardVerb ?? -1) || wanted.Contains(d.ReverseVerb ?? -1)).ToArray();
        int notes = drafts.Max(d => d.Notes.Max());
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes, learnedSlots = drafts.Select(d => d.Slot).ToArray() });
        s.UpdateStaffPossession(true); Settle("staff-ready");
        s.UpdateAvailableNotes(notes); Settle("notes-ready");
        foreach (var draft in drafts) s.UpdateDraftLearned(draft.Slot, true);
        Settle("status-ready");
        Cast(34, 561, 0x44, "wake-first", "endCutscene at Script 85 +0x05CE", 34);
        Cast(34, 561, 0x4C, "change-appearances", "endCutscene at Room 34 local script 202 +0x015F", 34);
        ExitClick(34, 558, 35);
        for (int w = 0; w < 900; w++) Advance();
        Settle("room35-scene-wait", expectedRoom: 35);
        ExitClick(35, 588, 36);
        WalkBox(9, "endCutscene at Room 36 local script 200 +0x003D", 36);
        WalkBox(9, "endCutscene at Room 36 local script 200 +0x009B", 36);
        ExitClick(36, 599, 37);
        actions.Add(new { stage = "firewood-trigger", result = s.Click(109, 102) });
        Settle("source-086", "endCutscene at Script 86 +0x055C", 0, 38);
        var straw = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 38).FilePath)).Single(o => o.Id == 610);
        actions.Add(new { stage = "look-straw", target = straw, result = s.Click(straw.X + straw.Width / 2, straw.Y + straw.Height / 2, doubleClick: true) });
        Settle("source-087", "endCutscene at Script 87 +0x09B9", 0, 38);
        actions.Add(new { stage = "catalog-hop-43", note = "explicit exploration assumption: catalog visit keeps session state; 601 return is source-blocked by the costume gate" });
        s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 43)); lastRoom = 43; rooms.Add(new { frames, room = 43 });
        Settle("arrival43", expectedRoom: 43);
        { int st = s.SessionTrace.Count; ClickObject(43, 650, false, "use650-093"); Settle("cage093", "endCutscene at Script 93 ", st, 43); }
        ClickObject(43, 639, false, "use639"); Settle("arrive44", expectedRoom: 44);
        ClickObject(44, 653, true, "look653");
        Advance(); Advance();
        actions.Add(new { stage = "escape", result = s.PressKey(0x1B) });
        Settle("arrive45", expectedRoom: 45);
        ClickObject(45, 656, true, "look656"); Settle("staff", expectedRoom: 45);
        ClickObject(45, 655, false, "use655"); Settle("return43", expectedRoom: 43);
        ClickObject(43, 639, false, "use639-096"); Settle("end096", "endCutscene at Script 96 ", 0, 46);
        { int st = s.SessionTrace.Count; ClickObject(46, 684, true, "look684-097"); Settle("room34-via-097", "endCutscene at Script 97 ", st, 34); }
        Record("in34");
        { int st = s.SessionTrace.Count; actions.Add(new { stage = "exit559-first", result = s.Click(246, 72) }); Settle("gate-first", null, st, 34); }
        Record("after-first");
        { int st = s.SessionTrace.Count; actions.Add(new { stage = "exit559-second", result = s.Click(246, 72) }); Settle("gate207", "endCutscene at Room 34 local script 207 +0x0100", st, 34); }
    }
    catch (Exception ex) { failure = ex.ToString(); failed = true; Record("failure"); }
    actions.Add(new { stage = "box-log", boxLog });
    var packet = new { mode, frames, failure, actions, records, rooms, geometry, directory, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
return failed ? 1 : 0;
