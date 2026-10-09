using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

public sealed class LoomEstimatedRoomLoadTests
{
    private static LoomExplorationSession? Session()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")), completeBoot: false);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 42));
        return session;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutedEstimatedLoadEntersRoomWithoutPromotingCertainty(bool roomOwned)
    {
        if (Session() is not { } session) return;
        var script = LoomCallObservationTests.Code(
            ("getRandomNr", "local[0x000], 0x00"),
            ("equalZero", "local[0x000], branch +0 -> 0x0006"),
            ("breakHere", ""),
            ("loadRoom", "0x2B"),
            ("move", "var[0x0F0] = 0x0011"),
            ("stopObjectCode", ""),
            ("stopObjectCode", ""));
        if (roomOwned) script = script with { Context = "Synthetic room-owned transition" };
        var start = session.RunTopLevelScript(script);
        Assert.True(start.PathEstimated);
        Assert.Equal(3, start.ResumeOffset);

        session.AdvanceFrame();

        Assert.Equal(43, session.Snapshot.RoomId);
        var load = Assert.IsType<ScummScriptEvent>(session.LastAction?.ReachedRoomLoad);
        Assert.True(load.Executed);
        Assert.Equal(ScummScriptEventCertainty.Conditional, load.Certainty);
        Assert.Contains("estimated", session.LastAction!.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.False(session.Snapshot.IsFaithful);
        // Room-entry effects still inherit the estimated transition. Rendering
        // keeps their chosen values without turning them into source proof.
        Assert.Contains(645, session.Snapshot.EstimatedStates);
        Assert.DoesNotContain(645, session.Snapshot.StatesSetByCode);
        Assert.Contains(645, session.Snapshot.ArtworkStateIds());
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        var game = new GameFolderScanner().Scan(folder);
        var bytes = File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 43).FilePath);
        var background = new ScummV3EgaRoomDecoder().Decode(bytes);
        var images = new ScummRoomObjects().Decode(bytes);
        var batches = ScummSceneDrawBatches.Build(game);
        var snapshot = session.Snapshot;
        var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions,
            new ScummRoomObjectDirectory().Decode(bytes), background, drawQueue: snapshot.DrawOrder);
        selection.ExcludeInterfaceImages(batches.InterfaceImages.Keys);
        selection.FillGaps(batches.ForRoom(43), 43);
        if (!images.Any(selection.IsActive))
            selection.ShowUnresolvedArtwork(batches.ForRoom(43), 43, snapshot.ArtworkStateIds(), batches.EntryStatesForRoom(43));
        Assert.Equal(background.Pixels.ToArray(), selection.Compose(background));
        if (roomOwned) Assert.NotEqual((ushort)17, session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x0F0]"));
        else Assert.Equal((ushort)17, session.Snapshot.KnownGlobalVariables["var[0x0F0]"]);
    }

    [Theory]
    [InlineData("var[0x0F1]")]
    [InlineData("0xFE")]
    public void UnknownOrAbsentDestinationsDoNotLoadARoom(string operand)
    {
        if (Session() is not { } session) return;
        ForgetSyntheticVariable(session);
        session.RunTopLevelScript(LoomCallObservationTests.Code(("breakHere", ""), ("loadRoom", operand), ("stopObjectCode", "")));
        session.AdvanceFrame();
        Assert.Equal(42, session.Snapshot.RoomId);
    }

    [Fact]
    public void ALoadOnAnUnresolvedBranchIsNotExecuted()
    {
        if (Session() is not { } session) return;
        ForgetSyntheticVariable(session);
        session.RunTopLevelScript(LoomCallObservationTests.Code(("breakHere", ""),
            ("equalZero", "var[0x0F1], branch +0 -> 0x0004"), ("loadRoom", "0x2B"),
            ("stopObjectCode", ""), ("stopObjectCode", "")));
        session.AdvanceFrame();
        Assert.Equal(42, session.Snapshot.RoomId);
    }

    private static void ForgetSyntheticVariable(LoomExplorationSession session)
    {
        // Session globals normally start at zero. This test explicitly models a
        // missing value, not an ordinary known-zero game variable.
        var runtime = (ScummScriptRuntimeState)typeof(LoomExplorationSession)
            .GetField("_runtime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(session)!;
        runtime.GlobalVariables.Remove("var[0x0F1]");
    }
}
