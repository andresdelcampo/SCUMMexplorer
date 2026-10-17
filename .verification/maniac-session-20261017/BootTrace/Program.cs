using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
var game = new GameFolderScanner().Scan(args[0]);
var data = (ScummV2DataSource)ScummDataSources.For(game);
if (args.Length > 1 && args[1] == "dump")
{
    foreach (var id in args.Skip(2).Select(int.Parse)) Console.WriteLine(data.GlobalScript(id)?.Format());
    return;
}
var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data);
var postBootFrames = 0;
Console.WriteLine($"Boot: {session.BootReport}; control {session.PlayerHasControl}; cutscene {session.InCutscene}");
if (args.Length > 1 && args[1] == "select")
{
    foreach (var point in new[] { (76, 104), (196, 104), (292, 60) })
    {
        session.Click(point.Item1, point.Item2);
        for (var frame = 0; frame < 100; frame++) { session.AdvanceFrame(); postBootFrames++; }
        Console.WriteLine($"Click {point}: control {session.PlayerHasControl}; cutscene {session.InCutscene}");
    }
    for (var frame = 0; frame < 5000 && !session.PlayerHasControl; frame++) { session.AdvanceFrame(); postBootFrames++; }
}
Console.WriteLine($"Final: room {session.CurrentRoomId}; control {session.PlayerHasControl}; cutscene {session.InCutscene}; rooms {string.Join(",", session.RoomsEntered)}; charcount {session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x007]")}; post-boot frames {postBootFrames}");
if (session.PlayerHasControl)
{
    for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
    Console.WriteLine($"Stability +120: room {session.CurrentRoomId}; control {session.PlayerHasControl}; cutscene {session.InCutscene}");
}
foreach (var line in session.SessionTrace.Where(s => !s.Contains("waits") && !s.Contains("delay ends")).Take(100)) Console.WriteLine(line);
Console.WriteLine("== Pending");
foreach (var line in session.PendingScriptSummary) Console.WriteLine(line);
Console.WriteLine("== Uncertainties");
foreach (var line in session.Snapshot.Uncertainties.Where(s => !s.Contains("seeded pick") && !s.Contains("when it runs again")).Distinct().Take(100)) Console.WriteLine(line);
