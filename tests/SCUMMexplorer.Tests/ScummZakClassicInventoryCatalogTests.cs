using System.Reflection;
using System.Text.Json;

namespace SCUMMexplorer.Tests;

public sealed class ScummZakClassicInventoryCatalogTests
{
    private static ScummGameSession? Ready()
    {
        var path = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(path)) return null;
        var session = ScummGameSession.BootForPlay(new GameFolderScanner().Scan(path), out _);
        for (var f = 0; f < 2500 && !session.CaptureInventoryStatus().CanEdit; f++) session.AdvanceFrame();
        Assert.True(session.CaptureInventoryStatus().CanEdit);
        return session;
    }

    private static ScummScriptRuntimeState Runtime(ScummSession session) => (ScummScriptRuntimeState)
        typeof(ScummSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;

    private static string InventoryState(ScummScriptRuntimeState runtime) => JsonSerializer.Serialize(new
    {
        Slots = runtime.InventorySlots, Rooms = runtime.InventoryRooms.OrderBy(x => x.Key),
        Owners = runtime.ObjectOwners.OrderBy(x => x.Key), States = runtime.ObjectStates.OrderBy(x => x.Key),
        Members = runtime.Inventory.Order(), Variables = runtime.GlobalVariables.OrderBy(x => x.Key),
        Actors = runtime.Actors.Snapshot(), Classes = runtime.ObjectClasses.OrderBy(x => x.Key)
    });

    [Theory]
    [InlineData(12)]
    [InlineData(14)]
    public void NativeLetterVerbAcquiresTheCardAndNeverTheLetterHeader(int verb)
    {
        if (Ready() is not {} session) return;
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "zak ega en"));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 3)); // Explicit exploration visit.
        var action = session.ActivateObjectVerb(139, verb); // Native code, bypassing puzzle/walking prerequisites.
        Assert.NotNull(action.Run);
        var runtime = Runtime(session);
        Assert.Contains(27, runtime.InventorySlots);
        Assert.DoesNotContain(139, runtime.InventorySlots);
        Assert.Equal(1, runtime.ObjectOwners[27]);
        Assert.Equal(3, runtime.InventoryRooms[27]);
        Assert.Contains(session.CaptureInventoryStatus().Catalog, i => i.ObjectId == 27 && i.Name == "fan club card");
        Assert.DoesNotContain(session.CaptureInventoryStatus().Catalog, i => i.ObjectId == 139);
    }

    [Fact]
    public void NestedSpecialHandlersExposeBirdNestWithoutOfferingTheirOwnHeaders()
    {
        if (Ready() is not {} session) return;
        var data = ScummDataSources.For(new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "zak ega en")));
        var room = data.ReadRoom(13);
        var entry = data.ReadObjectVerb(13, room.Objects.Last(o => o.Id == 53), 14, out _);
        Assert.Contains(entry.Instructions, i => i.Offset == 0x28 && i.Operation == "doSentence" && i.Operands == "0xFD, 0x0113, 0x0000, sub=0x01");
        var middle = data.ReadObjectVerb(13, room.Objects.Last(o => o.Id == 275), 253, out _);
        Assert.Contains(middle.Instructions, i => i.Offset == 0x42 && i.Operation == "doSentence" && i.Operands == "0xFD, 0x0112, 0x0000, sub=0x01");
        var pickup = data.ReadObjectVerb(13, room.Objects.Last(o => o.Id == 274), 253, out _);
        Assert.Contains(pickup.Instructions, i => i.Offset == 0x13 && i.Operation == "pickupObject" && i.Operands == ScummVars.V2.ActiveObject1);
        var catalog = session.CaptureInventoryStatus().Catalog;
        Assert.Equal(70, catalog.Count);
        Assert.Contains(catalog, i => i.ObjectId == 53 && i.SourceRoomId == 13 && i.Name == "abandoned bird nest");
        foreach (var header in new[] {117,139,183,184,185,186,187,188,189,274,275,276,298,307,358,607,632})
            Assert.DoesNotContain(catalog, i => i.ObjectId == header);
    }

    [Fact]
    public void NativeNestRelayPreservesActiveObjectAcrossBothSpecialCalls()
    {
        if (Ready() is not {} session) return;
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "zak ega en"));
        var granted = session.ApplyInventoryEdits(session.CaptureInventoryStatus(), [new(52, 1)]);
        Assert.True(granted.Applied, granted.Refusal); // Explicit tool preparation, not earned travel/acquisition.
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 13));
        var runtime = Runtime(session);
        runtime.Sentences.Add((11, 53, 52));
        for (var f = 0; f < 500 && !runtime.InventorySlots.Contains(53); f++) session.AdvanceFrame();
        Assert.Contains(53, runtime.InventorySlots);
        Assert.Equal(1, runtime.ObjectOwners[53]);
        Assert.Equal(13, runtime.InventoryRooms[53]);
        Assert.DoesNotContain(274, runtime.InventorySlots);
        Assert.DoesNotContain(275, runtime.InventorySlots);
    }

    [Theory]
    [InlineData(183, 33)]
    [InlineData(184, 56)]
    [InlineData(185, 81)]
    [InlineData(186, 55)]
    [InlineData(187, 26)]
    [InlineData(188, 21)]
    [InlineData(189, 42)]
    public void NativeShopBranchNeverPicksTheDisplayHeader(int display, int actual)
    {
        var path = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(path)) return;
        var data = (ScummV2DataSource)ScummDataSources.For(new GameFolderScanner().Scan(path));
        var indexed = data.ReadIndexedObjects();
        var runtime = new ScummScriptRuntimeState(indexed.States, indexed.Owners)
            { EngineVersion = SCUMMexplorer.Core.Domain.ScummVersion.V1, CurrentRoom = 7 };
        runtime.SetGlobalVariable(ScummVars.V2.Ego, 1);
        runtime.SetGlobalVariable(ScummVars.V2.ActiveObject1, (ushort)display);
        runtime.SetObjectOwner(42, 15);
        var room = data.ReadRoom(7);
        var buy = data.ReadObjectVerb(7, room.Objects.Last(o => o.Id == display), 17, out _);
        var executor = new ScummBoundedScriptExecutor();
        var prefix = executor.Execute(buy, indexed.States, room.Objects, runtime: runtime, startOffset: buy.EntryOffset,
            onEvent: (e, _) => e.Kind == ScummScriptEventKind.ScriptStarted ? ScummBoundedScriptExecutor.StopAfterEvent : null);
        Assert.Null(prefix.UnresolvedReason);
        Assert.Equal(display == 189 ? actual : display, runtime.GlobalVariables[ScummVars.V2.ActiveObject1]);
        if (display != 189) Assert.Equal(actual, runtime.GlobalVariables["var[0x02E]"]);
        runtime.SetGlobalVariable("var[0x02D]", runtime.GlobalVariables[ScummVars.V2.ActiveObject1]);
        // Narrow prepared execution of the original purchase commit branch; no claim that the transaction was earned.
        var commit = executor.Execute(data.GlobalScript(24)!, runtime.ObjectStates, room.Objects,
            instructionLimit: 2, runtime: runtime, startOffset: 0x23E);
        Assert.DoesNotContain(display, runtime.InventorySlots);
        Assert.Equal(1, runtime.ObjectOwners[actual]);
        if (display == 189) Assert.Contains(actual, runtime.InventorySlots);
        else Assert.DoesNotContain(runtime.InventorySlots, id => id != 0);
    }

    [Fact]
    public void CompleteOwnEditionScriptCensusHasNoHiddenInventoryOpcodePaths()
    {
        if (Ready() is not {} session) return;
        var data = ScummDataSources.For(new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "zak ega en")));
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "zak ega en"));
        var scripts = data.GlobalScriptIds().Select(data.GlobalScript).OfType<ScummScriptDisassembly>().ToList();
        foreach (var roomId in game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct())
        {
            if (data.RoomEntryScript(roomId) is {} entry) scripts.Add(entry);
            if (data.RoomExitScript(roomId) is {} exit) scripts.Add(exit);
            var room = data.ReadRoom(roomId);
            var verbs = data.ReadObjectVerbs(roomId, room.Objects);
            foreach (var obj in room.Objects.GroupBy(o => o.Id).Select(g => g.Last()))
                foreach (var verb in verbs.GetValueOrDefault(obj.Id) ?? [])
                    scripts.Add(data.ReadObjectVerb(roomId, obj, verb.VerbId, out _));
        }
        Assert.Equal(1478, scripts.Count);
        Assert.Equal(new[] {150,151,153}, scripts.Where(s=>!s.HasCompleteControlFlow || s.DecodeNote is not null).Select(s=>s.ScriptId).Order());
        var instructions = scripts.SelectMany(s => s.Instructions).ToArray();
        Assert.Equal(61, instructions.Count(i => i.Operation == "pickupObject"));
        Assert.DoesNotContain(instructions, i => i.Operation is "startScript" or "chainScript" && new[] {150,151,153}.Contains(Convert.ToInt32(i.Operands[2..],16)));
        Assert.Single(instructions, i => i.Operation == "setState" && i.Operands.EndsWith(", Pickupable"));
        Assert.DoesNotContain(instructions, i => i.Operation == "setState" && i.Operands.EndsWith(", Unpickupable"));
        Assert.DoesNotContain(instructions, i => i.Operation is "startScript" or "chainScript" && !i.Operands.StartsWith("0x"));
        // Native fixed entry points: boot 1, input 4, sentence 2, room-entry 5.
        Assert.DoesNotContain(ScummSession.BootScript, new[] {150,151,153});
        Assert.DoesNotContain(session.PlayInputScript, new[] {150,151,153});
        Assert.All(new[] {2,5}, hook => Assert.DoesNotContain(hook, new[] {150,151,153}));
        Assert.All(scripts.Where(s=>new[] {150,151,153}.Contains(s.ScriptId) && s.Context is null),
            script=>Assert.DoesNotContain(script.Instructions, i=>i.Operation=="pickupObject" || i.Operation=="setState" && i.Operands.EndsWith("Pickupable")));
        var native = ((ScummV2DataSource)data).ReadIndexedObjects();
        var objectIds = game.Rooms.Where(r=>r.ResourceId.HasValue).SelectMany(r=>data.ReadRoom(r.ResourceId!.Value).Objects).Select(o=>o.Id).ToHashSet();
        var indexedItems = native.States.Where(x=>(x.Value&1)!=0 && objectIds.Contains(x.Key)).Select(x=>x.Key).ToArray();
        Assert.Equal(72,indexedItems.Length);
        // The four unflagged sources have explicit native pickup/writer evidence:
        // sign's bobby pin 31; nest relay 53; broom 68; boom-box helper 71.
        // Six indexed scene representations redirect pickup to an existing logical item.
        var sceneAliases = new[] {429,431,471,535,536,537};
        Assert.Equal(indexedItems.Except(sceneAliases).Concat(new[]{31,53,68,71}).Order(),session.CaptureInventoryStatus().Catalog.Select(x=>x.ObjectId).Order());
    }

    [Fact]
    public void DatCounterBoundCannotConcealAnotherInventoryTarget()
    {
        var path = Path.Combine(TestGames.Root, "zak ega en");
        if (!Directory.Exists(path)) return;
        var game = new GameFolderScanner().Scan(path);
        var data = ScummDataSources.For(game);
        var globals = data.GlobalScriptIds().ToDictionary(id => id, id => data.GlobalScript(id)!);
        var rooms = game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct()
            .ToDictionary(id => id, data.ReadRoom);
        var sources = new List<(int? Object, int? Verb, ScummScriptDisassembly Script)>();
        sources.AddRange(globals.Values.Select(s => ((int?)null, (int?)null, s)));
        foreach (var (roomId, room) in rooms)
        {
            if (data.RoomEntryScript(roomId) is {} entry) sources.Add((null, null, entry));
            if (data.RoomExitScript(roomId) is {} exit) sources.Add((null, null, exit));
            var verbs = data.ReadObjectVerbs(roomId, room.Objects);
            foreach (var obj in room.Objects.GroupBy(o => o.Id).Select(g => g.Last()))
                foreach (var verb in verbs.GetValueOrDefault(obj.Id) ?? [])
                    sources.Add((obj.Id, verb.VerbId, data.ReadObjectVerb(roomId, obj, verb.VerbId, out _)));
        }
        static int? Number(string text) => text.StartsWith("0x") && int.TryParse(text.AsSpan(2),
            System.Globalization.NumberStyles.HexNumber, null, out var value) ? value : null;
        var callers = sources.Where(s => s.Script.Instructions.Any(i => i.Operation is "startScript" or "chainScript" &&
            Number(i.Operands.Split(',')[0].Trim()) == 55)).ToArray();
        Assert.Equal(7, callers.Length);
        Assert.All(callers, c => Assert.Equal(71, c.Object));
        Assert.Equal(new[] {1,3,4,5,11,12,14}, callers.Select(c => c.Verb!.Value).Order());
        var dat = globals[55];
        var direct = dat.Instructions.Where(i => i.Operation == "pickupObject" ||
            i.Operation == "setState" && i.Operands.EndsWith(", Pickupable")).ToArray();
        Assert.Equal(new[] {0x52,0x54}, direct.Select(i => i.Offset));
        Assert.All(direct, i => Assert.StartsWith(ScummVars.V2.ActiveObject1, i.Operands));
        Assert.Equal(direct, dat.Instructions.Where(i => i.Operands.Contains(ScummVars.V2.ActiveObject1)));
        Assert.DoesNotContain(dat.Instructions, i => i.Operation.Contains("Indirect") || i.Operation == "setVarRange");

        // Independent structural closure of ALL later calls, including both sides of every
        // counter/state branch. This does not execute or pretend to finish the DAT loop.
        var queue = new Queue<(string Label, ScummScriptDisassembly Script, int Active, int After)>();
        var closure = new HashSet<string>();
        queue.Enqueue(("G55", dat, 71, 0x54));
        while (queue.TryDequeue(out var current))
        {
            if (!closure.Add(current.Label)) continue;
            var instructions = current.Script.Instructions.Where(i => i.Offset > current.After).ToArray();
            Assert.DoesNotContain(instructions, i => i.Operation == "pickupObject" ||
                i.Operation == "setState" && i.Operands.EndsWith(", Pickupable"));
            foreach (var instruction in instructions)
            {
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (instruction.Operation is "startScript" or "chainScript")
                {
                    var id = Number(parts[0]); Assert.NotNull(id);
                    queue.Enqueue(($"G{id}", globals[id.Value], current.Active, -1));
                }
                else if (instruction.Operation == "doSentence" && parts.Length == 4 && parts[3] == "sub=0x01")
                {
                    var verb = Number(parts[0]); Assert.NotNull(verb);
                    var id = Number(parts[1]) ?? (parts[1] == ScummVars.V2.ActiveObject1 ? current.Active : (int?)null);
                    Assert.NotNull(id);
                    var location = Assert.Single(rooms, r => r.Value.Objects.Any(o => o.Id == id));
                    var obj = location.Value.Objects.Last(o => o.Id == id);
                    var nativeVerb = verb == 250 ? 253 : verb.Value;
                    var next = data.ReadObjectVerb(location.Key, obj, nativeVerb, out _);
                    queue.Enqueue(($"O{location.Key}/{id}/{nativeVerb}", next,
                        verb is 250 or 253 or 254 ? current.Active : id.Value, -1));
                }
            }
        }
        Assert.Equal(new[] {"G3","G19","G44","G55","O7/183/253","O30/492/4","O30/492/253","O33/520/11","O33/521/253"}.Order(), closure.Order());
    }

    [Fact]
    public void EveryCatalogEntryGrantsAndUndoesWithoutChangingUnrelatedState()
    {
        if (Ready() is not {} session) return;
        var runtime = Runtime(session);
        var original = InventoryState(runtime);
        var data = ScummDataSources.For(new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "zak ega en")));
        foreach (var item in session.CaptureInventoryStatus().Catalog)
        {
            var changed = session.ApplyInventoryEdits(session.CaptureInventoryStatus(), [new(item.ObjectId, 2)]);
            Assert.True(changed.Applied, $"{item.ObjectId}: {changed.Refusal}");
            var held = Assert.Single(session.CaptureInventoryStatus().Items, i => i.ObjectId == item.ObjectId);
            Assert.Equal(2, held.OwnerId);
            Assert.Equal(item.SourceRoomId, held.RetainedRoomId);
            var source = data.ReadRoom(item.SourceRoomId);
            foreach (var verb in data.ReadObjectVerbs(item.SourceRoomId, source.Objects).GetValueOrDefault(item.ObjectId) ?? [])
                Assert.True(runtime.VerbEntrypoint!(item.ObjectId, verb.VerbId)>0,$"{item.ObjectId}: verb {verb.VerbId}");
            Assert.True(session.UndoInventoryTransfers(changed.Undo!).Applied);
            Assert.Equal(original, InventoryState(runtime));
        }
    }

    [Theory]
    [InlineData(53, 13, 11, 49, "Not in my hands!")]
    [InlineData(27, 3, 12, 0, "Member in good standing since 1997.")]
    public void GrantedItemRunsRetainedNativeVerbAwayFromItsSourceRoom(int objectId, int sourceRoom, int verb, int other, string message)
    {
        if (Ready() is not {} session) return;
        var runtime = Runtime(session);
        var edits = new List<ScummInventoryTransfer> { new(objectId, 1) };
        if (other > 0) edits.Add(new(other, 1));
        var changed = session.ApplyInventoryEdits(session.CaptureInventoryStatus(), edits);
        Assert.True(changed.Applied, changed.Refusal);
        Assert.Equal(sourceRoom, runtime.InventoryRooms[objectId]);
        Assert.True(runtime.VerbEntrypoint!(objectId, verb) > 0);
        runtime.Sentences.Add((verb, objectId, other));
        for (var f = 0; f < 500 && !session.Messages.OnScreen.Any(m => m.Text.Contains(message)); f++) session.AdvanceFrame();
        Assert.Equal(1, session.CurrentRoomId);
        Assert.Contains(session.Messages.OnScreen, m => m.Text.Contains(message));
    }
}


