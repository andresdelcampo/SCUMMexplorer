namespace SCUMMexplorer.Tests;

/// <summary>
/// S4b (14 October 2026): engine behaviour the Indiana Jones and the Last Crusade EGA boot needed, each pinned from the ScummVM source named in its
/// comment (<c>engines/scumm</c>: script_v4.cpp <c>o4_pickupObject</c>, script_v5.cpp <c>o5_doSentence</c>/<c>o5_saveRestoreVerbs</c>/<c>o5_pseudoRoom</c>,
/// object.cpp <c>findInventory</c>/<c>getInventoryCount</c>, player_ad.cpp). They use synthetic scripts and need no game data.
/// </summary>
public sealed class ScummEngineOpcodeTests
{
    private static ScummScriptDisassembly Script(params ScummScriptInstruction[] instructions) =>
        new(1, 1, 0, 64, 32, instructions.Select((item, index) => item with { Offset = index }).ToArray(), null, null, [], []);

    private static int _next;
    // Every synthetic instruction is one byte long, so each starts one after the last of its script (Script renumbers them).
    private static ScummScriptInstruction I(string operation, string operands = "") => new(_next++, 0, operation, operands, "00");

    private static ScummScriptRuntimeState Runtime(params (int Object, byte Owner)[] owners)
    {
        var runtime = new ScummScriptRuntimeState(new Dictionary<int, byte>(owners.ToDictionary(o => o.Object, _ => (byte)0)),
            owners.ToDictionary(o => o.Object, o => o.Owner));
        runtime.SetGlobalVariable(ScummVars.Ego, 1);
        runtime.CurrentRoom = 5;
        return runtime;
    }

    private static ScummRoomObjectRecord Obj(int id) => new(id, 0, 0, 0, 8, 8, 0, 0, 0, 0, 0, 0, null);

    private static ScummBoundedScriptResult Run(ScummScriptDisassembly script, ScummScriptRuntimeState runtime,
        IReadOnlyList<ScummRoomObjectRecord>? objects = null, List<ScummScriptEvent>? seen = null) =>
        new ScummBoundedScriptExecutor().Execute(script, runtime.ObjectStates, objects ?? [], runtime: runtime,
            onEvent: (scriptEvent, _) => { seen?.Add(scriptEvent); return ""; });

    [Fact]
    public void PickupObjectTakesTheObjectIntoTheInventoryAndRunsTheInventoryScript()
    {
        // o4_pickupObject: owner = VAR_EGO, class 24 (untouchable) set, state 1, draw queue cleared, runInventoryScript(1).
        var runtime = Runtime((100, 0x0F));
        runtime.SetGlobalVariable(ScummVars.InventoryScript, 12);
        var seen = new List<ScummScriptEvent>();

        var run = Run(Script(I("pickupObject", "0x0064"), I("stopObjectCode")), runtime, [Obj(100)], seen);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal((byte)1, runtime.ObjectOwners[100]);
        Assert.Equal((byte)1, runtime.ObjectStates[100]);
        Assert.True(ScummObjectClasses.Has(runtime.ObjectClasses[100], ScummObjectClasses.Untouchable));
        Assert.Contains(100, runtime.InventorySlots);
        Assert.Equal(5, runtime.InventoryRooms[100]);
        var started = Assert.Single(seen);
        Assert.Equal(ScummScriptEventKind.ScriptStarted, started.Kind);
        Assert.Equal(12, started.TargetId);
        Assert.Contains("[0x0001]", started.Operands);
    }

    [Fact]
    public void PickupObjectDoesNothingForAnObjectTheRoomLacksOrOneAlreadyHeld()
    {
        var runtime = Runtime((100, 0x0F), (101, 1));
        runtime.SetGlobalVariable(ScummVars.InventoryScript, 12);
        var seen = new List<ScummScriptEvent>();

        // 100 is not among the room's objects; 101 is, but its owner is already an actor.
        Run(Script(I("pickupObject", "0x0064"), I("pickupObject", "0x0065"), I("stopObjectCode")), runtime, [Obj(101)], seen);

        Assert.Equal((byte)0x0F, runtime.ObjectOwners[100]);
        Assert.Empty(seen);
        Assert.DoesNotContain(100, runtime.InventorySlots);
        Assert.DoesNotContain(101, runtime.InventorySlots);
    }

    [Fact]
    public void InventoryQueriesReadTheInventoryArrayNotTheOwnerTable()
    {
        // object.cpp findInventory/getInventoryCount scan _inventory; getObjectOwner reads the owner table. Object 7 is owned by actor 1 through
        // setOwnerOf alone, so it is not counted.
        var runtime = Runtime((5, 1), (6, 1), (7, 1));
        runtime.AddToInventorySlots(5);
        runtime.AddToInventorySlots(6);
        var script = Script(
            I("getInventoryCount", "var[0x010], 0x01"),
            I("findInventory", "var[0x011], 0x01, 0x02"),
            I("findInventory", "var[0x012], 0x01, 0x03"),
            I("getObjectOwner", "var[0x013], 0x0007"),
            I("stopObjectCode"));

        var run = Run(script, runtime);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal(2, runtime.GlobalVariables["var[0x010]"]);
        Assert.Equal(6, runtime.GlobalVariables["var[0x011]"]);
        Assert.Equal(0, runtime.GlobalVariables["var[0x012]"]);
        Assert.Equal(1, runtime.GlobalVariables["var[0x013]"]);
    }

    [Fact]
    public void SetOwnerOfZeroFreesTheInventorySlot()
    {
        var runtime = Runtime((5, 1));
        runtime.AddToInventorySlots(5);

        Run(Script(I("setOwnerOf", "0x0005, 0x00"), I("stopObjectCode")), runtime);

        Assert.DoesNotContain(5, runtime.InventorySlots);
    }

    [Fact]
    public void DoSentenceQueuesAndTheFeTerminatorEmptiesTheStackAndStopsTheSentenceScript()
    {
        var runtime = Runtime();
        runtime.SetGlobalVariable(ScummVars.SentenceScript, 2);
        var seen = new List<ScummScriptEvent>();

        Run(Script(I("doSentence", "0x0A, 0x0342, 0x0000"), I("doSentence", "0x01, 0x0010, 0x0020")), runtime, [], seen);
        Assert.Equal([(0x0A, 0x342, 0), (1, 0x10, 0x20)], runtime.Sentences);

        // o5_doSentence: verb 0xFE returns before it reads the objects, so the instruction has one operand.
        Run(Script(I("doSentence", "0xFE"), I("stopObjectCode")), runtime, [], seen);
        Assert.Empty(runtime.Sentences);
        var stop = Assert.Single(seen, e => e.Kind == ScummScriptEventKind.ScriptStopped);
        Assert.Equal(2, stop.TargetId);
    }

    [Fact]
    public void ADoSentenceFeInTheBytecodeCarriesNoObjectOperands()
    {
        // 0x19 (doSentence) with a literal verb 0xFE, then stopObjectCode (0x00): the objects are not read.
        var decoded = ScummScriptDisassembler.Disassemble([0x19, 0xFE, 0x00], ScummScriptDialect.V3Indy3);

        Assert.Collection(decoded.Instructions,
            first => { Assert.Equal("doSentence", first.Operation); Assert.Equal("0xFE", first.Operands); },
            second => Assert.Equal("stopObjectCode", second.Operation));
    }

    [Fact]
    public void WaitForSentenceWaitsWhileASentenceIsQueuedOrTheSentenceScriptRuns()
    {
        var runtime = Runtime();
        runtime.SetGlobalVariable(ScummVars.SentenceScript, 2);
        var running = false;
        runtime.ScriptRunning = number => (running && number == 2, null);
        var script = Script(I("waitForSentence"), I("stopObjectCode"));

        runtime.Sentences.Add((1, 2, 3));
        Assert.Equal(0, Run(script, runtime).ResumeOffset);
        runtime.Sentences.Clear();
        running = true;
        Assert.Equal(0, Run(script, runtime).ResumeOffset);
        running = false;
        var finished = Run(script, runtime);
        Assert.Null(finished.ResumeOffset);
        Assert.True(finished.ReachedStop);
    }

    [Fact]
    public void WaitForActorIsAWaitInTheIndianaJonesDialectButNotInLoom()
    {
        var runtime = Runtime();
        runtime.Actors.Walk(1, 100, 50, new(5, null, null, new Dictionary<int, uint>()), "test");

        var waiting = Run(Script(I("waitForActor", "0x01"), I("stopObjectCode")), runtime);
        var moving = runtime.Actors.Moving(1) != 0;

        // LOOM's 0x3B has no operand; it is not a wait and stays as the reader reports it.
        Assert.Equal(moving ? 0 : null, waiting.ResumeOffset);
        Assert.NotNull(Run(Script(I("waitForActor", "LOOM no-op")), runtime).UnresolvedReason);
    }

    [Fact]
    public void SaveVerbsHidesAVerbFromItsLookupAndRestoreVerbsBringsItBack()
    {
        // o5_saveRestoreVerbs: save gives the verb a save id (getVerbSlot(verb, 0) no longer finds it), restore kills a verb now in use and clears the id.
        var verbs = new ScummVerbTable();
        verbs.Apply(0x65, [("new", [], null), ("at", [8, 144], null), ("name", [], "Open"), ("on", [], null)], _ => true, 5, null);
        Assert.NotNull(verbs.Find(0x65));

        verbs.SaveRestoreVerbs(1, 0x60, 0x70, 2);
        Assert.Null(verbs.Find(0x65));
        Assert.Empty(verbs.Snapshot());

        // A verb of the same number made meanwhile is replaced by the restored one.
        verbs.Apply(0x65, [("new", [], null), ("name", [], "Other"), ("on", [], null)], _ => true, 5, null);
        verbs.SaveRestoreVerbs(2, 0x65, 0x65, 2);
        Assert.Equal("Open", verbs.Find(0x65)!.Name);

        verbs.SaveRestoreVerbs(1, 0x65, 0x65, 3);
        verbs.SaveRestoreVerbs(3, 0x65, 0x65, 3);
        Assert.Null(verbs.Find(0x65));
        Assert.Empty(verbs.Snapshot());
    }

    [Fact]
    public void PseudoRoomMapsNumbersFrom0x80UpToTheRealRoom()
    {
        // o5_pseudoRoom: _resourceMapper[j & 0x7F] = room for every following byte j >= 0x80; the disassembler prints j & 0x7F.
        var runtime = Runtime();

        var run = Run(Script(I("pseudoRoom", "54, [5, 6, inventory]"), I("stopObjectCode")), runtime);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal(54, runtime.PseudoRooms[0x85]);
        Assert.Equal(54, runtime.PseudoRooms[0x86]);
        Assert.Equal(2, runtime.PseudoRooms.Count);
    }

    [Fact]
    public void GetVerbEntrypointAsksTheSession()
    {
        var runtime = Runtime();
        runtime.VerbEntrypoint = (obj, verb) => obj == 77 && verb == 3 ? 21 : 0;
        var script = Script(I("getVerbEntrypoint", "var[0x010], 0x004D, 0x0003"), I("getVerbEntrypoint", "var[0x011], 0x004D, 0x0004"), I("stopObjectCode"));

        var run = Run(script, runtime);

        Assert.Null(run.UnresolvedReason);
        Assert.Equal(21, runtime.GlobalVariables["var[0x010]"]);
        Assert.Equal(0, runtime.GlobalVariables["var[0x011]"]);
    }

}

