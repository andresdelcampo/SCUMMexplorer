using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: Tour <game folder> [frames after the pick, default 900]
// 6 November 2026 (user: "we should not play full room entry cutscenes on entry"; the hero's shape in picked rooms): every room picked from the list as the
// app does (a copy of the boot, PrepareManualRoom, else the still preview's first-visit picture). For each room: live or still, the hero in the picture
// (in the room, costume), and for a live room what happens by itself in the next frames (a cutscene begun, lines printed, the room left).
var game = new GameFolderScanner().Scan(args[0]);
var frames = args.Length > 1 ? int.Parse(args[1]) : 900;
var boot = ScummGameSession.BootForPlay(game, out _);
ScummCutscenePlanner.IntoControl(boot);
var snapshot = ScummBootSnapshot.Of(boot);
// TOUR_WALK=room: the room entered as picked, the hero sent to the middle of each walk box in turn; what the room's scripts have drawn at each (the castle plans).
if (Environment.GetEnvironmentVariable("TOUR_WALK") is { Length: > 0 } walkRoom)
{
    var session = (ScummGameSession)snapshot.Start();
    var target = game.Rooms.First(r => r.ResourceId == int.Parse(walkRoom));
    session.VisitRoomAsPicked(target);
    // TOUR_STOP=57,58: those global scripts' slots end (the castle's guards, whose confrontation would otherwise take the hero away).
    var stopped = (Environment.GetEnvironmentVariable("TOUR_STOP") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
    var pending = (System.Collections.IList)typeof(ScummSession).GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
    var frameScripts = (System.Collections.IList)typeof(ScummSession).GetField("_frameScripts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
    string Alive() => string.Join(" ", pending.Cast<object>().Concat(frameScripts.Cast<object>()).Select(p => $"{p.GetType().GetProperty("ScriptNumber")?.GetValue(p)}+{p.GetType().GetProperty("Offset")?.GetValue(p):X}"));
    Console.WriteLine($"after the visit: {Alive()}");
    void Stop()
    {
        foreach (var item in pending.Cast<object>().ToArray())
            if (item.GetType().GetProperty("ScriptNumber")?.GetValue(item) is int number && stopped.Contains(number)) pending.Remove(item);
    }
    for (var i = 0; i < 60; i++)
    {
        Stop();
        session.StepFrame();
        if (i < 2) Console.WriteLine($"frame {i}: {Alive()} | {string.Join(" ; ", session.Snapshot.Uncertainties.TakeLast(6))}");
    }
    foreach (var line in session.SessionTrace.Where(l => l.Contains("Script 25") || l.Contains("stopScript") || l.Contains("script 25")).TakeLast(8)) Console.WriteLine("  trace: " + line);
    var boxes = session.CurrentWalkBoxes;
    Console.WriteLine($"boxes {boxes?.Boxes.Count}, input {session.UserInputOn}, room {session.CurrentRoomId}, screen {session.MainScreen}");
    string Report()
    {
        var hero = session.Actors.First(a => a.Number == 1);
        var active = session.Snapshot.ObjectStates.Where(s => s.Value != 0 && session.Snapshot.ObjectPositions.ContainsKey(s.Key)).Select(s => $"{s.Key}:{s.Value}");
        var running = pending.Cast<object>().Select(p => $"{p.GetType().GetProperty("ScriptNumber")?.GetValue(p)}+{p.GetType().GetProperty("Offset")?.GetValue(p):X}");
        return $"room {session.CurrentRoomId} hero ({hero.X},{hero.Y}) box {hero.WalkBox}; queue {string.Join(",", session.Snapshot.DrawOrder.TakeLast(6))} | pending {string.Join(" ", running)} | var B8 {session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x0B8]")} B9 {session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x0B9]")}";
    }
    Console.WriteLine(Report());
    for (var box = 0; box < (boxes?.Boxes.Count ?? 0); box++)
    {
        if (boxes!.Coordinates(box) is not { } shape) continue;
        var (x, y) = (shape.Corners.Sum(c => c.X) / 4, shape.Corners.Sum(c => c.Y) / 4);
        var input = session.ClickScreen(x - session.ViewLeft, y + session.MainScreen.Top, out var refused);
        for (var i = 0; i < 300; i++) session.StepFrame();
        Console.WriteLine($"-> box {box} middle ({x},{y}) {(input is null ? "refused " + refused : "")}: {Report()}");
        if (session.CurrentRoomId != int.Parse(walkRoom)) break;
    }
    return;
}
int rooms = 0, live = 0, begins = 0, speaks = 0, leaves = 0, noHero = 0, viewers = 0;
// The scripts the engine ended because it could not go on (the class of the castle plans' drawObject 0), with the rooms they ended in.
var died = new Dictionary<string, HashSet<int>>();
void NoteDead(int room, IEnumerable<string> uncertainties)
{
    foreach (var line in uncertainties)
        if (System.Text.RegularExpressions.Regex.IsMatch(line, @" at \+0x[0-9A-F]{4}$") && !line.Contains("getRandomNr"))
        {
            var key = System.Text.RegularExpressions.Regex.Replace(line, @"^(?:Script \d+|Room \d+ [^:]*) \+0x[0-9A-F]+: ", "");
            key = System.Text.RegularExpressions.Regex.Replace(key, @"0x[0-9A-F]+|\d+", "N");
            (died.TryGetValue(key, out var set) ? set : died[key] = []).Add(room);
        }
}
foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).OrderBy(r => r.ResourceId))
{
    var id = room.ResourceId!.Value;
    rooms++;
    string line;
    try
    {
        var source = (ScummGameSession)snapshot.Start();
        var prepared = source.PrepareManualRoom(room);
        var picture = ScummRoomFirstVisit.Run(game, id);
        var hero = picture.State.Actors.FirstOrDefault(a => a.Number == picture.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x001]") && a.Room == id);
        NoteDead(id, picture.Snapshot.Uncertainties);
        var heroText = hero is null ? "no hero" : $"hero c{hero.Look.Costume} ({hero.X},{hero.Y}) scale {hero.Look.Scale}";
        if (hero is null) noHero++;
        if (prepared.Session is not ScummGameSession ready)
        {
            line = $"still | {heroText} | {prepared.Reason}";
        }
        else
        {
            live++;
            var liveHero = ready.Actors.FirstOrDefault(a => a.Number == ready.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x001]") && a.Room == id);
            var viewer = ready.ShownOnlyBy(id);
            if (viewer is not null) viewers++;
            var began = ready.CutscenesBegun;
            var printed = ready.Messages.Log.Count;
            int? firstCutscene = null, leftAt = null;
            for (var frame = 1; frame <= frames; frame++)
            {
                ready.StepFrame();
                if (firstCutscene is null && ready.CutscenesBegun > began) firstCutscene = frame;
                if (leftAt is null && ready.CurrentRoomId != id) leftAt = frame;
            }
            NoteDead(id, ready.Snapshot.Uncertainties);
            var lines = ready.Messages.Log.Count - printed;
            if (firstCutscene is not null) begins++;
            if (lines > 0) speaks++;
            if (leftAt is not null) leaves++;
            line = $"live | {heroText} | live hero {(liveHero is null ? "none" : $"c{liveHero.Look.Costume} ({liveHero.X},{liveHero.Y})")}" +
                   (viewer is null ? "" : $" | shown by {viewer}") +
                   (firstCutscene is null ? "" : $" | CUTSCENE at frame {firstCutscene}") + (lines == 0 ? "" : $" | LINES {lines}") + (leftAt is null ? "" : $" | LEFT at frame {leftAt}");
        }
    }
    catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or NotSupportedException or ArgumentException)
    {
        line = $"failed | {ex.Message}";
    }
    Console.WriteLine($"Room {id:000} | {line}");
}
foreach (var (key, set) in died.OrderByDescending(p => p.Value.Count)) Console.WriteLine($"ENDED in {set.Count} room(s) [{string.Join(",", set.Order().Take(6))}]: {key}");
static string Percent(int part, int all) => all == 0 ? "-" : $"{100.0 * part / all:F1}%";
Console.WriteLine($"TOTAL rooms {rooms}, live {live} ({Percent(live, rooms)}), still {rooms - live}; after the pick in live rooms: cutscene {begins} ({Percent(begins, live)}), lines {speaks} ({Percent(speaks, live)}), left {leaves} ({Percent(leaves, live)}); " +
                  $"no hero in the room's picture {noHero} ({Percent(noHero, rooms)}); shown through a script {viewers}");
