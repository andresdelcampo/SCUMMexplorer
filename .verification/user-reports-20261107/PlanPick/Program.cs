using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: PlanPick <game folder> [source rooms, comma-separated; default every room]   every plan from each source: does it stand, does a click walk
//        PlanPick <game folder> walk <room> <x> <y> [frames]   one plan, one click, the hero frame by frame and the trace of what the click started
// 7 November 2026 (user: Indy 3's castle plans 20, 28, 32 "no longer show correctly, Indy won't move"): each plan picked as the app does from the session another
// picked room left (the app's PrepareManualRoom on _play), then a click on the plan.
var game = new GameFolderScanner().Scan(args[0]);
// PlanPick <game folder> first <room,room...>: how long each room's first-visit picture (the window's "Loading the scene") takes.
if (args.Length > 2 && args[1] == "first")
{
    foreach (var id in args[2].Split(',').Select(int.Parse))
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var scene = ScummRoomFirstVisit.Run(game, id);
        Console.WriteLine($"Room {id}: first visit in {clock.ElapsedMilliseconds} ms, {(scene is null ? "none" : $"hero {scene.State.Actors.FirstOrDefault(a => a.Number == 1)?.X},{scene.State.Actors.FirstOrDefault(a => a.Number == 1)?.Y}")}");
    }
    return;
}
// PlanPick <game folder> where <object>: the rooms whose object directory holds the object, with its verbs.
if (args.Length > 2 && args[1] == "where")
{
    var data = ScummDataSources.For(game);
    var id = int.Parse(args[2]);
    foreach (var room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        ScummRoomData read;
        try { read = data.ReadRoom(room); } catch (Exception) { continue; }
        if (read.Objects.FirstOrDefault(o => o.Id == id) is not { } record) continue;
        var verbs = data.ReadObjectVerbs(room, read.Objects).GetValueOrDefault(id) ?? [];
        Console.WriteLine($"Room {room}: verbs {string.Join(",", verbs.Select(v => $"0x{v.VerbId:X2}"))}");
    }
    return;
}
// PlanPick <game folder> classes <object,object...>: the objects' classes as the game's index gives them.
if (args.Length > 2 && args[1] == "classes")
{
    var indexed = ScummGameSession.BootForPlay(game, out _).Snapshot.ObjectClasses;
    foreach (var id in args[2].Split(',').Select(int.Parse))
        Console.WriteLine($"object {id}: classes [{string.Join(",", Enumerable.Range(1, 32).Where(c => (indexed.GetValueOrDefault(id) & (1u << (c - 1))) != 0))}]");
    return;
}
// PlanPick <game folder> scene <room> <object> <verb hex> <edit>...: one scene run as the planner runs it with the given setup (edits Kind:Target:Value[:Room],
// e.g. Bit:bit[0x5DF]:1 PlayedIn:68:18), then its result and the trace of the run (work item 1: scenes that need two values).
if (args.Length > 4 && args[1] == "scene")
{
    var planner = new ScummCutscenePlanner(game);
    var (room, obj, verb) = (int.Parse(args[2]), int.Parse(args[3]), Convert.ToInt32(args[4], 16));
    var trigger = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).First(t => t.Room == room && t.ObjectId == obj && t.VerbId == verb);
    var edits = args.Skip(5).Select(a => a.Split(':')).Select(p => new ScummSetupEdit(Enum.Parse<ScummSetupKind>(p[0]), p[1], int.Parse(p[2]), "PlanPick", p.Length > 3 ? int.Parse(p[3]) : null)).ToList();
    var runs = new List<ScummSession>();
    var result = ScummCutsceneRun.RunPicked(game, () => { var s = ScummGameSession.BootForPlay(game, out _); runs.Add(s); return s; }, trigger, edits);
    Console.WriteLine($"begun {result.Begun} way {result.Way} rooms [{string.Join(",", result.RoomsEntered)}] lines {result.Lines} frames {result.Frames} failure {result.Failure}");
    // The first run is the game's own way (the sentence); a later one the start at the cutscene.
    var boots = runs.FirstOrDefault()?.SessionTrace.ToList().FindIndex(l => l.StartsWith("boot:", StringComparison.Ordinal)) ?? -1;
    foreach (var line in runs.FirstOrDefault()?.SessionTrace.Skip(boots + 1).Where(l => !l.Contains("delay") && !l.Contains("Script 17")).Take(60) ?? []) Console.WriteLine("  | " + (line.Length > 170 ? line[..170] : line));
    foreach (var line in runs.FirstOrDefault()?.Snapshot.Uncertainties.Where(u => u.Contains("Script 02", StringComparison.Ordinal) || u.Contains("getDist", StringComparison.Ordinal)).TakeLast(8) ?? [])
        Console.WriteLine("  ? " + (line.Length > 400 ? line[..400] : line));
    if (runs.FirstOrDefault() is { } own)
    {
        var hero = own.Actors.First(a => a.Number == 1);
        Console.WriteLine($"  hero room {hero.Room} at ({hero.X},{hero.Y}); object {args[3]} owner {own.Snapshot.ObjectOwners.GetValueOrDefault(int.Parse(args[3]))} classes [{string.Join(",", Enumerable.Range(1, 32).Where(c => (own.Snapshot.ObjectClasses.GetValueOrDefault(int.Parse(args[3])) & (1u << (c - 1))) != 0))}]; messages: {string.Join(" / ", own.Messages.Log.TakeLast(3).Select(m => m.Text))}");
    }
    return;
}
var boot = ScummGameSession.BootForPlay(game, out _);
// PlanPick <game folder> walk <room> <x> <y> [frames] [after room]: with an after room, that room is picked first, clicked at (60, 110) and run until a cutscene
// begins (a guard catches Indy) and 200 frames more; the room is then picked from there, as the window does.
ScummSession start = boot;
if (args.Length > 6 && args[1] == "walk")
{
    // Several rooms, comma-separated, are picked one after another, each from the session the last one left.
    foreach (var before in args[6].Split(',').Select(int.Parse))
    {
        var first = start.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == before)).Session!;
        first.ClickScreen(60, 110, out _);
        var stepped = 0;
        while (!first.InCutscene && stepped++ < 600) first.StepFrame();
        for (var frame = 0; frame < (int.TryParse(Environment.GetEnvironmentVariable("PLANPICK_AFTER"), out var after) ? after : 200); frame++) first.StepFrame();
        Console.WriteLine($"after Room {before}: room {first.CurrentRoomId} cutscene {first.InCutscene} after {stepped} frames");
        start = first;
    }
}
if (args.Length > 2 && args[1] == "walk")
{
    var prepared = start.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == int.Parse(args[2])));
    if (prepared.Session is not { } walking) { Console.WriteLine("REFUSED: " + prepared.Reason); return; }
    var hero0 = walking.Actors.First(a => a.Number == 1);
    Console.WriteLine($"room {walking.CurrentRoomId} hero costume {hero0.Look.Costume} at ({hero0.X},{hero0.Y}) box {hero0.WalkBox} visible {hero0.Visible}; draw {string.Join(",", walking.Snapshot.DrawOrder)}");
    foreach (var a in walking.Actors.Where(a => a.Room == walking.CurrentRoomId)) Console.WriteLine($"  actor {a.Number} costume {a.Look.Costume} at ({a.X},{a.Y}) box {a.WalkBox}");
    var mark = walking.SessionTrace.Count;
    var delivered = walking.ClickScreen(int.Parse(args[3]), int.Parse(args[4]), out var refused);
    Console.WriteLine(delivered is null ? "click refused: " + refused : "click: " + delivered.Description);
    var frames = args.Length > 5 ? int.Parse(args[5]) : 90;
    (int?, int?, int?) last = default;
    for (var frame = 1; frame <= frames; frame++)
    {
        walking.StepFrame();
        var hero = walking.Actors.FirstOrDefault(a => a.Number == 1);
        var now = (hero?.X, hero?.Y, (int?)walking.CurrentRoomId);
        if (now != last) Console.WriteLine($"  frame {frame}: room {walking.CurrentRoomId} hero ({hero?.X},{hero?.Y}) box {hero?.WalkBox} cutscene {walking.InCutscene}");
        last = now;
    }
    foreach (var line in walking.SessionTrace.Skip(mark).Where(l => !l.Contains("delay") && !l.Contains("Script 25")).Take(50)) Console.WriteLine("  | " + (line.Length > 170 ? line[..170] : line));
    return;
}
var plans = new[] { 20, 28, 32 };
var sources = args.Length > 1 ? args[1].Split(',').Select(int.Parse).ToArray() : game.Rooms.Select(r => r.ResourceId!.Value).ToArray();
var counts = plans.ToDictionary(p => p, _ => (Live: 0, Walked: 0, Tried: 0));

void Try(ScummSession from, string label)
{
    foreach (var plan in plans)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var prepared = from.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == plan));
        var c = counts[plan]; c.Tried++;
        if (prepared.Session is not { } session) { counts[plan] = c; Console.WriteLine($"{label} -> {plan}: REFUSED in {clock.ElapsedMilliseconds} ms: {prepared.Reason}"); continue; }
        c.Live++;
        var hero = session.Actors.First(a => a.Number == 1);
        var start = (hero.X, hero.Y);
        string click;
        var delivered = session.ClickScreen(start.X == 160 ? 100 : 160, 100, out var refused);
        click = delivered is null ? "click refused: " + refused : "clicked";
        for (var frame = 0; frame < 60; frame++) session.StepFrame();
        var after = session.Actors.FirstOrDefault(a => a.Number == 1);
        var walked = session.CurrentRoomId == plan && (after?.X, after?.Y) != start;
        if (walked) c.Walked++;
        counts[plan] = c;
        Console.WriteLine($"{label} -> {plan}: live in {clock.ElapsedMilliseconds} ms, costume {hero.Look.Costume} at {start}, {click}, now room {session.CurrentRoomId} at ({after?.X},{after?.Y}) box {after?.WalkBox} cutscene {session.InCutscene}{(walked ? "" : "  NOT WALKED")}");
    }
}

Try(boot, "boot");
foreach (var source in sources)
{
    var left = boot.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == source));
    if (left.Session is null) { Console.WriteLine($"{source}: still ({left.Reason})"); continue; }
    Try(left.Session, source.ToString());
}
foreach (var (plan, c) in counts)
    Console.WriteLine($"Room {plan}: live {100.0 * c.Live / Math.Max(1, c.Tried):F1}%, walked {100.0 * c.Walked / Math.Max(1, c.Tried):F1}% of {c.Tried} picks");
