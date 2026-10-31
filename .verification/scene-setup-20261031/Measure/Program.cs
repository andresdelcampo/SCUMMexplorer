using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: Measure <game folder> [max groups] [filter room]
// 31 October 2026 (REQUIREMENTS §8.6): every scan group's first trigger run from a fresh start, then the setup search for those a fresh start does not begin.
var game = new GameFolderScanner().Scan(args[0]);
var limit = args.Length > 1 ? int.Parse(args[1]) : int.MaxValue;
int? onlyRoom = args.Length > 2 ? int.Parse(args[2]) : null;
var clock = Stopwatch.StartNew();
var planner = new ScummCutscenePlanner(game);
Console.WriteLine($"{planner.Groups.Count} groups, planner ready in {clock.Elapsed.TotalSeconds:F1} s");
if (Environment.GetEnvironmentVariable("MEASURE_VERBS") == "1")
{
    Console.WriteLine($"verb source: {planner.VerbSource}");
    foreach (var v in Enumerable.Range(1, 0x80)) if (planner.Verbs.Word?.Invoke(v) is { } w) Console.WriteLine($"  verb 0x{v:X2} = {w}");
    var boot = ScummGameSession.BootForPlay(game, out _);
    ScummCutscenePlanner.UntilAVerbIsOn(boot);
    foreach (var verb in boot.Verbs.Take(20)) Console.WriteLine($"  session verb 0x{verb.VerbId:X2} mode {verb.Mode} name [{verb.Name}] image {verb.ImageObject}");
    return;
}
int fresh = 0, setup = 0, still = 0, n = 0;
foreach (var group in planner.Groups)
{
    var t = group.First;
    if (onlyRoom is int r && t.Room != r) continue;
    if (n++ >= limit) break;
    var run = planner.Run(t);
    if (Environment.GetEnvironmentVariable("MEASURE_DIRECT") == "1")
    {
        var d = ScummCutsceneRun.Run(game, () => ScummGameSession.BootForPlay(game, out _), t, null, ScummPickedStart.Direct);
        if (d.Begun != run.Begun) Console.WriteLine($"DIFF Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2}: direct {d.Begun} ({d.Failure}), picked {run.Begun} way={run.Way} ({run.Failure})");
        if (run.Begun) fresh++;
        continue;
    }
    if (run.Begun) { fresh++; continue; }
    var start = clock.Elapsed;
    var candidates = planner.Setup.Candidates(t);
    if (Environment.GetEnvironmentVariable("MEASURE_TRIALS") == "1")
    {
        Console.WriteLine($"  fresh: begun {run.Begun} rooms [{string.Join(",", run.RoomsEntered)}] lines {run.Lines} frames {run.Frames} failure {run.Failure}");
        foreach (var c in candidates)
        {
            var r1 = ScummCutsceneRun.Run(game, () => ScummGameSession.BootForPlay(game, out _), t, [c]);
            Console.WriteLine($"  trial {c.Describe()}: begun {r1.Begun} rooms [{string.Join(",", r1.RoomsEntered)}] lines {r1.Lines} frames {r1.Frames} failure {r1.Failure}");
        }
    }
    var plan = planner.PlanSetup(t);
    var took = (clock.Elapsed - start).TotalSeconds;
    if (plan.Result.Begun) setup++; else still++;
    Console.WriteLine($"{(plan.Result.Begun ? (plan.Result.Way == ScummPickedStart.AtCutscene ? "ATCUT" : "SETUP") : "STILL")} Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} ({t.ObjectName}) {took:F1} s, {candidates.Count} candidates: " +
                      $"{string.Join(" | ", candidates.Select(c => c.Describe()))}");
    Console.WriteLine($"      result: way {plan.Result.Way} lines {plan.Result.Lines} rooms [{string.Join(",", plan.Result.RoomsEntered)}] frames {plan.Result.Frames}");
    if (plan.HasEdits) Console.WriteLine($"      kept: {string.Join("; ", plan.Edits.Select(e => e.Describe()))}; rooms [{string.Join(",", plan.Result.RoomsEntered)}] lines {plan.Result.Lines}");
}
Console.WriteLine($"fresh start begins {fresh}; setup begins {setup}; still nothing {still}; {clock.Elapsed.TotalSeconds:F0} s");
