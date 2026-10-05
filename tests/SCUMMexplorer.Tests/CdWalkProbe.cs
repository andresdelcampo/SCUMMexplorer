using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>Discovery only (env CD_WALK_OUT, CD_WALK=room,fromX,fromY,toX,toY): puts the ego at one point of a CD room, walks him to another and logs each change of his walk box and the camera.</summary>
public sealed class CdWalkProbe
{
    [Fact]
    public void Walk()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_WALK_OUT");
        var spec = Environment.GetEnvironmentVariable("CD_WALK");
        if (outPath is null || spec is null || !Directory.Exists(folder)) return;
        var p = spec.Split(',').Select(int.Parse).ToArray();
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == p[0]));
        for (var i = 0; i < 30; i++) session.AdvanceFrame();
        var lines = new List<string>();
        session.PlaceEgo(p[1], p[2]);
        for (var i = 0; i < 5; i++) session.AdvanceFrame();
        session.WalkEgoTo(p[3], p[4]);
        int? last = null;
        for (var i = 0; i < 600; i++)
        {
            var ego = session.Actors.First(a => a.Number == 1);
            if (ego.WalkBox != last) { lines.Add($"frame {i}: box {ego.WalkBox} at {ego.X},{ego.Y} begun {session.CutscenesBegun} inCutscene {session.InCutscene}"); last = ego.WalkBox; }
            session.AdvanceFrame();
        }
        var end = session.Actors.First(a => a.Number == 1);
        lines.Add($"end: box {end.WalkBox} at {end.X},{end.Y} begun {session.CutscenesBegun}");
        File.WriteAllLines(outPath, lines);
    }
}
