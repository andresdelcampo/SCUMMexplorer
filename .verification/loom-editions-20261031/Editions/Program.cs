using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// Usage: Editions <LOOM folder> [max groups]
// 31 October 2026 (ROADMAP 2b, "one scene engine"): LOOM's scanned scenes (the list every edition but the English reference shows) played two ways.
// LOOM's own path: a fresh run (LoomCutsceneRun), else LoomCdSceneSetupPlanner's derived start. The shared engine: ScummCutscenePlanner.PlanSetup
// (the path Indy 3, Monkey Island and Indy 4 use). Counts: begins, how, and "shows nothing" (started at its cutscene, no line, no other room).
var game = new GameFolderScanner().Scan(args[0]);
var max = args.Length > 1 ? int.Parse(args[1]) : int.MaxValue;
var clock = Stopwatch.StartNew();
var data = ScummDataSources.For(game);
var triggers = LoomCutsceneVerbs.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value).ToList(), LoomCutsceneTitles.Doorways(game), game);
var groups = ScummCutsceneIndex.Group(triggers).Take(max).ToList();
var loom = new LoomCdSceneSetupPlanner(game, triggers, data);
var snapshot = game.V4Archive is { } vga ? vga.ReadIndexedObjects() : new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
// EDITIONS_LOOM_SESSION=1: the shared planner runs on LOOM's own session (as the app plays LOOM), not on ScummGameSession.
var onLoom = Environment.GetEnvironmentVariable("EDITIONS_LOOM_SESSION") == "1";
var shared = onLoom
    ? new ScummCutscenePlanner(game, game.V4Archive is { } cd ? () => new LoomExplorationSession(game, cd.ReadIndexedObjects(), completeBoot: false) : () => new LoomExplorationSession(game, snapshot))
    : new ScummCutscenePlanner(game);
var sharedTriggers = shared.Triggers.GroupBy(t => (t.Room, t.ObjectId, t.VerbId)).ToDictionary(g => g.Key, g => g.First());
if (Environment.GetEnvironmentVariable("EDITIONS_TRACE") is { Length: > 0 } spec)
{
    // EDITIONS_TRACE=room,object,verb: LOOM's derived start and the shared engine's candidates, each tried alone.
    var p = spec.Split(',').Select(int.Parse).ToArray();
    var t = triggers.First(x => x.Room == p[0] && x.ObjectId == p[1] && x.VerbId == p[2]);
    var d = loom.Derive(t);
    Console.WriteLine($"LOOM: plays {d.Plays}; before [{string.Join(" | ", d.Before.Select(b => $"{b.Kind} {b.Room}/{b.ObjectId}/0x{b.VerbId:X2} {b.Where}"))}] after [{string.Join(" | ", d.After.Select(b => $"{b.Kind} {b.Room}/{b.ObjectId}/0x{b.VerbId:X2} {b.Where}"))}]");
    foreach (var note in d.Notes) Console.WriteLine("  " + note);
    var mineT = shared.Triggers.First(x => x.Room == p[0] && x.ObjectId == p[1] && x.VerbId == p[2]);
    var fresh = shared.Run(mineT);
    Console.WriteLine($"shared fresh: begun {fresh.Begun} way {fresh.Way} lines {fresh.Lines} rooms [{string.Join(",", fresh.RoomsEntered)}] frames {fresh.Frames} {fresh.Failure}");
    if (Environment.GetEnvironmentVariable("EDITIONS_BOX") is { Length: > 0 } boxSpec)
    {
        var sess = ScummGameSession.BootForPlay(game, out _);
        sess.VisitRoom(game.Rooms.First(r => r.ResourceId == p[0]));
        var a1 = sess.Actors.First(a => a.Number == 1);
        Console.WriteLine($"after visit: ego room {a1.Room} at ({a1.X},{a1.Y}) box {a1.WalkBox}, cutscene {sess.InCutscene}, pending [{string.Join(", ", sess.PendingScriptSummary)}]");
        var applied = sess.ApplySceneSetup([new(ScummSetupKind.WalkBox, "1", int.Parse(boxSpec), "trace", p[0])]);
        for (var f = 0; f < 200; f++)
        {
            var a = sess.Actors.First(x => x.Number == 1);
            if (f % 20 == 0) Console.WriteLine($"  frame {f}: applied {applied.Count} ego ({a.X},{a.Y}) box {a.WalkBox} room {a.Room}, cutscene {sess.InCutscene}, pending [{string.Join(", ", sess.PendingScriptSummary)}]");
            sess.StepFrame();
        }
        foreach (var l in sess.SessionTrace.TakeLast(15)) Console.WriteLine("    " + l);
        return;
    }
    foreach (var c in shared.Setup.Candidates(mineT))
    {
        var r1 = ScummCutsceneRun.Run(game, () => ScummGameSession.BootForPlay(game, out _), mineT, [c]);
        Console.WriteLine($"  trial {c.Describe()}: begun {r1.Begun} lines {r1.Lines} rooms [{string.Join(",", r1.RoomsEntered)}] frames {r1.Frames} {r1.Failure}");
    }
    return;
}
Console.WriteLine($"{game.Title} {game.Edition}: {groups.Count} LOOM groups, shared scan {shared.Triggers.Count} triggers; {clock.Elapsed.TotalSeconds:F0} s");
int loomFresh = 0, loomSetup = 0, loomNone = 0, notInShared = 0, own = 0, setup = 0, atCut = 0, none = 0, nothing = 0;
var lines = new List<string>();
foreach (var group in groups)
{
    var t = group.First;
    var key = $"Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} ({t.ObjectName})";
    string loomWay;
    if (game.V4Archive is null && LoomCutsceneRun.VisitedRooms(game, snapshot, t) is not null) { loomFresh++; loomWay = "fresh"; }
    else if (loom.Derive(t) is { Plays: true } s) { if (s.HasSteps) loomSetup++; else loomFresh++; loomWay = s.HasSteps ? "setup" : "fresh"; }
    else { loomNone++; loomWay = "none"; }
    if (!sharedTriggers.TryGetValue((t.Room, t.ObjectId, t.VerbId), out var mine)) { notInShared++; lines.Add($"NOT-IN-SHARED loom {loomWay} | {key}"); continue; }
    var plan = shared.PlanSetup(mine);
    var r = plan.Result;
    var way = !r.Begun ? "none" : r.Way == ScummPickedStart.AtCutscene ? "at-cutscene" : plan.HasEdits ? "setup" : "own";
    switch (way) { case "own": own++; break; case "setup": setup++; break; case "at-cutscene": atCut++; break; default: none++; break; }
    var showsNothing = !r.Begun || r.Way == ScummPickedStart.AtCutscene && r.Lines == 0 && r.RoomsEntered.All(x => x == t.Room);
    if (showsNothing) nothing++;
    if (way != "own" || loomWay != "fresh")
        lines.Add($"{way,-11} loom {loomWay,-5} lines {r.Lines} rooms [{string.Join(",", r.RoomsEntered.Where(x => x != t.Room))}] frames {r.Frames}{(showsNothing ? " NOTHING" : "")} | {key}" +
                  (plan.HasEdits ? $" | setup: {string.Join("; ", plan.Edits.Select(e => e.Describe()))}" : ""));
}
foreach (var line in lines) Console.WriteLine(line);
string P(int k) => $"{k} ({100.0 * k / Math.Max(1, groups.Count):F1}%)";
Console.WriteLine($"SUMMARY{(onLoom ? " (LOOM session)" : "")} {Path.GetFileName(args[0].TrimEnd('\\', '/'))}: groups {groups.Count}; LOOM path begins {P(loomFresh + loomSetup)} (fresh {loomFresh}, setup {loomSetup}, none {loomNone}); " +
                  $"shared begins {P(own + setup + atCut)} (own {P(own)}, setup {P(setup)}, at cutscene {P(atCut)}, none {none}), shows nothing {P(nothing)}, not in shared scan {notInShared}; {clock.Elapsed.TotalSeconds:F0} s");
