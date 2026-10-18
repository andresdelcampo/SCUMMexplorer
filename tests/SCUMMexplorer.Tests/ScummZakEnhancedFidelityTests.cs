using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>Bounded source previews, not an earned flight or a complete-story playthrough.</summary>
public sealed class ScummZakEnhancedFidelityTests(ITestOutputHelper output)
{
    private static string Folder => Path.Combine(TestGames.Root, "zakE ega en");
    private static readonly Lazy<ScummCutscenePlanner?> Planner = new(() => Directory.Exists(Folder)
        ? new ScummCutscenePlanner(new GameFolderScanner().Scan(Folder)) : null);

    [Theory]
    [InlineData(2, 0, 0, 646)]
    [InlineData(14, 55, 11, 907)]
    public void CorrectNativeActorApproachLetsFormerlyCappedSceneFinish(int room, int obj, int verb, int expectedFrames)
    {
        if (Planner.Value is not { } planner) return;
        var trigger = Assert.Single(planner.Groups, group => group.First.Room == room &&
            group.First.ObjectId == obj && group.First.VerbId == verb).First;
        var run = planner.Run(trigger);
        output.WriteLine($"{room}/{obj}/{verb}: begun={run.Begun}, frames={run.Frames}, rooms={string.Join(',', run.RoomsEntered)}, failure={run.Failure}");
        Assert.True(run.Begun);
        Assert.Null(run.Failure);
        Assert.Equal(expectedFrames, run.Frames);
        Assert.Contains(16, run.RoomsEntered);
    }

    private static ScummGameSession Session(out IScummDataSource data, out List<ScummScriptObservation> observations)
    {
        var game = new GameFolderScanner().Scan(Folder);
        data = ScummDataSources.For(game);
        var captured = new List<ScummScriptObservation>();
        observations = captured;
        return new(game, ((ScummV2DataSource)data).ReadIndexedObjects(), data: data,
            observer: new ScummScriptObserver(captured.Add, site => site.Script is 104 or 78,
                key => key == "var[0x001]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshFlightStartRetainsItsUnmetNativeTicketOwnerGuard(bool openObject)
    {
        if (Planner.Value is null) return;
        var session = Session(out var data, out var observations);
        var initialOwner = session.Snapshot.ObjectOwners[51];
        var initialInventory = session.Snapshot.Inventory.Where(item => item == 51).ToArray();
        var game = new GameFolderScanner().Scan(Folder);
        var room = game.Rooms.Single(resource => resource.ResourceId == 11);
        if (openObject) { session.VisitRoom(room); session.ActivateObjectVerb(249, 1); }
        else session.EnterRoomAgain(room);
        for (var frame = 0; frame < 1500; frame++) session.AdvanceFrame();
        var guards = observations.Where(observation => observation.Site is
            { Script: 104, Stage: "instruction", Offset: 0x1C9 }).ToArray();
        Assert.True(guards.Length > 10);
        Assert.All(guards, guard => Assert.Equal(initialOwner, guard.Values["var[0x001]"]));
        // isNotEqual is the fallthrough condition: while owner == room-owner 15,
        // its relative jump returns to breakHere; it does not acquire the object for us.
        Assert.Equal(15, initialOwner);
        Assert.Equal(initialOwner, session.Snapshot.ObjectOwners[51]);
        Assert.Equal(initialInventory, session.Snapshot.Inventory.Where(item => item == 51));
        Assert.Contains(session.PendingScriptSummary, line => line.StartsWith("Script 104 +0x01C5"));
        Assert.True(session.InCutscene);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("unsupported v2"));
        var ownerRead = Assert.Single(data.GlobalScript(104)!.Instructions, instruction => instruction.Offset == 0x1C5);
        Assert.Equal("getObjectOwner", ownerRead.Operation);
        Assert.Contains("0x0033", ownerRead.Operands);
        output.WriteLine($"11/{(openObject ? "249/1" : "0/0")}: 1500 frames, {guards.Length} native owner guards, owner51={initialOwner}, inventory unchanged for51, {string.Join(';', session.PendingScriptSummary.Where(line => line.StartsWith("Script 104")))}");
    }

    [Fact]
    public void TerminalEndingKeepsItsNativeEscapePromptWithoutAutomaticRestart()
    {
        if (Planner.Value is null) return;
        var session = Session(out var data, out var observations);
        var game = new GameFolderScanner().Scan(Folder);
        session.VisitRoom(game.Rooms.Single(resource => resource.ResourceId == 23));
        session.ActivateObjectVerb(410, 253);
        var shown = new HashSet<string>();
        for (var frame = 0; frame < 1500; frame++)
        { foreach (var message in session.Messages.OnScreen) shown.Add(message.Text.Trim()); session.AdvanceFrame(); }
        Assert.Contains("THE GAME IS OVER", shown);
        Assert.Contains("Press ESCAPE to start again.", shown);
        Assert.True(session.InCutscene);
        Assert.Equal(23, session.CurrentRoomId);
        Assert.Contains(observations, observation => observation.Site is { Script: 78, Offset: 0x211, Operation: "jumpRelative" });
        Assert.DoesNotContain(observations, observation => observation.Site is { Script: 78, Offset: 0x217, Operation: "restart" });
        Assert.Contains(data.GlobalScript(78)!.Instructions, instruction => instruction.Offset == 0x211 &&
            instruction.Operation == "jumpRelative" && instruction.Operands.Contains("0x01D5"));
        output.WriteLine($"23/410/253: reached both terminal prompts within1500frames; native loop remains, no restart or automatic Escape");
    }
}
