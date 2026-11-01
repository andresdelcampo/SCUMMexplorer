using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

// Usage: Measure <game folder> [max groups] [filter room]
// 31 October 2026 (REQUIREMENTS §8.6): every scan group's first trigger run from a fresh start, then the setup search for those a fresh start does not begin.
var game = new GameFolderScanner().Scan(args[0]);
var limit = args.Length > 1 ? int.Parse(args[1]) : int.MaxValue;
int? onlyRoom = args.Length > 2 ? int.Parse(args[2]) : null;
var clock = Stopwatch.StartNew();
// Maniac Mansion starts its scenes after the reviewed party selection, as the app does (ManiacCutsceneSetup, 1 November 2026).
var maniac = GameSupport.FamilyOf(game) is GameFamily.ManiacClassic or GameFamily.ManiacEnhanced;
var planner = maniac ? SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.Planner(game) : new ScummCutscenePlanner(game);
Func<ScummSession> newRun = maniac ? () => SCUMMexplorer.Core.Maniac.ManiacCutsceneSetup.NewSession(game) : () => ScummGameSession.BootForPlay(game, out _);
Console.WriteLine($"{planner.Groups.Count} groups, planner ready in {clock.Elapsed.TotalSeconds:F1} s");
if (Environment.GetEnvironmentVariable("MEASURE_OBJECT_CODE") is { Length: > 0 } code)
{
    // MEASURE_OBJECT_CODE=room,object,verb(hex): the verb code as the data source decodes it for the planner.
    var p = code.Split(',');
    var data = ScummDataSources.For(game);
    var record = data.ReadRoom(int.Parse(p[0])).Objects.First(o => o.Id == int.Parse(p[1]));
    foreach (var ins in data.ReadObjectVerb(int.Parse(p[0]), record, Convert.ToInt32(p[2], 16), out var entry)!.Instructions.Take(6))
        Console.WriteLine($"  {ins.Offset:X4} {ins.Operation} {ins.Operands}");
    var codePlanner = new ScummCutscenePlanner(game);
    foreach (var t in codePlanner.Triggers.Where(t => t.Room == int.Parse(p[0]) && t.ObjectId == int.Parse(p[1]) && t.VerbId == Convert.ToInt32(p[2], 16)))
        foreach (var c in codePlanner.Setup.Candidates(t)) Console.WriteLine($"  candidate {c.Kind} {c.Target} {c.Value}: {c.Describe()}");
    return;
}
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
        var d = ScummCutsceneRun.Run(game, newRun, t, null, ScummPickedStart.Direct);
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
            var r1 = ScummCutsceneRun.RunPicked(game, newRun, t, [c]);
            Console.WriteLine($"  trial {c.Describe()}: begun {r1.Begun} rooms [{string.Join(",", r1.RoomsEntered)}] lines {r1.Lines} frames {r1.Frames} failure {r1.Failure}");
        }
        // MEASURE_COMBO=0,2,5: one run with those candidates together (by their index in the list above).
        if (Environment.GetEnvironmentVariable("MEASURE_COMBO") is { Length: > 0 } combo)
        {
            var picked = combo.Split(',').Select(int.Parse).Where(i => i < candidates.Count).Select(i => candidates[i]).ToList();
            var rc = ScummCutsceneRun.RunPicked(game, newRun, t, picked);
            Console.WriteLine($"  combo {string.Join(" + ", picked.Select(c => c.Describe()))}: begun {rc.Begun} rooms [{string.Join(",", rc.RoomsEntered)}] lines {rc.Lines} frames {rc.Frames}");
        }
        // Pairs, for a few candidates (a scene that needs two values at once).
        if (candidates.Count is > 1 and <= 4)
            for (var i = 0; i < candidates.Count; i++)
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var r2 = ScummCutsceneRun.RunPicked(game, newRun, t, [candidates[i], candidates[j]]);
                    Console.WriteLine($"  pair {candidates[i].Describe()} + {candidates[j].Describe()}: begun {r2.Begun} rooms [{string.Join(",", r2.RoomsEntered)}] lines {r2.Lines}");
                }
    }
    var plan = planner.PlanSetup(t);
    var took = (clock.Elapsed - start).TotalSeconds;
    if (plan.Result.Begun) setup++; else still++;
    Console.WriteLine($"{(plan.Result.Begun ? (plan.Result.Way == ScummPickedStart.AtCutscene ? "ATCUT" : "SETUP") : "STILL")} Room {t.Room:00} object {t.ObjectId} verb 0x{t.VerbId:X2} ({t.ObjectName}) {took:F1} s, {candidates.Count} candidates: " +
                      $"{string.Join(" | ", candidates.Select(c => c.Describe()))}");
    if (Environment.GetEnvironmentVariable("MEASURE_SCRIPTS") == "1")
        Console.WriteLine($"      scripts: {string.Join(" | ", t.Scripts)}; cutscene: {ScummSceneSetupSearch.FirstCutscene(game, t)}");
    Console.WriteLine($"      result: way {plan.Result.Way} lines {plan.Result.Lines} rooms [{string.Join(",", plan.Result.RoomsEntered)}] frames {plan.Result.Frames}");
    if (plan.HasEdits) Console.WriteLine($"      kept: {string.Join("; ", plan.Edits.Select(e => e.Describe()))}; rooms [{string.Join(",", plan.Result.RoomsEntered)}] lines {plan.Result.Lines}");
}
Console.WriteLine($"fresh start begins {fresh}; setup begins {setup}; still nothing {still}; {clock.Elapsed.TotalSeconds:F0} s");
