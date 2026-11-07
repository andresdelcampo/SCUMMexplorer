using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: Compare <LOOM English Roland folder> <loom-replays.json> [max]
// 31 October 2026 (ROADMAP "one scene engine"): LOOM English's 328 reviewed replays as the answer key for the shared scene engine. For each replay, the
// same trigger (room, object, verb) in the shared engine's scan, its plan (ScummCutscenePlanner.PlanSetup), and the comparison: found, begins, how, and
// the rooms reached against the rooms the catalog recorded.
var game = new GameFolderScanner().Scan(args[0]);
var catalog = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.GetProperty("replays");
var max = args.Length > 2 ? int.Parse(args[2]) : int.MaxValue;
if (Environment.GetEnvironmentVariable("COMPARE_TRACE") is { Length: > 0 } spec)
{
    // COMPARE_TRACE=room,object,verb: one picked run, with the room loads and the boot's pending scripts.
    var p = spec.Split(',').Select(int.Parse).ToArray();
    var session = ScummGameSession.BootForPlay(game, out _);
    Console.WriteLine($"after boot: room {session.CurrentRoomId}, input {session.UserInputOn}, cutscene {session.InCutscene}, pending [{string.Join(", ", session.PendingScriptSummary)}]");
    session.VisitRoom(game.Rooms.First(r => r.ResourceId == p[0]));
    Console.WriteLine($"after visit: room {session.CurrentRoomId}, pending [{string.Join(", ", session.PendingScriptSummary)}]");
    var mark = session.SessionTrace.Count;
    session.PickInteraction(p[1], p[2]);
    for (var f = 0; f < 600 && session.CurrentRoomId == p[0]; f++)
    {
        session.StepFrame();
        // COMPARE_TRACE_EGO=1: the hero's place each frame it changes (7 November: what starts Room 23's meeting after a reaction)
        if (Environment.GetEnvironmentVariable("COMPARE_TRACE_EGO") == "1" && session.Actors.FirstOrDefault(a => a.Number == 1) is { } ego)
            Console.WriteLine($"frame {f}: ego ({ego.X}, {ego.Y}) box {ego.WalkBox} room {ego.Room}, cutscene {session.InCutscene}, lines {session.SessionTrace.Count}");
    }
    Console.WriteLine($"now: room {session.CurrentRoomId}");
    foreach (var line in session.SessionTrace.Skip(mark).Where(l => Environment.GetEnvironmentVariable("COMPARE_TRACE_ALL") == "1" || l.Contains("load", StringComparison.OrdinalIgnoreCase) || l.Contains("Room 02") || l.StartsWith("picked") || l.Contains("sentence")).Take(60)) Console.WriteLine("  " + line);
    return;
}
// COMPARE_LOOM_SESSION=1: the shared planner runs on LOOM's own session (LoomExplorationSession), as the app would play LOOM through it.
var onLoom = Environment.GetEnvironmentVariable("COMPARE_LOOM_SESSION") == "1";
var lflSnapshot = onLoom ? new SCUMMexplorer.Core.Scumm.LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")) : null;
var planner = onLoom ? new ScummCutscenePlanner(game, () => new SCUMMexplorer.Core.Loom.LoomExplorationSession(game, lflSnapshot!)) : new ScummCutscenePlanner(game);
var triggers = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others)).GroupBy(t => (t.Room, t.ObjectId, t.VerbId)).ToDictionary(g => g.Key, g => g.First());
var groupOf = planner.Groups.SelectMany(g => new[] { g.First }.Concat(g.Others).Select(t => ((t.Room, t.ObjectId, t.VerbId), g))).GroupBy(p => p.Item1).ToDictionary(p => p.Key, p => p.First().g);
// Every trigger the scan saw, shared helpers included (Group leaves those out), for telling "left out as a shared helper" from "not found".
var all = planner.Triggers.Select(t => (t.Room, t.ObjectId, t.VerbId)).ToHashSet();
var data = ScummDataSources.For(game);
if (Environment.GetEnvironmentVariable("COMPARE_ONE") is { Length: > 0 } one)
{
    // COMPARE_ONE=room,object,verb (7 November 2026): the planner's run of one replay, its setup replayed in a session kept for reading: the session's
    // cutscene, load and chain lines and the lines it printed.
    var q = one.Split(',').Select(int.Parse).ToArray();
    var picked = triggers[(q[0], q[1], q[2])];
    var onePlan = planner.PlanSetup(picked);
    Console.WriteLine($"plan: way {onePlan.Result.Way} frames {onePlan.Result.Frames} lines {onePlan.Result.Lines} rooms [{string.Join(",", onePlan.Result.RoomsEntered)}] setup: {string.Join("; ", onePlan.Edits.Select(e => e.Describe()))}");
    ScummSession? kept = null;
    var again = ScummCutsceneRun.Run(game, () => kept = onLoom ? new SCUMMexplorer.Core.Loom.LoomExplorationSession(game, lflSnapshot!) : ScummGameSession.BootForPlay(game, out _),
        picked, onePlan.Edits, onePlan.Result.Way);
    Console.WriteLine($"again: frames {again.Frames} lines {again.Lines}");
    foreach (var line in kept!.SessionTrace.Where(l => l.StartsWith("cutscene at") || l.StartsWith("endCutscene") || l.Contains("chains") || l.Contains("load", StringComparison.OrdinalIgnoreCase) || l.StartsWith("picked")).TakeLast(40))
        Console.WriteLine("  " + line);
    foreach (var line in kept.Messages.Log.TakeLast(12)) Console.WriteLine("  said: " + line);
    return;
}
int catSame = 0, helpers = 0, sharedCode = 0;
var thresholds = new[] { 60, 100, 150, 200, 300, 400, 600 };
var runRule = new int[thresholds.Length];
var catLog = new List<string>();
var frameLog = new List<string>();
int n = 0, found = 0, begun = 0, roomsSame = 0, roomsMore = 0, roomsFewer = 0;
var ways = new Dictionary<string, int>();
foreach (var replay in catalog.EnumerateArray())
{
    if (n++ >= max) break;
    var id = replay.GetProperty("id").GetString()!;
    var room = replay.GetProperty("room").GetInt32();
    var obj = replay.TryGetProperty("objectId", out var o) ? o.GetInt32() : 0;
    var verb = replay.TryGetProperty("verb", out var v) ? v.GetInt32() : 0;
    var expected = replay.GetProperty("rooms").EnumerateArray().Select(r => r.GetInt32()).Where(r => r != room).ToHashSet();
    if (!triggers.TryGetValue((room, obj, verb), out var trigger))
    {
        // The scan names an object's verbs that share code after the lowest of them (NamesSharedCode): the same code under another verb.
        var record = obj == 0 ? null : data.ReadRoom(room).Objects.FirstOrDefault(r => r.Id == obj);
        var table = record is null ? null : data.ReadObjectVerbs(room, [record]).GetValueOrDefault(obj);
        var mine = table?.FirstOrDefault(e => e.VerbId == verb);
        int? twin = mine is null ? null : table!.Where(e => e.ScriptOffset == mine.ScriptOffset && e.VerbId != verb).Select(e => (int?)e.VerbId).FirstOrDefault(w => triggers.ContainsKey((room, obj, w!.Value)));
        if (twin is int w && w != 0) { sharedCode++; Console.WriteLine($"SHARED-CODE {id} as verb 0x{w:X2} | {replay.GetProperty("title").GetString()}"); continue; }
        if (all.Contains((room, obj, verb))) { helpers++; Console.WriteLine($"HELPER {id} | {replay.GetProperty("title").GetString()}"); continue; }
        Console.WriteLine($"MISSING {id} | {replay.GetProperty("title").GetString()}"); continue;
    }
    found++;
    var plan = planner.PlanSetup(trigger);
    var got = plan.Result.RoomsEntered.Where(r => r != room).ToHashSet();
    var way = !plan.Result.Begun ? "none" : plan.Result.Way == ScummPickedStart.AtCutscene ? "at-cutscene" : plan.HasEdits ? "setup" : "own";
    ways[way] = ways.GetValueOrDefault(way) + 1;
    if (plan.Result.Begun) begun++;
    var match = got.SetEquals(expected) ? "same" : got.IsSupersetOf(expected) ? "more" : "fewer";
    var category = planner.IsScene(groupOf[(room, obj, verb)]) ? "Scenes" : "Dialogue and reactions";
    var expectedCategory = replay.GetProperty("category").GetString();
    if (category == expectedCategory) catSame++;
    else Console.WriteLine($"CATEGORY {id}: catalog {expectedCategory}, engine {category} | {replay.GetProperty("title").GetString()}");
    // The catalog's rule (R-8.9) applied to the run: another room reached, or a long silent animation (no line, at least T frames).
    for (var t = 0; t < thresholds.Length; t++)
        if ((plan.Result.RoomsEntered.Any(r => r != room) || plan.Result.Lines == 0 && plan.Result.Frames >= thresholds[t] ? "Scenes" : "Dialogue and reactions") == expectedCategory) runRule[t]++;
    var catalogFrames = replay.GetProperty("frames").GetInt32();
    frameLog.Add($"{catalogFrames}	{plan.Result.Frames}	{plan.Result.Lines}	{id}");
    catLog.Add($"{expectedCategory}	{plan.Result.Lines}	{plan.Result.Frames}	{plan.Result.RoomsEntered.Count}	{id}");
    if (match == "same") roomsSame++; else if (match == "more") roomsMore++; else roomsFewer++;
    if (way != "own" || match != "same")
        Console.WriteLine($"{way,-11} rooms {match,-5} {id} | catalog [{string.Join(",", expected.Order())}] engine [{string.Join(",", got.Order())}] lines {plan.Result.Lines} | {replay.GetProperty("title").GetString()}" +
                          (plan.HasEdits ? $" | setup: {string.Join("; ", plan.Edits.Select(e => e.Describe()))}" : ""));
}
Console.WriteLine("run-based category agreement by silent-frames threshold: " + string.Join(", ", thresholds.Select((t, i) => $"{t}: {runRule[i]}")));
File.WriteAllLines(onLoom ? "../categories-loom-session.tsv" : "../categories.tsv", catLog);
File.WriteAllLines(onLoom ? "../frames-loom-session.tsv" : "../frames.tsv", frameLog);
Console.WriteLine($"replays {Math.Min(n, max)}: found {found}, begin {begun}; " + string.Join(", ", ways.OrderBy(w => w.Key).Select(w => $"{w.Key} {w.Value}")) +
                  $"; rooms same {roomsSame}, more {roomsMore}, fewer {roomsFewer}; category same {catSame}; not grouped: shared helper {helpers}, shared code {sharedCode}");
