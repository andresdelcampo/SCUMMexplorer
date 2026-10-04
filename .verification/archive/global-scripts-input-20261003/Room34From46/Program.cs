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
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 43));
    int frames = 0, initialTrace = s.SessionTrace.Count;
    var records = new List<object>();
    var actions = new List<object>();
    var rooms = new List<object>();
    var geometry = s.CurrentWalkBoxes;
    var directory = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 43).FilePath));
    int? lastRoom = 43;
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    void Advance()
    {
        s.AdvanceFrame(); frames++;
        if (lastRoom != s.Snapshot.RoomId) { lastRoom = s.Snapshot.RoomId; rooms.Add(new { frames, room = lastRoom }); }
    }
    void Settle(string stage, string? expected = null, int traceStart = 0, int expectedRoom = 17)
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
    string? failure = null;
    try
    {
        Settle("arrival", expectedRoom: 43);
        { int st = s.SessionTrace.Count; ClickObject(43, 650, false, "use650-093"); Settle("cage093", "endCutscene at Script 93 ", st, 43); }
        ClickObject(43, 639, false, "use639"); Settle("arrive44", expectedRoom: 44);
        ClickObject(44, 653, true, "look653");
        Advance(); Advance();
        actions.Add(new { stage = "escape", result = s.PressKey(0x1B) });
        Settle("arrive45", expectedRoom: 45);
        ClickObject(45, 656, true, "look656"); Settle("staff", expectedRoom: 45);
        ClickObject(45, 655, false, "use655"); Settle("return43", expectedRoom: 43);
        ClickObject(43, 639, false, "use639-096"); Settle("end096", "endCutscene at Script 96 ", 0, 46);
        actions.Add(new { stage = "after096", room = s.Snapshot.RoomId });
        { int st = s.SessionTrace.Count; ClickObject(46, 684, true, "look684-097"); Settle("room34-via-097", "endCutscene at Script 97 ", st, 34); }
        Record("in34");
        actions.Add(new { stage = "in34", status = s.GameStatus, geometry = s.CurrentWalkBoxes });

    }
    catch (Exception ex) { failure = ex.ToString(); failed = true; Record("failure"); }
    var packet = new { mode, frames, failure, actions, records, rooms, geometry, directory, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
return failed ? 1 : 0;
