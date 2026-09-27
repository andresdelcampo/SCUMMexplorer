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

    private static LoomScriptInstruction I(int offset, string operation, string operands) =>
        new(offset, 0, operation, operands, "");

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(1, 1, 0, 32, 16, instructions, null, null, [], []);
}
