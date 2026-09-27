using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

// Usage: VisitProbe <game folder> <step>...
// A step is "R" (visit Room R from the catalog) or "R/O/V" (visit Room R, then
// activate object O's verb V, in hex). After each action, pending scripts are
// continued as a doorway click does in the app. Prints the
// session trace, reached events and uncertainties added by each step.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));
var session = new LoomExplorationSession(game, index);
var uncertainties = 0;
var trace = 0;

void Show(string label)
{
    var snapshot = session.Snapshot;
    Console.WriteLine($"== {label}: room {snapshot.RoomId:00}, pending {snapshot.PendingScripts}");
    foreach (var line in session.SessionTrace.Skip(trace)) Console.WriteLine("   trace: " + line);
    foreach (var line in snapshot.Uncertainties.Skip(uncertainties)) Console.WriteLine("   uncertain: " + line);
    foreach (var actor in session.Actors.Where(a => a.Room == snapshot.RoomId || a.Unknown is not null && a.Number == 1))
        Console.WriteLine($"   actor {actor.Number}: room {actor.Room?.ToString() ?? "?"}, ({actor.X?.ToString() ?? "?"}, {actor.Y?.ToString() ?? "?"}), box {actor.WalkBox?.ToString() ?? "?"}, {(actor.Visible is bool v ? v ? "visible" : "hidden" : "visibility ?")}{(actor.Unknown is null ? "" : " unknown: " + actor.Unknown)}{(actor.Estimate is null ? "" : " estimate: " + actor.Estimate)}");
    trace = session.SessionTrace.Count;
    uncertainties = snapshot.Uncertainties.Count;
}

Show($"boot ({session.BootFrames} frames)");
foreach (var step in args.Skip(1))
{
    // "var023=2": sets a global word through the session's test seam.
    if (step.StartsWith("var", StringComparison.Ordinal) && step.Contains('='))
    {
        var (name, value) = (step[3..step.IndexOf('=')], ushort.Parse(step[(step.IndexOf('=') + 1)..]));
        typeof(LoomExplorationSession).GetMethod("SetGlobalWord", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(session, [Convert.ToInt32(name, 16), value]);
        Console.WriteLine($"== set var[0x{Convert.ToInt32(name, 16):X3}] = {value}");
        continue;
    }
    // "sounds": lists the reached sound commands so far.
    if (step == "sounds")
    {
        foreach (var item in session.ReachedSoundEvents) Console.WriteLine("   sound: " + item.Format());
        continue;
    }
    // "+N": advances N frames.
    if (step.StartsWith('+'))
    {
        for (var n = int.Parse(step[1..]); n > 0 && session.Snapshot.PendingScripts > 0; n--) session.AdvanceFrame();
        Show($"after {step} frames");
        continue;
    }
    var parts = step.Split('/');
    var roomId = int.Parse(parts[0]);
    if (session.Snapshot.RoomId != roomId)
    {
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == roomId));
        Show($"visit Room {roomId:00}");
    }
    if (parts.Length < 3) continue;
    var objectId = int.Parse(parts[1]);
    var verb = Convert.ToInt32(parts[2], 16);
    var action = session.ActivateObjectVerb(objectId, verb);
    Console.WriteLine($"   action: {action.Evidence}");
    Show($"object {objectId} verb 0x{verb:X2}");
    // As a doorway click: up to 32 frames outside a cutscene, 1000 inside one,
    // until the session has left the room and no cutscene is open.
    int frames = 0, cutsceneFrames = 0, frame = 0;
    while (session.Snapshot.PendingScripts > 0)
    {
        var inCutscene = session.InCutscene;
        if (!inCutscene && session.Snapshot.RoomId != roomId) break;
        if (inCutscene ? cutsceneFrames++ >= 1000 : frames++ >= 32) break;
        var before = session.Snapshot.RoomId;
        session.AdvanceFrame();
        Show($"frame {++frame}{(inCutscene ? " (cutscene)" : "")}");
        if (session.Snapshot.RoomId != before)
            Console.WriteLine($"   entered Room {session.Snapshot.RoomId:00}: {session.LastAction?.Evidence}");
    }
}
