using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

var folder = args.FirstOrDefault() ?? @"D:\Juegos\Lucas\mmansion ega en";
var mode = args.ElementAtOrDefault(1) ?? "scan";
var game = new GameFolderScanner().Scan(folder);
var data = ScummDataSources.For(game);
if (mode == "raw")
{
    foreach (var id in data.GlobalScriptIds())
    {
        var script = data.GlobalScript(id);
        if (script is null || !script.Instructions.Any(i => i.Operation == "cutscene")) continue;
        Console.WriteLine($"Script {id}: " + string.Join("; ", script.Instructions.Where(i => i.Operation is "cutscene" or "endCutscene" or "print").Select(i => $"+{i.Offset:X4} {i.Operation} {i.Operands}")));
    }
    return;
}
if (mode == "script")
{
    var script = data.GlobalScript(int.Parse(args[2]));
    foreach (var instruction in script!.Instructions)
        Console.WriteLine($"+0x{instruction.Offset:X4} {instruction.Operation} {instruction.Operands}");
    return;
}
if (mode == "proof")
{
    var room = int.Parse(args[2]);
    var obj = int.Parse(args[3]);
    var verb = Convert.ToInt32(args[4], 16);
    var source = data.ReadObjectVerb(room, data.ReadRoom(room).Objects.Single(o => o.Id == obj), verb, out _);
    Console.WriteLine($"Source Room {room} object {obj} verb {verb:X2}");
    foreach (var i in source.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
    var session = ManiacCutsceneSetup.NewSession(game);
    session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
    var mark = session.SessionTrace.Count;
    var earlier = session.CutscenesBegun;
    session.ActivateObjectVerb(obj, verb);
    var messages = new HashSet<string>();
    var frame = 0;
    for (; frame < ScummCutsceneRun.EndFrames; frame++)
    {
        foreach (var message in session.Messages.OnScreen)
            if (messages.Add(message.Text)) Console.WriteLine($"Frame {frame}, Room {session.CurrentRoomId}: {message.Text}");
        if (session.CutscenesBegun > earlier && !session.InCutscene && session.Messages.HaveMsg == 0) break;
        session.AdvanceFrame();
    }
    Console.WriteLine($"Result: begun={session.CutscenesBegun > earlier}, frames={frame}, currentRoom={session.CurrentRoomId}, inCutscene={session.InCutscene}");
    foreach (var line in session.SessionTrace.Skip(mark).Where(l => l.Contains("cutscene", StringComparison.OrdinalIgnoreCase) || l.Contains("runs script"))) Console.WriteLine(line);
    foreach (var line in session.Snapshot.Uncertainties.Where(l => l.Contains("unsupported v2", StringComparison.Ordinal))) Console.WriteLine("Retained limitation: " + line);
    return;
}
var clock = System.Diagnostics.Stopwatch.StartNew();
var planner = ManiacCutsceneSetup.Planner(game);
Console.WriteLine($"Scan {planner.Triggers.Count} triggers; {planner.Groups.Count} groups; {planner.Groups.Count(g => g.IsScene)} static scenes; {clock.ElapsedMilliseconds} ms");
Console.WriteLine("Verbs: " + string.Join(", ", Enumerable.Range(1, 15).Select(v => $"{v:X2}={planner.Verbs.WordFor(v)}")));
foreach (var group in planner.Groups)
{
    var t = group.First;
    var line = $"{t.Room}/{t.ObjectId}/{t.VerbId:X2}\t{ScummCutsceneTitles.Title(t)}\t{(group.IsScene ? "Scene" : "Dialogue")}\t{string.Join(";", t.Scripts)}\t{t.SpanInstructions} instructions\t{t.Texts} texts\t{t.RoomLoads} loads\t{t.FirstText}";
    if (mode == "run")
    {
        var run = planner.Run(t);
        line += $"\tBegun={run.Begun}; frames={run.Frames}; rooms={string.Join(',', run.RoomsEntered)}; failure={run.Failure}";
    }
    Console.WriteLine(line);
}
if (mode == "run")
    Console.WriteLine($"Runs {planner.Groups.Count}; begun {planner.Groups.Count(g => planner.Run(g.First).Begun)}; failed {planner.Groups.Count(g => planner.Run(g.First).Failure is not null)}; frame-bound {planner.Groups.Count(g => planner.Run(g.First).Frames == ScummCutsceneRun.EndFrames)}; elapsed {clock.ElapsedMilliseconds} ms");
