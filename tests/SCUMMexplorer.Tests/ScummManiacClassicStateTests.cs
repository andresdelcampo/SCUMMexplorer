namespace SCUMMexplorer.Tests;

public sealed class ScummManiacClassicStateTests
{
    private static string Folder => Path.Combine(TestGames.Root,"mmansion ega en");

    [Fact]
    public void GenuineHeldKeyCanBeReassignedAndUndoneWithoutStoryOrActorChanges()
    {
        if(!Directory.Exists(Folder)) return;
        var session=ManiacClassicStateRoute.AcquireKey(Folder);
        var before=session.CaptureInventoryStatus();
        Assert.True(before.CanEdit,before.Refusal);
        Assert.Equal(new[]{1,3,5},before.Characters.Select(c=>c.ActorId).Order().ToArray());
        Assert.Equal("Dave",before.Characters.Single(c=>c.ActorId==3).Name);
        Assert.Equal("Syd",before.Characters.Single(c=>c.ActorId==1).Name);
        Assert.Equal("Bernard",before.Characters.Single(c=>c.ActorId==5).Name);
        Assert.Equal(3,before.Items.Single(i=>i.ObjectId==55).OwnerId);
        var variables=session.Snapshot.KnownGlobalVariables.OrderBy(p=>p.Key).ToArray();
        var states=session.Snapshot.ObjectStates.OrderBy(p=>p.Key).ToArray();
        var classes=session.Snapshot.ObjectClasses.OrderBy(p=>p.Key).ToArray();
        var owners=session.Snapshot.ObjectOwners.OrderBy(p=>p.Key).ToArray();
        var actors=System.Text.Json.JsonSerializer.Serialize(session.Actors);
        var scenes=session.CutscenesBegun;
        var applied=session.ApplyInventoryTransfers(before,[new(55,1)]);
        Assert.True(applied.Applied,applied.Refusal);
        Assert.NotNull(applied.Undo);
        Assert.Equal(1,session.CaptureInventoryStatus().Items.Single(i=>i.ObjectId==55).OwnerId);
        Assert.Equal(before.Items.Single(i=>i.ObjectId==55) with { OwnerId=1 },session.CaptureInventoryStatus().Items.Single(i=>i.ObjectId==55));
        Assert.Equal(owners.Where(p=>p.Key!=55),session.Snapshot.ObjectOwners.OrderBy(p=>p.Key).Where(p=>p.Key!=55));
        Assert.Equal(variables,session.Snapshot.KnownGlobalVariables.OrderBy(p=>p.Key).ToArray());
        Assert.Equal(states,session.Snapshot.ObjectStates.OrderBy(p=>p.Key).ToArray());
        Assert.Equal(classes,session.Snapshot.ObjectClasses.OrderBy(p=>p.Key).ToArray());
        Assert.Equal(actors,System.Text.Json.JsonSerializer.Serialize(session.Actors));
        Assert.Equal(1,session.CurrentRoomId);
        Assert.Equal(scenes,session.CutscenesBegun);
        var undone=session.UndoInventoryTransfers(applied.Undo!);
        Assert.True(undone.Applied,undone.Refusal);
        Assert.Equal(owners,session.Snapshot.ObjectOwners.OrderBy(p=>p.Key).ToArray());
        Assert.Equal(before.Items.ToArray(),session.CaptureInventoryStatus().Items.ToArray());
        Assert.False(session.UndoInventoryTransfers(applied.Undo!).Applied);
    }

    [Fact]
    public void CancelAndInvalidTargetsDoNotMutateInventoryAndStaleSnapshotsAreRefused()
    {
        if(!Directory.Exists(Folder)) return;
        var session=ManiacClassicStateRoute.AcquireKey(Folder);
        var before=session.CaptureInventoryStatus();
        // Cancel discards this caller-owned draft: capturing makes no edit.
        _ = new ScummInventoryTransfer(55,1);
        Assert.Equal(before.Items.ToArray(),session.CaptureInventoryStatus().Items.ToArray());
        Assert.False(session.ApplyInventoryTransfers(before,[new(54,1)]).Applied); // Room mat is not held.
        Assert.False(session.ApplyInventoryTransfers(before,[new(55,9)]).Applied); // Named NPC is not party.
        Assert.False(session.ApplyInventoryTransfers(before,[new(55,2)]).Applied); // Unselected kid.
        Assert.False(session.ApplyInventoryTransfers(before,[new(55,1),new(54,1)]).Applied); // Atomic rejection.
        Assert.False(session.ApplyInventoryTransfers(before,[new(55,1),new(55,5)]).Applied); // Duplicate item.
        Assert.Equal(before.Items.ToArray(),session.CaptureInventoryStatus().Items.ToArray());
        var applied=session.ApplyInventoryTransfers(before,[new(55,1)]);
        Assert.True(applied.Applied,applied.Refusal);
        Assert.False(session.ApplyInventoryTransfers(before,[new(55,5)]).Applied);
        Assert.Equal(1,session.Snapshot.ObjectOwners[55]);
        Assert.True(session.UndoInventoryTransfers(applied.Undo!).Applied);
    }

    [Fact]
    public void StartupAndQueuedPlayInputCannotBeEdited()
    {
        if(!Directory.Exists(Folder)) return;
        var menu=ScummGameSession.BootForPlay(new GameFolderScanner().Scan(Folder),out _);
        Assert.False(menu.CaptureInventoryStatus().CanEdit);
        var session=ManiacClassicStateRoute.AcquireKey(Folder);
        var before=session.CaptureInventoryStatus();
        Assert.NotNull(session.PressPlayKey('d',out _));
        Assert.True(session.HasPendingPlayInput);
        Assert.False(session.ApplyInventoryTransfers(before,[new(55,1)]).Applied);
        Assert.Equal(3,session.Snapshot.ObjectOwners[55]);
    }
}
