using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

// Usage: VisitProbe <game folder> <step>...
// A step is "R" (visit Room R from the catalog) or "R/O/V" (visit Room R, then
// activate object O's verb V, in hex). After each action, pending scripts are
// continued as a doorway click does in the app. Prints the
// session trace, reached events and uncertainties added by each step.
// "@x,y" puts the pointer over the room (assumed hover input), "@off" takes it
// off, and "verbs" prints the verb slots.
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
    Console.WriteLine($"== {label}: room {snapshot.RoomId:00}, pending {snapshot.PendingScripts}, camera x {session.CameraX?.ToString() ?? "unknown"}{(session.CameraEstimated ? " (estimate)" : "")}");
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
    // "?014,015": prints global words (hex numbers), or "unknown".
    if (step.StartsWith('?'))
    {
        foreach (var name in step[1..].Split(','))
        {
            var key = $"var[0x{Convert.ToInt32(name, 16):X3}]";
            Console.WriteLine($"   {key} = {(session.Snapshot.KnownGlobalVariables.TryGetValue(key, out var value) ? value.ToString() : "unknown")}");
        }
        continue;
    }
    // "sounds": lists the reached sound commands so far.
    if (step.StartsWith('@'))
    {
        // "@x,y" puts the pointer over the room (assumed hover input); "@off" takes it off.
        session.SetPointer(step == "@off" ? null : (int.Parse(step[1..].Split(',')[0]), int.Parse(step[1..].Split(',')[1])));
        Console.WriteLine($"== pointer {(session.Pointer is { } p ? $"({p.X}, {p.Y})" : "off")}");
        continue;
    }
    if (step.StartsWith('!'))
    {
        // "!x,y" sends a click to the game (assumed input); "!!x,y" a double click.
        var doubleClick = step.StartsWith("!!", StringComparison.Ordinal);
        var point = step.TrimStart('!').Split(',').Select(int.Parse).ToArray();
        var clicked = session.Click(point[0], point[1], doubleClick);
        Console.WriteLine($"   click: {clicked.Evidence}");
        Show($"{(doubleClick ? "double click" : "click")} at ({point[0]}, {point[1]})");
        continue;
    }
    if (step == "objects")
    {
        // "objects" lists the current room's objects: box, walk point and classes.
        var room = game.Rooms.Single(item => item.ResourceId == session.Snapshot.RoomId);
        foreach (var record in new SCUMMexplorer.Core.Rendering.LoomRoomObjectDirectory().Decode(File.ReadAllBytes(room.FilePath)))
            Console.WriteLine($"   object {record.Id}: ({record.X}, {record.Y}, {record.Width}, {record.Height}), walk ({record.WalkX}, {record.WalkY}), classes 0x{session.Snapshot.ObjectClasses.GetValueOrDefault(record.Id):X8}");
        continue;
    }
    if (step == "verbs")
    {
        foreach (var slot in session.Verbs) Console.WriteLine($"   {slot.Describe()}" + (slot.Estimate is null ? "" : $" (estimate: {slot.Estimate})") + (slot.Unknown is null ? "" : $" (unknown: {slot.Unknown})"));
        continue;
    }
    if (step.StartsWith("class", StringComparison.Ordinal))
    {
        // "class165,166" prints the class bits of objects.
        foreach (var id in step[5..].Split(',').Select(int.Parse))
            Console.WriteLine($"   object {id} classes 0x{session.Snapshot.ObjectClasses.GetValueOrDefault(id):X8}");
        continue;
    }
    if (step == "sounds")
    {
        foreach (var item in session.ReachedSoundEvents) Console.WriteLine("   sound: " + item.Format());
        continue;
    }
    // "+N": advances N frames.
    if (step.StartsWith('+'))
    {
        var startRoom = session.Snapshot.RoomId;
        for (var n = int.Parse(step[1..]); n > 0 && session.Snapshot.PendingScripts > 0; n--)
        {
            session.AdvanceFrame();
            if (session.Snapshot.RoomId != startRoom)
            {
                Console.WriteLine($"   entered Room {session.Snapshot.RoomId:00}: {session.LastAction?.Evidence}");
                startRoom = session.Snapshot.RoomId;
            }
        }
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
