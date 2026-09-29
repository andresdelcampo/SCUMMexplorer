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
    [InlineData("delay", "0x00003C", 60)]
    [InlineData("delayVariable", "var[0x0F1]", 13)]
    public void ADelayPausesTheScriptWithItsJiffies(string operation, string operands, int jiffies)
    {
        // o5_delay/o5_delayVariable: the slot's delay is set and the script breaks; the frame loop counts it down.
        var script = Script(
            new(0, 0, operation, operands, "2E 3C 00 00"),
            new(4, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        runtime.GlobalVariables["var[0x0F1]"] = 13;

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.Equal(4, run.ResumeOffset);
        Assert.Equal(jiffies, run.Delay);
        Assert.False(run.ReachedStop);
        Assert.Null(run.UnresolvedReason);
    }

    [Fact]
    public void AWaitForActorBreaksWhileTheActorMovesAndGoesOnOnceItStops()
    {
        // o5_wait (SO_WAIT_FOR_ACTOR) breaks while the actor's _moving is set, and goes on at once otherwise.
        var script = Script(
            new(0, 0, "waitFor", "actor, 0x03", "AE 01 03"),
            new(3, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        var still = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);
        Assert.Null(still.ResumeOffset);
        Assert.True(still.ReachedStop);

        var room = Path.Combine(Folder, "02.LFL");
        if (!File.Exists(room)) return;
        runtime.CurrentRoom = 2;
        runtime.WalkBoxes = LoomWalkBoxReader.Read(File.ReadAllBytes(room));
        var place = new LoomActorTable.Placement(2, runtime.WalkBoxes, null, null);
        runtime.Actors.Put(3, 820, 60, 2, place, ego: 1);
        runtime.Actors.Walk(3, 860, 125, place, "walk");
        var walking = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);
        Assert.Equal(0, walking.ResumeOffset);
        Assert.Contains(walking.Trace, line => line.StartsWith("waiting for actor 3 to stop moving", StringComparison.Ordinal));
    }

    [Fact]
    public void AWaitForMessageGoesOnAtOnceWhenNoMessageIsShown()
    {
        // o5_wait breaks only while VAR_HAVE_MSG is set (LoomMessageTests covers the wait itself).
        var script = Script(
            new(0, 0, "waitFor", "message", "AE 02"),
            new(2, 0, "stopObjectCode", "", "00"));
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());

        var run = new LoomBoundedScriptExecutor().Execute(script, runtime.ObjectStates, [], runtime: runtime);

        Assert.Null(run.ResumeOffset);
        Assert.True(run.ReachedStop);
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
    public void ACatalogVisitDuringACutsceneEndsItSoClicksAreRead()
    {
        if (Load() is not { } data) return;
        var (game, index) = data;
        var session = new LoomExplorationSession(game, index, completeBoot: true);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 8));
        // The trial (script 046) is a cutscene: script 002 turns input off at its start.
        session.ActivateObjectVerb(255, 0x38);
        for (var frame = 0; frame < 300; frame++) session.AdvanceFrame();
        Assert.True(session.InCutscene);
        Assert.Contains("not sent, because user input is off", string.Join(" ", session.Click(160, 100).Uncertainties));

        // Leaving from the catalog stops script 046 and ends its cutscene (script 003 turns input back on).
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 2));
        Assert.False(session.InCutscene);
        Assert.Contains("catalog visit to Room 02: stops script 046, which began a cutscene (data 1)", session.SessionTrace);
        Assert.Contains("catalog visit to Room 02: endCutscene runs script 3 with data 1", session.SessionTrace);
        Assert.Contains(session.Snapshot.Uncertainties, item => item.Contains("selected from the catalog while a cutscene played", StringComparison.Ordinal));
        for (var frame = 0; frame < 30; frame++) session.AdvanceFrame();
        Assert.Equal(2, session.Snapshot.RoomId);
        var click = session.Click(160, 100);
        Assert.DoesNotContain(click.Uncertainties, item => item.Contains("not sent", StringComparison.Ordinal));
    }

    [Fact]
    public void AWalkIntoRoom37sBox8PlaysTheFirewoodScene()
    {
        if (Load() is not { } data) return;
        var (game, index) = data;
        var session = new LoomExplorationSession(game, index);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 36));
        // Room 36 object 599's doorway: loadRoomWithEgo into Room 37, walk to (24, 101) in box 0. A first visit
        // (bit[0xD9A] is 0) starts local 200, which watches the ego's walk box.
        session.ActivateObjectVerb(599, 0x38);
        Assert.Equal(37, session.Snapshot.RoomId);
        for (var frame = 0; frame < 100 && session.EgoMoving; frame++) session.AdvanceFrame();
        var ego = session.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((37, 24, 101, 0), (ego.Room, ego.X, ego.Y, ego.WalkBox));
        Assert.Null(ego.Estimate);

        // The game's click in box 8: the walk enters box 8, local 200 chains to script 086.
        session.Click(109, 102);
        var rooms = new List<int>();
        var lines = new List<string>();
        for (var frame = 0; frame < 2000 && (frame < 20 || session.InCutscene); frame++)
        {
            session.AdvanceFrame();
            if (session.Snapshot.RoomId is int room && (rooms.Count == 0 || rooms[^1] != room)) rooms.Add(room);
            if (session.Messages.Talk is { } talk && talk.Lines.Any(line => line.Trim().Length > 0) &&
                (lines.Count == 0 || lines[^1] != talk.Lines[0])) lines.Add(talk.Lines[0]);
        }

        Assert.Contains("Room 37 local script 200 chains to script 86", session.SessionTrace);
        Assert.Contains("endCutscene at Script 86 +0x055C runs script 3", session.SessionTrace);
        Assert.Equal([37, 58, 37, 38], rooms);
        // All 13 of script 086's printed lines (the last print, +0x0551, is empty).
        Assert.Equal(13, lines.Count);
        Assert.Equal("Vaya, vaya, vaya.", lines[0]);
        Assert.Equal("Y no vendré sólo.", lines[^1]);
        Assert.False(session.InCutscene);
        Assert.Equal((ushort)1, session.Snapshot.KnownGlobalVariables["bit[0xD9A]"]);
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
        // Script 002 freezes every other slot, including the input loop (script 006);
        // the cutscene's own script keeps running.
        Assert.Contains("freezeScripts 0x7F at Script 02 +0x0004: frozen: Room 10 Object 295 Verb 0x38, Script 06, Room 10 local script 200",
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
