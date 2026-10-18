namespace SCUMMexplorer.Tests;

public sealed class ScummZakEnhancedStateTests
{
    private static ScummGameSession? Acquired()
    {
        var path=Path.Combine(TestGames.Root,"zakE ega en");
        if(!Directory.Exists(path)) return null;
        var session=ScummGameSession.BootForPlay(new GameFolderScanner().Scan(path),out _);
        void Run(int frames) { for(int i=0;i<frames;i++) session.AdvanceFrame(); }
        void Key(char key) { Assert.NotNull(session.PressPlayKey(key,out _)); Run(2); }
        void Click(int x,int y) { Assert.NotNull(session.ClickScreen(x,y,out _)); Run(2); }
        Run(150); Key('w'); Click(50,81); Click(10,145); Run(180);
        Key('d'); Click(55,78); Click(10,145); Run(180);
        Assert.Equal(1,session.Snapshot.ObjectOwners[15]);
        for(int frame=0;frame<3000 && !session.CaptureInventoryStatus().CanEdit;frame++) session.AdvanceFrame();
        Assert.True(session.CaptureInventoryStatus().CanEdit,session.CaptureInventoryStatus().Refusal);
        return session;
    }

    [Fact]
    public void GenuineBillTransferAndUndoPreserveUnrelatedState()
    {
        if(Acquired() is not {} session) return;
        var before=session.Snapshot;
        var actors=System.Text.Json.JsonSerializer.Serialize(session.Actors);
        var pending=session.PendingScriptSummary.ToArray();
        var status=session.CaptureInventoryStatus();
        Assert.True(status.CanEdit,status.Refusal);
        Assert.Equal(new[]{1,2,3,4},status.Characters.Select(c=>c.ActorId));
        Assert.Equal(new[]{"Zak","Annie","Melissa","Leslie"},status.Characters.Select(c=>c.Name));
        var item=Assert.Single(status.Items,i=>i.ObjectId==15);
        Assert.Equal(1,item.OwnerId);
        var edit=session.ApplyInventoryTransfers(status,[new(15,2)]);
        Assert.True(edit.Applied,edit.Refusal);
        Assert.Equal(2,session.Snapshot.ObjectOwners[15]);
        var moved=Assert.Single(session.CaptureInventoryStatus().Items,i=>i.ObjectId==15);
        Assert.Equal(item.Slot,moved.Slot); Assert.Equal(item.RetainedRoomId,moved.RetainedRoomId);
        Assert.Equal(before.ObjectStates.OrderBy(x=>x.Key),session.Snapshot.ObjectStates.OrderBy(x=>x.Key));
        Assert.Equal(before.ObjectPositions.OrderBy(x=>x.Key),session.Snapshot.ObjectPositions.OrderBy(x=>x.Key));
        Assert.Equal(before.ObjectClasses.OrderBy(x=>x.Key),session.Snapshot.ObjectClasses.OrderBy(x=>x.Key));
        Assert.Equal(before.KnownGlobalVariables.OrderBy(x=>x.Key),session.Snapshot.KnownGlobalVariables.OrderBy(x=>x.Key));
        Assert.Equal(actors,System.Text.Json.JsonSerializer.Serialize(session.Actors));
        Assert.Equal(pending,session.PendingScriptSummary);
        Assert.Equal(1,session.CurrentRoomId);
        Assert.NotNull(edit.Undo);
        var undo=session.UndoInventoryTransfers(edit.Undo!);
        Assert.True(undo.Applied,undo.Refusal);
        Assert.Equal(before.ObjectOwners.OrderBy(x=>x.Key),session.Snapshot.ObjectOwners.OrderBy(x=>x.Key));
        Assert.False(session.UndoInventoryTransfers(edit.Undo!).Applied);
        // The original inventory slot/code must still answer the native Read sentence after undo.
        session.PressPlayKey('x',out _); session.AdvanceFrame(); session.AdvanceFrame();
        session.PressPlayKey('o',out _); session.AdvanceFrame(); session.AdvanceFrame();
        session.ClickScreen(10,145,out _);
        for(int frame=0;frame<180 && !session.Messages.OnScreen.Any(m=>m.Text.Contains("1138"));frame++) session.AdvanceFrame();
        Assert.Contains(session.Messages.OnScreen,m=>m.Text.Contains("1138"));
    }

    [Fact]
    public void SnapshotCancelInvalidBatchAndStaleEditDoNotMutateOwners()
    {
        if(Acquired() is not {} session) return;
        var before=session.Snapshot.ObjectOwners.OrderBy(x=>x.Key).ToArray();
        var status=session.CaptureInventoryStatus(); // Cancel discards this snapshot; no Apply.
        Assert.Equal(before,session.Snapshot.ObjectOwners.OrderBy(x=>x.Key));
        Assert.False(session.ApplyInventoryTransfers(status,[new(15,7)]).Applied); // named NPC
        Assert.False(session.ApplyInventoryTransfers(status,[new(92,2)]).Applied); // drawer still in room
        Assert.False(session.ApplyInventoryTransfers(status,[new(15,2),new(92,2)]).Applied); // atomic batch
        Assert.Equal(before,session.Snapshot.ObjectOwners.OrderBy(x=>x.Key));
        var applied=session.ApplyInventoryTransfers(status,[new(15,2)]);
        Assert.True(applied.Applied,applied.Refusal);
        Assert.False(session.ApplyInventoryTransfers(status,[new(15,3)]).Applied);
        Assert.Equal(2,session.Snapshot.ObjectOwners[15]);
        Assert.True(session.UndoInventoryTransfers(applied.Undo!).Applied);
    }

    [Fact]
    public void ReachedCutsceneBlocksInventoryEdits()
    {
        if(Acquired() is not {} session) return;
        void Run(int frames) { for(int i=0;i<frames;i++) session.AdvanceFrame(); }
        session.PressPlayKey('w',out _); Run(2);
        session.ClickScreen(307,81,out _); Run(2);
        session.ClickScreen(10,145,out _); Run(180);
        session.PressPlayKey('e',out _); Run(2);
        session.ClickScreen(307,81,out _); Run(240);
        Assert.True(session.InCutscene);
        var status=session.CaptureInventoryStatus();
        Assert.False(status.CanEdit);
        Assert.False(session.ApplyInventoryTransfers(status,[new(15,2)]).Applied);
        Assert.Equal(1,session.Snapshot.ObjectOwners[15]);
    }
}
