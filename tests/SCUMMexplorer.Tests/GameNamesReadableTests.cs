using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// ROADMAP item 3 (31 October 2026): the readable view of every game names what the game's own data names: objects (each room's stored name) and actors
/// (<c>actorOps … actorName</c>, scoped to the script that sets it). Record: <c>.verification/readable-names-20261031/README.md</c>.
/// </summary>
public sealed class GameNamesReadableTests(ITestOutputHelper output)
{
    [Fact]
    public void Indy3ScriptsNameTheirActorsAndObjects()
    {
        var folder = Path.Combine(TestGames.Root, "indy3 ega en");
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var glossary = LoomGlossary.Load(game);
        // Global script 118 +0x0034: actorOps 0x01, [...; actorName, "Indy"]; +0x0045 actor 4 "Marcus".
        Assert.Equal("Indy", glossary.Actor(1, "script:118")?.Name);
        Assert.Equal("Marcus", glossary.Actor(4, "script:118")?.Name);
        var text = new LoomScriptPresenter(glossary).Present(ScummDataSources.For(game).GlobalScript(118)!);
        output.WriteLine(string.Join('\n', text.Split('\n').Take(20)));
        Assert.Contains("Indy", text);
        // Room 2's objects carry their stored names (the jar, object 71, which the scene setup tests use).
        Assert.False(string.IsNullOrEmpty(glossary.Object(0, 71)?.Name));
    }

    [Fact]
    public void Monkey2NamesItsHeroGameWide()
    {
        var folder = Path.Combine(TestGames.Root, "mi2 mt32 en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var glossary = LoomGlossary.Load(game);
        output.WriteLine($"actor 1: {glossary.Actor(1)?.Name} ({glossary.Actor(1)?.Source})");
        // Global script 1 +0x0969: actorOps 0x01, [init; costume, 0x01; talkColor, 0x0F; actorName, "Guybrush"].
        Assert.Equal("Guybrush", glossary.Actor(1, "script:1")?.Name);
    }
}
