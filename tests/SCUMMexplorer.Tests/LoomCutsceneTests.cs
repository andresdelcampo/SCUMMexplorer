using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Cutscenes, overrides, freezes, one-frame waits, isSoundRunning and
/// isScriptRunning, modelled after ScummVM (beginCutscene, beginOverride,
/// freezeScripts, o5_wait, o5_isSoundRunning, isScriptRunning), and global
/// scripts that carry on after loading a room.
/// </summary>
public sealed class LoomCutsceneTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Theory]
    [InlineData("delay", "0x00003C", "a delay of 60 jiffies")]
    [InlineData("waitFor", "message", "waiting for the message to finish")]
    [InlineData("waitFor", "actor, 0x03", "waiting for actor 0x03 to stop walking")]
    public void WaitsContinueOnTheNextFrameWithATimingLabel(string operation, string operands, string label)
    {
        var script = Script(
            new(0, 0, operation, operands, "AE 02 00 00"),
            new(4, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.Equal(4, run.ResumeOffset);
        Assert.False(run.ReachedStop);
        Assert.Contains($"timing not simulated: {label} continues on the next frame", run.UnresolvedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void IsSoundRunningReadsStartedSoundsAsAnEstimateAndOthersAsSilent()
    {
        var script = Script(
            new(0, 0, "isSoundRunning", "var[0x010], 0x19", "7C 10 00 19"),
            new(4, 0, "isSoundRunning", "var[0x011], 0x1A", "7C 11 00 1A"),
            new(8, 0, "isSoundRunning", "var[0x012], 0x00", "7C 12 00 00"),
            new(12, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.ActiveSounds.Add(0x19);

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.True(run.ReachedStop);
        Assert.Equal((ushort)1, runtime.GlobalVariables["var[0x010]"]);
        Assert.Equal((ushort)0, runtime.GlobalVariables["var[0x011]"]);
        Assert.Equal((ushort)0, runtime.GlobalVariables["var[0x012]"]);
        // Playing is an estimate (length not simulated); silent follows from the route.
        Assert.Contains("var[0x010]", runtime.EstimatedVariables);
        Assert.DoesNotContain("var[0x011]", runtime.EstimatedVariables);
        Assert.Contains("reads sound 25 as playing", run.UnresolvedReason, StringComparison.Ordinal);
        Assert.Contains("reads sound 26 as silent", run.UnresolvedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void IsScriptRunningAsksTheSessionAndLabelsUnmodelledEndsAsEstimates()
    {
        var script = Script(
            new(0, 0, "isScriptRunning", "var[0x010], 0xD2", "68 10 00 D2"),
            new(4, 0, "isScriptRunning", "var[0x011], 0x05", "68 11 00 05"),
            new(8, 0, "stopObjectCode", "", "00"));
        var executor = new LoomBoundedScriptExecutor();

        var alone = executor.Execute(script, new Dictionary<int, byte>(), []);
        Assert.Contains("needs a session", alone.UnresolvedReason, StringComparison.Ordinal);

        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>())
        {
            ScriptRunning = number => number == 0xD2 ? (true, null) : (false, "it ended at an unmodelled step")
        };
        var run = executor.Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.True(run.ReachedStop);
        Assert.Equal((ushort)1, runtime.GlobalVariables["var[0x010]"]);
        Assert.Equal((ushort)0, runtime.GlobalVariables["var[0x011]"]);
        Assert.DoesNotContain("var[0x010]", runtime.EstimatedVariables);
        Assert.Contains("var[0x011]", runtime.EstimatedVariables);
    }

    [Fact]
    public void BeginOverrideStepsOverItsSkipJump()
    {
        // beginOverride; jumpRelative +1 (the skip); breakHere; stopObjectCode.
        var decoded = LoomScriptDisassembler.Disassemble([0x58, 0x01, 0x18, 0x01, 0x00, 0x80, 0x00]);
        Assert.Equal([0, 2, 5, 6], decoded.Instructions.Select(item => item.Offset));

        var script = Script(
            new(0, 0x58, "beginOverride", "", "58 01"),
            new(2, 0x18, "jumpRelative", "+5  ->  0x000A", "18 05 00"),
            new(5, 0x1A, "move", "local[0x000] = 0x0001", "1A 00 00 01 00"),
            new(10, 0, "stopObjectCode", "", "00"));

        var run = new LoomBoundedScriptExecutor().Execute(script, new Dictionary<int, byte>(), []);

        Assert.True(run.ReachedStop);
        Assert.Null(run.UnresolvedReason);
        Assert.Equal((ushort)1, run.KnownVariables["local[0x000]"]);
        Assert.Contains(run.Trace, line => line.Contains("runs only if the player skips the cutscene", StringComparison.Ordinal));
    }

    [Fact]
    public void FreezeLeavesFreezeResistantStartsRunningBelowFlag0x80()
    {
        if (Load() is not { } data) return;
        var (game, index) = data;
        // Without the boot's own pending scripts, so only the synthetic starts are running.
        var session = new LoomExplorationSession(game, index, completeBoot: false);
        // Scripts 39 and 40 start a sound and wait while it plays, so both stay pending.
        // Opcode 0x2A is startScript with bit 0x20: script 39 starts freeze-resistant.
        session.RunTopLevelScript(Script(
            new(0, 0x2A, "startScript", "0x27, [0x0019]", "2A 27 19 00 FF"),
            new(5, 0x0A, "startScript", "0x28, [0x0017]", "0A 28 17 00 FF"),
            new(10, 0, "stopObjectCode", "", "00")));
        Assert.Equal(2, session.Snapshot.PendingScripts);

        session.RunTopLevelScript(Freeze("0x7F"));
        Assert.EndsWith("frozen: Script 40", session.SessionTrace[^1], StringComparison.Ordinal);
        session.AdvanceFrame();
        Assert.Equal(2, session.Snapshot.PendingScripts);

        session.RunTopLevelScript(Freeze("0x00"));
        Assert.EndsWith("no script is frozen", session.SessionTrace[^1], StringComparison.Ordinal);

        session.RunTopLevelScript(Freeze("0x80"));
        Assert.EndsWith("frozen: Script 39, Script 40", session.SessionTrace[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalScriptCarriesOnAfterItsRoomLoad()
    {
        if (Load() is not { } data) return;
        var (game, index) = data;
        // Without hooks, so the cutscene runs no start or end script.
        var session = new LoomExplorationSession(game, index, completeBoot: false);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 10));

        // Object 295 starts script 052: a cutscene that loads Room 11, then Room 13.
        var action = session.ActivateObjectVerb(295, 0x38);
        Assert.Null(action.DestinationRoomId);
        Assert.True(session.InCutscene);

        Assert.True(session.ContinueUntilRoomChanges(32));

        Assert.Equal(13, session.Snapshot.RoomId);
        Assert.False(session.InCutscene);
        Assert.Contains("Script 52 continues at +0x0047 in Room 11 after loadRoom", session.SessionTrace);
        var last = session.LastAction!;
        Assert.Equal((10, 295, 13, 0x017D), (last.SourceRoomId, last.ObjectId, last.DestinationRoomId, last.ReachedRoomLoad!.Offset));
        Assert.Contains(last.Uncertainties, item => item == "Script 52 passed through Room 11 (loadRoom at +0x0045) before this load.");
    }

    [Fact]
    public void CutsceneRunsTheScriptsInItsHooksAndFreezesTheRest()
    {
        if (Load() is not { } data) return;
        var (game, index) = data;
        var session = new LoomExplorationSession(game, index);
        // The completed boot sets the hooks (script 001 +0x017A and +0x017F).
        Assert.Equal((ushort)2, session.Snapshot.KnownGlobalVariables["var[0x023]"]);
        Assert.Equal((ushort)3, session.Snapshot.KnownGlobalVariables["var[0x024]"]);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 10));

        session.ActivateObjectVerb(295, 0x38);

        Assert.Contains("cutscene at Script 52 +0x0000 runs script 2", session.SessionTrace);
        // Script 002 freezes every other slot; the cutscene's own script keeps running.
        Assert.Contains("freezeScripts 0x7F at Script 02 +0x0004: frozen: Room 10 Object 295 Verb 0x38, Room 10 local script 200",
            session.SessionTrace);
        Assert.Equal((ushort)1, session.Snapshot.KnownGlobalVariables["bit[0xD6E]"]);

        Assert.True(session.ContinueUntilRoomChanges(32));

        Assert.Contains("endCutscene at Script 52 +0x017C runs script 3", session.SessionTrace);
        Assert.Contains("freezeScripts 0x00 at Script 03 +0x0016: no script is frozen", session.SessionTrace);
        Assert.Equal((ushort)0, session.Snapshot.KnownGlobalVariables["bit[0xD6E]"]);
        Assert.Equal((ushort)0, session.Snapshot.KnownGlobalVariables["var[0x005]"]);
        Assert.Equal(13, session.Snapshot.RoomId);
    }

    private static LoomScriptDisassembly Freeze(string flag) => Script(
        new(0, 0x60, "freezeScripts", flag, "60 00"),
        new(2, 0, "stopObjectCode", "", "00"));

    private static (GameLibrary Game, LoomIndexedObjectSnapshot Index)? Load()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return null;
        return (new GameFolderScanner().Scan(Folder), new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
    }

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(250, 1, 0, 32, 16, instructions, null, null, [], [], "Synthetic test script");
}
