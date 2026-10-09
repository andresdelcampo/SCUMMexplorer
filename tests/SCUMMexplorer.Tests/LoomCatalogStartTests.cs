using System.Text.Json;
using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class LoomCatalogStartTests
{
    private sealed record Catalog(LoomCatalogStartRecipe[] CatalogStarts);
    private static LoomCatalogStartRecipe Recipe() => JsonSerializer.Deserialize<Catalog>(File.ReadAllText(
        Path.GetFullPath("../../../../src/SCUMMexplorer.App/loom-replays.json", AppContext.BaseDirectory)),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.CatalogStarts.Single();

    private static LoomExplorationSession Fresh()
    {
        var game = new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative)));
        return new(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL")));
    }
    private static void Replay(LoomExplorationSession session, params LoomReplayStep[] steps)
    {
        var sequence = new LoomReplaySequence(session, steps);
        Assert.True(sequence.RunSilently(steps.Length), sequence.Failure);
    }

    [Fact]
    public void FreshLateCatalogReturnDrawsTheSourceEstablishedGhost()
    {
        var session = Fresh();
        var recipe = Recipe();
        var result = LoomCatalogStartPreparer.Prepare(session, recipe);
        Assert.True(result.Prepared, result.Failure);
        Assert.True(session.StoryBit("bit[0xD9C]"));
        Assert.Contains(session.SessionTrace, t => t.StartsWith("endCutscene at Script 87 +0x0984"));
        session.VisitRoomById(46);
        Replay(session, new("frames", Frames: 40), new("verb", 685, 0x36),
            new("scene", EndBoundary: "endCutscene at Script 97 +0x0042", ExpectedRoom: 34));
        Assert.Equal(120, session.Actors.Single(a => a.Number == 3).Look.Costume);
        // The source hides actor 3 until the conversation brings him in; check its actual visible frames.
        var conversation = new LoomReplaySequence(session, [new("verb", 560, 0x37),
            new("scene", EndBoundary: "endCutscene at Script 98 +0x0605", ExpectedRoom: 34)]);
        var drawnGhost = false;
        for (var frame = 0; conversation.Continue() && frame < 4000; frame++)
        {
            session.AdvanceFrame(); conversation.FrameAdvanced();
            var actor = session.Actors.Single(a => a.Number == 3);
            if (session.Snapshot.RoomId != 34 || actor.Visible != true || actor.Room != 34) continue;
            Assert.Equal(120, actor.Look.Costume);
            var layer = ScummActorRenderer.Render([actor], 34, 640, 144, session.ReadCostume);
            drawnGhost |= layer.Actors.Any(a => a.Number == 3 && a.Costume == 120 && a.Pixels > 0);
        }
        Assert.True(conversation.Complete, conversation.Failure);
        Assert.True(drawnGhost);
        Assert.Equal(120, session.Actors.Single(a => a.Number == 3).Look.Costume);
    }

    [Fact]
    public void EstablishedGhostDoesNotReplayEarlierScenes()
    {
        var session = Fresh(); var recipe = Recipe();
        Assert.True(LoomCatalogStartPreparer.Prepare(session, recipe).Prepared);
        var before = session.CutscenesBegun;
        var result = LoomCatalogStartPreparer.Prepare(session, recipe);
        Assert.False(result.Prepared); Assert.Null(result.Failure);
        Assert.Equal(before, session.CutscenesBegun);
        Assert.True(session.StoryBit("bit[0xD9C]"));
    }

    [Fact]
    public void RestoredRustyIsNotMadeAGhostAgain()
    {
        var session = Fresh(); var recipe = Recipe();
        Assert.True(LoomCatalogStartPreparer.Prepare(session, recipe).Prepared);
        Replay(session, new("verb", 587, 0x4D),
            new("scene", EndBoundary: "endCutscene at Room 34 local script 209 +0x03A7", ExpectedRoom: 34));
        Assert.True(session.StoryBit("bit[0xDC7]"));
        Assert.False(session.StoryBit("bit[0xD9C]"));
        var before = session.CutscenesBegun;
        Assert.False(LoomCatalogStartPreparer.Prepare(session, recipe).Prepared);
        Assert.Equal(before, session.CutscenesBegun);
        session.VisitRoomById(46);
        Replay(session, new("frames", Frames: 40), new("verb", 685, 0x36),
            new("scene", EndBoundary: "endCutscene at Script 97 +0x0042", ExpectedRoom: 34));
        Assert.Equal(0, session.Actors.Single(a => a.Number == 3).Room);
    }

    [Fact]
    public void AnAlreadyEnteredRoomDoesNotRunCatalogPreparation()
    {
        var session = Fresh(); session.VisitRoomById(46);
        var before = session.CutscenesBegun;
        var result = LoomCatalogStartPreparer.Prepare(session, Recipe());
        Assert.False(result.Prepared); Assert.Null(result.Failure);
        Assert.Equal(before, session.CutscenesBegun);
    }

    [Fact]
    public void ExistingAwakeningIsNotRepeated()
    {
        var session = Fresh(); var recipe = Recipe();
        Replay(session, recipe.Stages[0].Steps);
        var before = session.SessionTrace.Count;
        Assert.True(LoomCatalogStartPreparer.Prepare(session, recipe).Prepared);
        Assert.DoesNotContain(session.SessionTrace.Skip(before), t => t.StartsWith("cutscene at Script 85 "));
        Assert.True(session.StoryBit("bit[0xD9C]"));
    }

    [Fact]
    public void AFailedStageCancelsLaterPrerequisites()
    {
        var session = Fresh(); var recipe = Recipe();
        var broken = recipe with { Stages = [new("var[0x0D1]", 1,
            [new("visit", 34), new("verb", 562, 0x44),
             new("scene", Frames: 40, EndBoundary: "absent source end", ExpectedRoom: 34)]), ..recipe.Stages.Skip(1)] };
        var result = LoomCatalogStartPreparer.Prepare(session, broken);
        Assert.False(result.Prepared); Assert.NotNull(result.Failure);
        Assert.DoesNotContain(session.SessionTrace, t => t.StartsWith("cutscene at Script 86 "));
        Assert.NotEqual(true, session.StoryBit("bit[0xD9C]"));
    }
}
