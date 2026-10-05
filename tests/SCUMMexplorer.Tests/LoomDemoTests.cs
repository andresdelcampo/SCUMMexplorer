using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The two LOOM self-running previews (<c>Games/loom demo short ega en</c> and <c>long demo en</c>). Their 00.LFL lists resources
/// their folders lack; the scanner keeps the others. Without the folders these tests do nothing.
/// </summary>
public sealed class LoomDemoTests
{
    public static IEnumerable<object[]> Demos() => [["loom demo short ega en", 16], ["loom demo long ega en", 32]];

    private static string? Folder(string name)
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", name));
        return File.Exists(Path.Combine(folder, "LOOMDEMO.EXE")) ? folder : null;
    }

    [Theory]
    [MemberData(nameof(Demos))]
    public void DemoKeepsOnlyDecodableResources(string name, int rooms)
    {
        if (Folder(name) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.True(game.IsDemo);
        Assert.Equal(rooms, game.Rooms.Count());
        // The black carrier rooms (no objects, one flat colour) are no scenes: 2 of the short demo's 16, 10 of the long one's 32.
        Assert.Equal(rooms - (rooms == 16 ? 2 : 10), game.Scenes.Count());
        var reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        foreach (var script in game.Resources.Where(r => r.Kind == ResourceKind.Script))
            Assert.NotEmpty(reader.Decode(script).Instructions);
        var chains = new LflResourceChain();
        foreach (var entry in game.Resources.Where(r => r.Kind is ResourceKind.Script or ResourceKind.Costume or ResourceKind.Sound))
            Assert.Null(chains.FindLeftover(game, entry));
    }

    [Theory]
    [MemberData(nameof(Demos))]
    public void DemoGlossaryDoesNotNameTheFullGamesScripts(string name, int rooms)
    {
        if (Folder(name) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var glossary = LoomGlossary.Load(game);
        Assert.Null(glossary.Script(45));
        Assert.Null(glossary.Bit(0xD72));
        Assert.NotEqual(0, rooms);
    }

    [Theory]
    [MemberData(nameof(Demos))]
    public void DemoSessionStaysInRoom01UntilThePreviewIsPlayed(string name, int rooms)
    {
        if (Folder(name) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        for (var frame = 0; frame < 600; frame++) session.AdvanceFrame();
        Assert.Equal(1, session.Snapshot.RoomId);
        Assert.Equal(0, session.Snapshot.PendingScripts);
        Assert.NotEqual(0, rooms);
    }

    [Theory]
    [MemberData(nameof(Demos))]
    public void CatalogVisitDressesTheEgoAsThePreviewDoes(string name, int rooms)
    {
        if (Folder(name) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        int? EgoCostume() => session.Actors.First(a => a.Number == 1).Look.Costume;
        // Room 03 shows the smaller Bobbin (costume 3), the beach the ordinary one (costume 1), in either order of visits.
        session.VisitRoomById(3);
        Assert.Equal(3, EgoCostume());
        session.VisitRoomById(16);
        Assert.Equal(1, EgoCostume());
        Assert.NotEqual(0, rooms);
    }

    [Theory]
    [MemberData(nameof(Demos))]
    public void DemoPlaysThroughItsRoomsAndStartsOver(string name, int rooms)
    {
        if (Folder(name) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")), demoPreview: true);
        var visited = new List<int>();
        for (var frame = 0; frame < 8000 && visited.Count(room => room == 1) < 2; frame++)
        {
            session.AdvanceFrame();
            if (session.Snapshot.RoomId is int room && (visited.Count == 0 || visited[^1] != room)) visited.Add(room);
        }
        Assert.Equal(2, visited.Count(room => room == 1));
        // Measured with DemoProbe play: the short preview shows 14 of its 16 rooms, the long one 22 of 32 (the rest are sound banks and rooms it never enters).
        var expected = rooms == 16 ? 14 : 22;
        Assert.True(visited.Distinct().Count() >= expected, $"the preview showed {visited.Distinct().Count()} of {rooms} rooms");
    }
}
