using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Static evidence that <see cref="LoomSceneDrawBatches"/> collects besides draw
/// batches: interface (verb-area) images and unavoidable entry-script states.
/// </summary>
public sealed class LoomSceneEvidenceTests
{
    [Fact]
    public void VerbImagesAreFoundThroughLiteralsAndGlobalVariableAssignments()
    {
        var shows = Script(
            I(0x00, "verbOps", "0x01, [at, 0x0010, 0x0090; image, 0x0064; on]"),
            I(0x0C, "verbOps", "0x36, [at, 0x00F8, 0x0098; image, var[0x068]; on]"),
            I(0x18, "verbOps", "0x04, [at, local[0x001], local[0x002]; image, local[0x005]; on]"));
        var selects = Script(
            I(0x00, "move", "var[0x068] = 0x009E"),
            I(0x05, "move", "var[0x068] = 0x0000"),
            I(0x0A, "move", "var[0x069] = 0x00A0"));

        var found = LoomSceneDrawBatches.FindInterfaceImages([(shows, "script 008"), (selects, "Room 02 object 154 Verb 0x39")]);

        Assert.Equal([0x64, 0x9E], found.Keys.Order());
        Assert.Equal("verbOps image in script 008 +0x0000", found[0x64]);
        Assert.Equal("var[0x068] set in Room 02 object 154 Verb 0x39 +0x0000, shown by verbOps image in script 008 +0x000C",
            found[0x9E]);
    }

    [Fact]
    public void OnlyAStateSetOnEveryPathThroughTheEntryScriptCounts()
    {
        // 0x10 is set on one branch only; both branches rejoin before 0x11 is set.
        var script = Script(
            I(0x00, "equalZero", "bit[0xD62], branch +0x0007 -> 0x000C"),
            I(0x05, "setState", "0x0010, 0x01"),
            I(0x09, "jumpRelative", "+4  ->  0x0010"),
            I(0x0C, "ifClassOfIs", "0x0012, [0x0098], branch +0x0000 -> 0x0010"),
            I(0x10, "setState", "0x0011, 0x00"),
            I(0x14, "stopObjectCode", ""));

        var states = LoomSceneDrawBatches.FindUnavoidableStates(script, "Room 99 entry script");

        var only = Assert.Single(states);
        Assert.Equal(0x11, only.Key);
        Assert.Equal(0, only.Value.State);
        Assert.Equal("Room 99 entry script +0x0010 sets it to 0 on every path", only.Value.Source);
    }

    [Fact]
    public void AStopBeforeTheStateMakesItAvoidable()
    {
        var script = Script(
            I(0x00, "equalZero", "bit[0xD62], branch +0x0002 -> 0x0007"),
            I(0x05, "stopScript", "0x00"),
            I(0x07, "setState", "0x0011, 0x01"),
            I(0x0B, "stopObjectCode", ""));

        Assert.Empty(LoomSceneDrawBatches.FindUnavoidableStates(script, "entry"));
    }

    [Fact]
    public void RepeatedOrVariableStatesAreNotUsed()
    {
        var twice = Script(
            I(0x00, "setState", "0x0011, 0x01"),
            I(0x04, "setState", "0x0011, 0x00"),
            I(0x08, "setState", "0x0012, 0x01"),
            I(0x0C, "stopObjectCode", ""));
        Assert.Equal([0x12], LoomSceneDrawBatches.FindUnavoidableStates(twice, "entry").Keys);

        var variable = Script(
            I(0x00, "setState", "0x0012, 0x01"),
            I(0x04, "setState", "local[0x000], 0x00"),
            I(0x08, "stopObjectCode", ""));
        Assert.Empty(LoomSceneDrawBatches.FindUnavoidableStates(variable, "entry"));
    }

    [Fact]
    public void ALoopOverALiteralLocalTableRestsOnItsLastFrame()
    {
        // Room 04 local 201's shape: the argument picks a table, and the loop
        // draws entries 0..4 (o5_isLess jumps back while the index is <= 4).
        var script = Script(
            I(0x00, "isEqual", "local[0x000], 0x00AB, branch +0x000C -> 0x0013"),
            I(0x07, "setVarRange", "local[0x006], count=5, [0xB3, 0xB4, 0xB4, 0xB3, 0xB2]"),
            I(0x10, "jumpRelative", "+0  ->  0x0013"),
            I(0x13, "move", "local[0x003] = 0x0000"),
            I(0x18, "move", "local[0x004] = local[0x006+local[0x003]]"),
            I(0x1F, "drawObject", "local[0x004], at, 0x00FF, 0x00FF"),
            I(0x26, "breakHere", ""),
            I(0x27, "increment", "local[0x003]"),
            I(0x2A, "isLess", "local[0x003], 0x0004, branch +0xFFE7 -> 0x0018"),
            I(0x31, "stopObjectCode", ""));
        var rects = new Dictionary<int, (int, int, int, int)>
        {
            [0xB2] = (8, 8, 16, 16), [0xB3] = (8, 8, 16, 16), [0xB4] = (8, 8, 16, 16)
        };

        var batch = Assert.Single(LoomSceneDrawBatches.FindLocalTableRestFrames(script, "Room 04 local script 201", rects));

        Assert.Equal([0xB2], batch.Steps.Select(step => step.ObjectId));
        Assert.Equal(LoomSceneDrawBatches.OtherScriptPriority, batch.Priority);
        Assert.Equal("Room 04 local script 201 +0x001F, last frame from the table set at +0x0007", batch.Source);
    }

    [Fact]
    public void DrawsOfArgumentsOrUnknownLocalsGiveNoRestFrame()
    {
        // Room 70 local 205's shape: the drawn objects are the script's arguments.
        var arguments = Script(
            I(0x00, "drawObject", "local[0x001], at, 0x00FF, 0x00FF"),
            I(0x07, "breakHere", ""),
            I(0x08, "drawObject", "local[0x000], at, 0x00FF, 0x00FF"),
            I(0x0F, "setVarRange", "local[0x006], count=2, [0x10, 0x11]"),
            I(0x18, "stopObjectCode", ""));
        Assert.Empty(LoomSceneDrawBatches.FindLocalTableRestFrames(arguments, "s", new Dictionary<int, (int, int, int, int)>()));

        // A table entry overwritten by a result is no longer known.
        var overwritten = Script(
            I(0x00, "setVarRange", "local[0x006], count=2, [0x10, 0x11]"),
            I(0x09, "getRandomNr", "local[0x006], 0x0005"),
            I(0x0E, "drawObject", "local[0x006], at, 0x00FF, 0x00FF"),
            I(0x15, "stopObjectCode", ""));
        Assert.Empty(LoomSceneDrawBatches.FindLocalTableRestFrames(overwritten, "s",
            new Dictionary<int, (int, int, int, int)> { [0x10] = (0, 0, 8, 8) }));
    }

    private static LoomScriptInstruction I(int offset, string operation, string operands) =>
        new(offset, 0, operation, operands, "");

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(1, 1, 0, 32, 16, instructions, null, null, [], []);

    [Fact]
    public void Room69DifficultyButtonsAreBlankedByLocal201SoTheyAreNotDrawnBack()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(folder);
        var drawn = LoomSceneDrawBatches.Build(game).ForRoom(69).SelectMany(batch => batch.Steps).Select(step => step.ObjectId);
        // Local 201 stops 202 and boxes (120,64)-(200,128) over objects 876-878.
        Assert.DoesNotContain(drawn, id => id is 876 or 877 or 878);
        Assert.Contains(885, drawn);
    }

    [Fact]
    public void Room69SessionBlanksTheQueuedDifficultyButtonsWhenLocal201BoxesOverThem()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")), completeBoot: false);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 69));
        for (var i = 0; i < 30; i++) session.AdvanceFrame();
        Assert.Contains(878, session.Snapshot.DrawOrder);
        session.Click(160, 72);
        for (var i = 0; i < 60; i++) session.AdvanceFrame();
        Assert.DoesNotContain(session.Snapshot.DrawOrder, id => id is 877 or 878 or 879);
        Assert.Contains(886, session.Snapshot.DrawOrder);
    }

    [Fact]
    public void Rooms82To85AreSoundBanksAndLeaveTheSceneListWhileRoom61Stays()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(folder);
        var banks = game.Rooms.Where(SCUMMexplorer.Core.Rendering.LoomRoomKinds.IsResourceBank).Select(r => r.ResourceId!.Value).ToArray();
        Assert.Equal([82, 83, 84, 85], banks);
        Assert.DoesNotContain(game.Scenes, r => banks.Contains(r.ResourceId!.Value));
        Assert.Contains(game.Scenes, r => r.ResourceId == 61);
        Assert.Equal(game.Rooms.Count() - 4, game.Scenes.Count());
        Assert.Equal(32, game.Resources.Count(r => r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Sound && r.ResourceId is >= 33 and <= 64));
    }
}
