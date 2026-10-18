using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedCatalogTests
{
    private static (GameLibrary Game, ScummV2DataSource Data)? Fixture()
    {
        var path = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(path)) return null;
        var game = new GameFolderScanner().Scan(path);
        return (game, (ScummV2DataSource)ScummDataSources.For(game));
    }

    private static IEnumerable<ScummScriptDisassembly> Blocks(GameLibrary game, IScummDataSource data)
    {
        foreach (var id in data.GlobalScriptIds())
            if (data.GlobalScript(id) is { } script) yield return script;
        foreach (var room in game.Rooms.Select(r => r.ResourceId).OfType<int>().Distinct())
        {
            if (data.RoomEntryScript(room) is { } entry) yield return entry;
            if (data.RoomExitScript(room) is { } exit) yield return exit;
            foreach (var id in data.RoomLocalScriptIds(room))
                if (data.RoomLocalScript(room, id) is { } local) yield return local;
            var records = data.ReadRoom(room).Objects;
            var tables = data.ReadObjectVerbs(room, records);
            foreach (var record in records.GroupBy(r => r.Id).Select(g => g.Last()))
                foreach (var verb in tables.GetValueOrDefault(record.Id) ?? [])
                    yield return data.ReadObjectVerb(room, record, verb.VerbId, out _);
        }
    }

    [Fact]
    public void CompleteOwnEditionCodeCensusHasOnlyOneParameterizedPickupHelper()
    {
        if (Fixture() is not { } fixture) return;
        var blocks = Blocks(fixture.Game, fixture.Data).ToArray();
        Assert.Equal(1092, blocks.Length);
        Assert.All(blocks, block => Assert.True(block.HasCompleteControlFlow, block.Context));
        Assert.Equal(49, blocks.Sum(b => b.Instructions.Count(i => i.Operation == "pickupObject")));
        Assert.DoesNotContain(blocks.SelectMany(b => b.Instructions), i =>
            i.Operation == "setState" && i.Operands.Contains("pickupable", StringComparison.OrdinalIgnoreCase));
        var calls = blocks.SelectMany(b => b.Instructions.Where(i => i.Operation is "startScript" or "chainScript")).ToArray();
        Assert.All(calls, instruction => Assert.StartsWith("0x", instruction.Operands));
        Assert.Equal(8, calls.Count(i => i.Operands == "0x74"));
        var globalPickups = fixture.Data.GlobalScriptIds().SelectMany(id =>
            (fixture.Data.GlobalScript(id)?.Instructions ?? []).Where(i => i.Operation == "pickupObject").Select(i => (Id: id, Instruction: i))).ToArray();
        var pickup = Assert.Single(globalPickups);
        Assert.Equal(116, pickup.Id);
        Assert.Equal(0x45, pickup.Instruction.Offset);
        Assert.Equal(ScummVars.V2.ActiveObject1, pickup.Instruction.Operands);
    }

    [Theory]
    [InlineData(34, 0, true)]
    [InlineData(35, 35, false)]
    [InlineData(36, 0, true)]
    [InlineData(37, 0, true)]
    [InlineData(38, 0, true)]
    public void NativeCallerGuardSeparatesThePiggyBankFromItsInventoryContents(int objectId, int callerArgument, bool pickedUp)
    {
        if (Fixture() is not { } fixture) return;
        var runtime = new ScummScriptRuntimeState(fixture.Data.ReadIndexedObjects().States) { EngineVersion = fixture.Game.Version };
        runtime.GlobalVariables[ScummVars.V2.Ego] = 3;
        runtime.GlobalVariables[ScummVars.V2.ActiveObject1] = (ushort)objectId;
        runtime.GlobalVariables["var[0x07B]"] = (ushort)callerArgument;
        var run = new ScummBoundedScriptExecutor().Execute(fixture.Data.GlobalScript(116)!, runtime.ObjectStates,
            fixture.Data.ReadRoom(26).Objects, runtime: runtime, startOffset: 0x3F);
        Assert.Null(run.UnresolvedReason);
        Assert.True(run.ReachedStop);
        Assert.Equal(pickedUp, runtime.Inventory.Contains(objectId));
    }

    [Fact]
    public void CompleteNamedRosterCanBeGrantedAndUndoneWithoutGrantingTheSceneContainer()
    {
        if (Fixture() is not { } fixture) return;
        var session = ScummGameSession.BootForPlay(fixture.Game, out _);
        ManiacCutsceneSetup.PrepareIfNeeded(fixture.Game, session);
        for (var frame = 0; frame < 2500 && !session.CaptureInventoryStatus().CanEdit; frame++) session.AdvanceFrame();
        var view = session.CaptureInventoryStatus();
        Assert.True(view.CanEdit, view.Refusal);
        var runtime = (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        string NativeState() => JsonSerializer.Serialize(new
        {
            Slots = runtime.InventorySlots, Rooms = runtime.InventoryRooms.OrderBy(p => p.Key),
            Owners = runtime.ObjectOwners.OrderBy(p => p.Key), States = runtime.ObjectStates.OrderBy(p => p.Key),
            Classes = runtime.ObjectClasses.OrderBy(p => p.Key), Members = runtime.Inventory.Order(),
            Words = runtime.GlobalVariables.OrderBy(p => p.Key), Actors = runtime.Actors.Snapshot()
        });
        var before = NativeState();
        Assert.Equal(48, view.Catalog.Count);
        Assert.DoesNotContain(view.Catalog, item => item.ObjectId == 35);
        var codeIds = fixture.Game.Rooms.Select(r => r.ResourceId).OfType<int>()
            .SelectMany(room => fixture.Data.ReadRoom(room).Objects).Select(record => record.Id).ToHashSet();
        var expected = fixture.Data.ReadIndexedObjects().States.Where(pair => (pair.Value & 1) != 0)
            .Select(pair => pair.Key).Where(codeIds.Contains).Order();
        Assert.Equal(expected, view.Catalog.Select(item => item.ObjectId).Order());
        Assert.False(session.ApplyInventoryEdits(view, [new(55, 5), new(35, 5)]).Applied);
        Assert.Equal(before, NativeState());
        var applied = session.ApplyInventoryEdits(view, view.Catalog.Select(item => new ScummInventoryTransfer(item.ObjectId, 5)).ToArray());
        Assert.True(applied.Applied, applied.Refusal);
        Assert.Equal(48, session.CaptureInventoryStatus().Items.Count);
        foreach (var item in view.Catalog)
        {
            Assert.Equal(item.SourceRoomId, runtime.InventoryRooms[item.ObjectId]);
            var records = fixture.Data.ReadRoom(item.SourceRoomId).Objects;
            var verbs = fixture.Data.ReadObjectVerbs(item.SourceRoomId, records).GetValueOrDefault(item.ObjectId) ?? [];
            Assert.NotEmpty(verbs);
            Assert.Contains(verbs, verb => runtime.VerbEntrypoint!(item.ObjectId, verb.VerbId) > 0);
        }
        Assert.True(session.UndoInventoryTransfers(applied.Undo!).Applied);
        Assert.Equal(before, NativeState());
    }
}

