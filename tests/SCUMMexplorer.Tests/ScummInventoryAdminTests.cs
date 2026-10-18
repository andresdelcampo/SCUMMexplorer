using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummInventoryAdminTests
{
    public static TheoryData<string, int, int> Copies => new()
    {
        { "mmansion ega en", 55, 48 }, { "mmansionE ega es", 55, 48 },
        { "zak ega en", 15, 76 }, { "zakE ega en", 15, 76 }
    };

    private static ScummGameSession? Ready(string folder)
    {
        var path = Path.Combine(TestGames.Root, folder);
        if (!Directory.Exists(path)) return null;
        var game = new GameFolderScanner().Scan(path);
        var session = ScummGameSession.BootForPlay(game, out _);
        if (folder.StartsWith("mmansion")) ManiacCutsceneSetup.PrepareIfNeeded(game, session);
        for (int i = 0; i < 2500 && !session.CaptureInventoryStatus().CanEdit; i++) session.AdvanceFrame();
        Assert.True(session.CaptureInventoryStatus().CanEdit, session.CaptureInventoryStatus().Refusal);
        return session;
    }

    private static ScummScriptRuntimeState Runtime(ScummSession session) =>
        (ScummScriptRuntimeState)typeof(ScummSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;

    private static string Relevant(ScummScriptRuntimeState runtime) => JsonSerializer.Serialize(new
    {
        Slots = runtime.InventorySlots, Rooms = runtime.InventoryRooms.OrderBy(x => x.Key),
        Owners = runtime.ObjectOwners.OrderBy(x => x.Key), States = runtime.ObjectStates.OrderBy(x => x.Key),
        Classes = runtime.ObjectClasses.OrderBy(x => x.Key), Members = runtime.Inventory.Order(),
        Variables = runtime.GlobalVariables.OrderBy(x => x.Key), Actors = runtime.Actors.Snapshot(),
        Positions = runtime.ObjectPositions.OrderBy(x => x.Key), SetStates = runtime.StatesSetInRoom.Order()
    });

    [Theory, MemberData(nameof(Copies))]
    public void SourceCatalogGrantsUnearnedItemAndUndoRestoresExactNativeState(string folder, int objectId, int catalogCount)
    {
        if (Ready(folder) is not {} session) return;
        var runtime = Runtime(session);
        var before = Relevant(runtime);
        var view = session.CaptureInventoryStatus();
        Assert.Equal(catalogCount, view.Catalog.Count);
        Assert.Equal(80, view.Capacity);
        Assert.Equal(view.Capacity - view.Items.Count, view.FreeSlots);
        Assert.Equal(view.Catalog.Count, view.Catalog.Select(x => x.ObjectId).Distinct().Count());
        var item = Assert.Single(view.Catalog, x => x.ObjectId == objectId);
        Assert.Equal(1, item.SourceRoomId);
        Assert.DoesNotContain(view.Items, x => x.ObjectId == objectId);
        if (folder.StartsWith("mmansion")) Assert.Equal(Enumerable.Range(1, 7), view.Characters.Select(x => x.ActorId));
        var recipient = 2;
        var result = session.ApplyInventoryEdits(view, [new(objectId, recipient)]);
        Assert.True(result.Applied, result.Refusal);
        var held = Assert.Single(session.CaptureInventoryStatus().Items, x => x.ObjectId == objectId);
        Assert.Equal(recipient, held.OwnerId);
        Assert.Equal(item.SourceRoomId, held.RetainedRoomId);
        Assert.Equal(10, runtime.ObjectStates[objectId] & 10);
        Assert.Contains(objectId, runtime.Inventory);
        Assert.False(session.ApplyInventoryEdits(view, [new(objectId, 1)]).Applied);
        Assert.True(session.UndoInventoryTransfers(result.Undo!).Applied);
        Assert.Equal(before, Relevant(runtime));
        Assert.DoesNotContain(session.CaptureInventoryStatus().Items, x => x.ObjectId == objectId);
        Assert.False(session.UndoInventoryTransfers(result.Undo!).Applied);
    }

    [Theory, MemberData(nameof(Copies))]
    public void FullNativeInventoryRejectsMixedBatchBeforeAnyWriteAndAllowsHeldTransfers(string folder, int objectId, int _)
    {
        if (Ready(folder) is not {} session) return;
        var runtime = Runtime(session);
        var initial = session.CaptureInventoryStatus();
        var grant = session.ApplyInventoryEdits(initial, [new(objectId, initial.CurrentActorId)]);
        Assert.True(grant.Applied, grant.Refusal);
        for (int slot = 0; slot < runtime.InventorySlots.Length; slot++)
            if (runtime.InventorySlots[slot] == 0) runtime.InventorySlots[slot] = 10000 + slot;
        var view = session.CaptureInventoryStatus();
        Assert.Equal(0, view.FreeSlots);
        var unheld = view.Catalog.First(x => !string.IsNullOrWhiteSpace(x.Name) && !runtime.InventorySlots.Contains(x.ObjectId));
        var before = Relevant(runtime);
        var failed = session.ApplyInventoryEdits(view, [new(objectId, 2), new(unheld.ObjectId, 2)]);
        Assert.False(failed.Applied);
        Assert.Contains("full", failed.Refusal);
        Assert.Equal(before, Relevant(runtime));
        var moved = session.ApplyInventoryEdits(view, [new(objectId, 2)]);
        Assert.True(moved.Applied, moved.Refusal);
        Assert.True(session.UndoInventoryTransfers(moved.Undo!).Applied);
        Assert.Equal(before, Relevant(runtime));
    }

    [Theory, MemberData(nameof(Copies))]
    public void GrantsRejectInvalidDuplicateForeignAndUnheldStateStaleBatches(string folder, int objectId, int _)
    {
        if (Ready(folder) is not {} session) return;
        var runtime = Runtime(session);
        var view = session.CaptureInventoryStatus();
        var before = Relevant(runtime);
        Assert.False(session.ApplyInventoryEdits(view, [new(objectId, 9)]).Applied);
        Assert.False(session.ApplyInventoryEdits(view, [new(objectId, 2), new(99999, 2)]).Applied);
        Assert.False(session.ApplyInventoryEdits(view, [new(objectId, 2), new(objectId, 1)]).Applied);
        Assert.Equal(before, Relevant(runtime));
        runtime.ObjectStates[objectId] ^= 4;
        Assert.False(session.ApplyInventoryEdits(view, [new(objectId, 2)]).Applied);
        view = session.CaptureInventoryStatus();
        session.AdvanceFrame();
        Assert.False(session.ApplyInventoryEdits(view, [new(objectId, 2)]).Applied);
    }

    [Theory]
    [InlineData("mmansion ega en",55)]
    [InlineData("mmansionE ega es",55)]
    [InlineData("zak ega en",15)]
    [InlineData("zakE ega en",15)]
    public void UndoRestoresMissingOwnerAndStateKeysInsteadOfAddingDefaults(string folder, int objectId)
    {
        if (Ready(folder) is not {} session) return;
        var runtime = Runtime(session);
        runtime.RemoveObjectOwner(objectId); runtime.RemoveObjectState(objectId);
        var original = Relevant(runtime);
        var changed = session.ApplyInventoryEdits(session.CaptureInventoryStatus(), [new(objectId, 2)]);
        Assert.True(changed.Applied, changed.Refusal);
        Assert.True(session.UndoInventoryTransfers(changed.Undo!).Applied);
        Assert.Equal(original, Relevant(runtime));
        Assert.False(runtime.ObjectOwners.ContainsKey(objectId));
        Assert.False(runtime.ObjectStates.ContainsKey(objectId));
    }

    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void GrantedBillRunsItsRetainedNativeReadCodeAwayFromSourceRoom(string folder)
    {
        if (Ready(folder) is not {} session) return;
        var runtime = Runtime(session);
        var result = session.ApplyInventoryEdits(session.CaptureInventoryStatus(), [new(15, 1)]);
        Assert.True(result.Applied, result.Refusal);
        var game = new GameFolderScanner().Scan(Path.Combine(TestGames.Root, folder));
        var room = game.Rooms.Single(x => x.ResourceId == 2);
        session.VisitRoom(room); // Explicit exploration visit, not earned travel.
        for (int i = 0; i < 500 && !session.PlayerHasControl; i++) session.AdvanceFrame();
        var entry = runtime.VerbEntrypoint!(15, 12);
        Assert.True(entry > 0);
        Assert.Equal(1, runtime.InventoryRooms[15]);
        runtime.Sentences.Add((12, 15, 0));
        for (int i = 0; i < 500 && !session.Messages.OnScreen.Any(x => x.Text.Contains("1138")); i++)
        {
            if (session.CutsceneSkippable) session.PressKey(27);
            session.AdvanceFrame();
        }
        Assert.Contains(session.Messages.OnScreen, x => x.Text.Contains("1138"));
    }
}

