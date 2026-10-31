using System.Diagnostics;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: ComposeTiming <game folder> <room> [with-derivation]
// 30 October 2026: times the play screen's first Compose after the catalog preparation, alone or while the cutscene derivation runs as the App runs it
// (its own copy of the game, a pool of boots, four workers).
var folder = args[0];
var game = new GameFolderScanner().Scan(folder);
var roomId = int.Parse(args[1]);
var clock = Stopwatch.StartNew();
Task? derivation = null;
if (args.Length > 2)
{
    derivation = Task.Run(() =>
    {
        GameLibrary? first = GameFolderScanner.Reopen(game);
        var planner = new ScummCutscenePlanner(first, () => ScummGameSession.BootForPlay(Interlocked.Exchange(ref first, null) ?? GameFolderScanner.Reopen(game), out _),
            start: ScummCutsceneStart.SnapshotPerWorker);
        Parallel.ForEach(planner.Groups, new ParallelOptions { MaxDegreeOfParallelism = 4 }, g => planner.Run(g.First));
        Console.WriteLine($"{clock.Elapsed.TotalSeconds:F2}s derivation done");
    });
    Thread.Sleep(8000);
}
if (args.Length > 3)
{
    // The App's room cards: every room's first visit on the same game object, in the background, one at a time.
    _ = Task.Run(() =>
    {
        foreach (var r in game.Rooms.Where(r => r.ResourceId is not null))
            try { ScummRoomFirstVisit.Run(game, r.ResourceId!.Value); } catch (Exception) { }
        Console.WriteLine($"{clock.Elapsed.TotalSeconds:F2}s cards done");
    });
    Thread.Sleep(2000);
}
var t0 = clock.Elapsed.TotalSeconds;
var session = ScummGameSession.BootForPlay(game, out var objects);
Console.WriteLine($"{clock.Elapsed.TotalSeconds:F2}s boot {clock.Elapsed.TotalSeconds - t0:F2}s");
var prepared = session.PrepareManualRoom(game.Rooms.First(r => r.ResourceId == roomId));
var live = (ScummGameSession?)prepared.Session ?? session;
Console.WriteLine($"{clock.Elapsed.TotalSeconds:F2}s prepared ({prepared.Reason ?? "live"})");
var screen = new ScummPlayScreen(game, live, objects.States);
var t1 = clock.Elapsed.TotalSeconds;
screen.Compose(0);
Console.WriteLine($"{clock.Elapsed.TotalSeconds:F2}s first Compose {clock.Elapsed.TotalSeconds - t1:F2}s");
var t2 = clock.Elapsed.TotalSeconds;
screen.Compose(1);
Console.WriteLine($"{clock.Elapsed.TotalSeconds:F2}s second Compose {clock.Elapsed.TotalSeconds - t2:F2}s");
derivation?.Wait();
