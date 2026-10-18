using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacClassicCatalogTests
{
    private static (GameLibrary Game, ScummV2DataSource Data)? Fixture()
    {
        var path = Path.Combine(TestGames.Root, "mmansion ega en");
        if (!Directory.Exists(path)) return null;
        var game = new GameFolderScanner().Scan(path);
        return (game, (ScummV2DataSource)ScummDataSources.For(game));
    }

    private static IEnumerable<(int? Object, int? Verb, ScummScriptDisassembly Script)> Blocks(GameLibrary game, ScummV2DataSource data)
    {
        foreach (var id in data.GlobalScriptIds())
            if (data.GlobalScript(id) is {} script) yield return (null, null, script);
        foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct())
        {
            if (data.RoomEntryScript(room) is {} entry) yield return (null, null, entry);
            if (data.RoomExitScript(room) is {} exit) yield return (null, null, exit);
            var objects = data.ReadRoom(room).Objects;
            var verbs = data.ReadObjectVerbs(room, objects);
            foreach (var obj in objects.GroupBy(o => o.Id).Select(g => g.Last()))
                foreach (var verb in verbs.GetValueOrDefault(obj.Id) ?? [])
                    yield return (obj.Id, verb.VerbId, data.ReadObjectVerb(room, obj, verb.VerbId, out _));
        }
    }

    [Fact]
    public void SourceCensusClosesTheOnlyIndirectPickupAgainstAllHelperCallers()
    {
        if (Fixture() is not {} fixture) return;
        var (game, data) = fixture;
        var blocks = Blocks(game, data).ToArray();
        Assert.Equal(1089, blocks.Length);
        Assert.All(blocks, b => { Assert.True(b.Script.HasCompleteControlFlow); Assert.Null(b.Script.UndecodedOffset); });
        Assert.Equal(49, blocks.Sum(b => b.Script.Instructions.Count(i => i.Operation == "pickupObject")));
        Assert.DoesNotContain(blocks.SelectMany(b => b.Script.Instructions), i =>
            i.Operation == "setState" && (i.Operands.EndsWith(", Pickupable", StringComparison.Ordinal) ||
                i.Operands.EndsWith(", Unpickupable", StringComparison.Ordinal)));
        var indirectGlobals = blocks.Where(b => b.Object is null)
            .SelectMany(b => b.Script.Instructions.Where(i => i.Operation == "pickupObject").Select(i => (b.Script, Instruction: i))).ToArray();
        var pickup = Assert.Single(indirectGlobals);
        Assert.Equal(116, pickup.Script.ScriptId);
        Assert.Equal(0x3F, pickup.Instruction.Offset);
        Assert.Equal(ScummVars.V2.ActiveObject1, pickup.Instruction.Operands);
        var calls = blocks.SelectMany(b => b.Script.Instructions.Where(i =>
            i.Operation is "startScript" or "chainScript").Select(i => (b.Object, b.Verb, Instruction: i))).ToArray();
        Assert.All(calls, c => Assert.StartsWith("0x", c.Instruction.Operands));
        var helperCalls = calls.Where(c => c.Instruction.Operands.Split(',')[0].Trim() == "0x74").ToArray();
        Assert.Equal(8, helperCalls.Length);
        Assert.Equal(new int?[] { 34, 35, 36, 37, 38 }, helperCalls.Select(c => c.Object).Distinct().Order());
        Assert.Equal(new int?[] { 1, 2, 6, 14 }, helperCalls.Where(c => c.Object == 35).Select(c => c.Verb).Order());
        foreach (var block in blocks.Where(b => b.Object == 35))
            foreach (var call in block.Script.Instructions.Where(i => i.Operation == "startScript" && i.Operands == "0x74"))
                Assert.Equal("var[0x07B], 0x0023", block.Script.Instructions.Last(i => i.Offset < call.Offset && i.Operation == "move").Operands);
    }

    [Theory]
    [InlineData(34, 0, true)]
    [InlineData(35, 35, false)]
    [InlineData(36, 0, true)]
    [InlineData(37, 0, true)]
    [InlineData(38, 0, true)]
    public void NativeHelperGuardDistinguishesInventoryFromTheNailedPiggyBank(int objectId, int callerArgument, bool pickedUp)
    {
        if (Fixture() is not {} fixture) return;
        var (game, data) = fixture;
        var runtime = new ScummScriptRuntimeState(data.ReadIndexedObjects().States) { EngineVersion = game.Version };
        runtime.GlobalVariables[ScummVars.V2.Ego] = 3;
        runtime.GlobalVariables[ScummVars.V2.ActiveObject1] = (ushort)objectId;
        runtime.GlobalVariables["var[0x07B]"] = (ushort)callerArgument;
        // Begin at the source's actual argument guard. Earlier code can break the bank,
        // wait for text or summon Ed; none of those paths may bypass this pickup guard.
        var run = new ScummBoundedScriptExecutor().Execute(data.GlobalScript(116)!, runtime.ObjectStates,
            data.ReadRoom(26).Objects, runtime: runtime, startOffset: 0x39);
        Assert.Null(run.UnresolvedReason);
        Assert.True(run.ReachedStop);
        Assert.Equal(pickedUp, runtime.Inventory.Contains(objectId));
    }

    [Fact]
    public void CompleteCatalogMatchesCodeBearingNativeItemsAndEveryGrantRetainsItsVerbResource()
    {
        if (Fixture() is not {} fixture) return;
        var (game, data) = fixture;
        var objects = game.Rooms.Where(r => r.ResourceId.HasValue).SelectMany(r => data.ReadRoom(r.ResourceId!.Value).Objects)
            .Select(o => o.Id).ToHashSet();
        var indexed = data.ReadIndexedObjects().States.Where(p => (p.Value & 1) != 0).Select(p => p.Key).Order().ToArray();
        Assert.Equal(new[] { 1, 3, 4, 5, 22, 23, 24, 27 }, indexed.Where(id => !objects.Contains(id)));
        var expected = indexed.Where(objects.Contains).ToArray();
        Assert.Equal(48, expected.Length);
        var session = ScummGameSession.BootForPlay(game, out _);
        ManiacCutsceneSetup.PrepareIfNeeded(game, session);
        for (int n = 0; n < 2500 && !session.CaptureInventoryStatus().CanEdit; n++) session.AdvanceFrame();
        var view = session.CaptureInventoryStatus();
        Assert.True(view.CanEdit, view.Refusal);
        Assert.Equal(expected, view.Catalog.Select(i => i.ObjectId).Order());
        Assert.DoesNotContain(view.Catalog, i => i.ObjectId == 35);
        Assert.All(view.Catalog, i => Assert.False(string.IsNullOrWhiteSpace(i.Name)));
        var runtime = (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
        string State() => JsonSerializer.Serialize(new {
            runtime.InventorySlots, Rooms = runtime.InventoryRooms.OrderBy(p => p.Key), Owners = runtime.ObjectOwners.OrderBy(p => p.Key),
            States = runtime.ObjectStates.OrderBy(p => p.Key), Inventory = runtime.Inventory.Order(),
            Globals = runtime.GlobalVariables.OrderBy(p => p.Key), Actors = runtime.Actors.Snapshot()
        });
        var before = State();
        var applied = session.ApplyInventoryEdits(view, view.Catalog.Select(i => new ScummInventoryTransfer(i.ObjectId, 7)).ToArray());
        Assert.True(applied.Applied, applied.Refusal);
        foreach (var item in view.Catalog)
        {
            var held = Assert.Single(session.CaptureInventoryStatus().Items, i => i.ObjectId == item.ObjectId);
            Assert.Equal(7, held.OwnerId);
            Assert.Equal(item.SourceRoomId, held.RetainedRoomId);
            var source = data.ReadRoom(item.SourceRoomId);
            var record = source.Objects.Last(o => o.Id == item.ObjectId);
            var verbs = data.ReadObjectVerbs(item.SourceRoomId, source.Objects)[item.ObjectId];
            Assert.NotEmpty(verbs);
            foreach (var verb in verbs)
            {
                var native = data.ReadObjectVerb(item.SourceRoomId, record, verb.VerbId, out _);
                Assert.Equal(native.EntryOffset, runtime.VerbEntrypoint!(item.ObjectId, verb.VerbId));
            }
        }
        Assert.True(session.UndoInventoryTransfers(applied.Undo!).Applied);
        Assert.Equal(before, State());
    }
}
