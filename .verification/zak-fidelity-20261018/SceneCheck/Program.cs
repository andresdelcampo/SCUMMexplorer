using System.Collections;
using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Read-only diagnostic of the five S7 capped starts; mirrors ScummCutsceneRun's invocation and 6000-frame bound.
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var data = ScummDataSources.For(game);
if (args.Length > 4 && args[1] == "object")
{
    var room = int.Parse(args[2]);
    var id = int.Parse(args[3]);
    var verb = int.Parse(args[4]);
    var script = data.ReadObjectVerb(room,data.ReadRoom(room).Objects.Last(r=>r.Id==id),verb,out _);
    foreach(var i in script.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
    return;
}
if (args.Length > 2 && args[1] == "find")
{
    void Print(string label, ScummScriptDisassembly? script)
    {
        if (script is null) return;
        foreach (var i in script.Instructions.Where(i => i.Operands.Contains(args[2], StringComparison.OrdinalIgnoreCase)))
            Console.WriteLine($"{label} +{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue)) Print($"Script {r.ResourceId}", data.GlobalScript(r.ResourceId!.Value));
    foreach (var r in game.Rooms)
    {
        var id = r.ResourceId!.Value;
        var read = data.ReadRoom(id);
        foreach (var name in read.ObjectNames)
        {
            var text = System.Text.Encoding.Latin1.GetString(name.Value);
            if (text.Contains("batter", StringComparison.OrdinalIgnoreCase) || text.Contains("pila",StringComparison.OrdinalIgnoreCase)) Console.WriteLine($"NAME Room {id} object {name.Key}: {text}");
        }
        Print($"Room {id} entry",data.RoomEntryScript(id));
        Print($"Room {id} exit",data.RoomExitScript(id));
        foreach(var record in read.Objects.DistinctBy(o=>o.Id))
            foreach(var verb in data.ReadObjectVerbs(id,[record]).GetValueOrDefault(record.Id) ?? [])
                Print($"Room {id} object {record.Id} verb {verb.VerbId}",data.ReadObjectVerb(id,record,verb.VerbId,out _));
    }
    return;
}
var keys = args.Length > 3 && args[1] == "one" ? new[] { (int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4])) }
    : new[] { (2, 0, 0), (11, 0, 0), (11, 249, 1), (14, 55, 11), (23, 410, 253) };
if (args.Length > 1 && args[1] == "dump")
{
    foreach (var id in args.Skip(2).Select(int.Parse))
    {
        Console.WriteLine($"Script {id}");
        foreach (var i in data.GlobalScript(id)!.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    return;
}
if (args.Length > 1 && args[1] == "runs")
{
    var planner = new ScummCutscenePlanner(game);
    foreach (var (room, obj, verb) in keys)
    {
        var trigger = planner.Groups.Single(g => g.First.Room == room && g.First.ObjectId == obj && g.First.VerbId == verb).First;
        var run = planner.Run(trigger);
        Console.WriteLine($"{room}/{obj}/{verb}: begun {run.Begun}; frames {run.Frames}; rooms [{string.Join(',',run.RoomsEntered)}]; failure {run.Failure ?? "none"}");
    }
    return;
}
foreach (var (room, obj, verb) in keys)
{
    Console.WriteLine($"=== {Path.GetFileName(folder)} {room}/{obj}/{verb} ===");
    var session = ScummGameSession.BootForPlay(game, out _);
    var at = session.SessionTrace.Count;
    if (obj == 0) session.EnterRoomAgain(game.Rooms.First(r => r.ResourceId == room));
    else { session.VisitRoom(game.Rooms.First(r => r.ResourceId == room)); session.ActivateObjectVerb(obj, verb); }
    for (var frame = 0; frame <= ScummCutsceneRun.EndFrames; frame++)
    {
        if (frame % 1000 == 0) Show(frame);
        if (frame == ScummCutsceneRun.EndFrames) break;
        session.AdvanceFrame();
    }
    foreach (var line in session.SessionTrace.Skip(at).Where(t => t.Contains("cutscene", StringComparison.OrdinalIgnoreCase) || t.Contains("unsupported", StringComparison.OrdinalIgnoreCase)).Distinct()) Console.WriteLine("TRACE " + line);
    foreach (var line in session.Snapshot.Uncertainties.Distinct().Take(35)) Console.WriteLine("UNCERT " + line);
    void Show(int frame)
    {
        var snap = session.Snapshot;
        Console.WriteLine($"frame {frame} room {snap.RoomId} cutscene {session.InCutscene} begun {session.CutscenesBegun} havemsg {session.Messages.HaveMsg} messages {JsonSerializer.Serialize(snap.ScreenMessages)}");
        var pending = (IEnumerable)typeof(ScummSession).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        foreach (var item in pending)
        {
            object? P(object o, string name) => o.GetType().GetProperty(name)!.GetValue(o);
            var f = P(item, "Frame")!;
            var offset = (int)P(item, "Offset")!;
            var script = (ScummScriptDisassembly)P(item, "Script")!;
            Console.WriteLine($"PENDING {P(f,"Label")} offset +{offset:X4} delay {P(f,"Delay")} freeze {P(f,"FreezeCount")}");
            foreach (var i in script.Instructions.Where(i => i.Offset >= offset - 12 && i.Offset <= offset + 22)) Console.WriteLine($"  +{i.Offset:X4} {i.Operation} {i.Operands}");
        }
        Console.WriteLine("ACTORS " + JsonSerializer.Serialize(session.Actors.Where(a => a.Room == snap.RoomId).Select(a => new {a.Number,a.Room,a.X,a.Y,a.WalkBox,a.Moving,a.Unknown,a.Look.Facing})));
        Console.WriteLine("VARS " + JsonSerializer.Serialize(snap.KnownGlobalVariables.Where(p=>p.Key.StartsWith("var[") && Convert.ToInt32(p.Key[6..^1],16) < 0x46)));
    }
}
