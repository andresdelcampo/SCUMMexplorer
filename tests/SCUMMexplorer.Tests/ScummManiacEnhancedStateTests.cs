using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedStateTests
{
    [Fact]
    public void GenuineHeldKeyTransfersAtomicallyAndUndoPreservesAllOtherState()
    {
        var session = AcquireKey();
        if (session is null) return;
        var before = session.Snapshot;
        var actors = System.Text.Json.JsonSerializer.Serialize(session.Actors);
        var trace = session.SessionTrace.ToArray();
        var scenes = session.CutscenesBegun;
        var view = session.CaptureInventoryStatus();
        var key = Assert.Single(view.Items, x => x.ObjectId == 55);
        Assert.Equal(3, key.OwnerId);
        Assert.Equal(1, key.RetainedRoomId);
        Assert.False(string.IsNullOrWhiteSpace(key.Name));
        // One valid and one invalid transfer must reject the entire batch.
        Assert.False(session.ApplyInventoryTransfers(view, [new(55, 1), new(54, 5)]).Applied);
        Assert.False(session.ApplyInventoryTransfers(view, [new(55, 9)]).Applied); // NPC
        Assert.False(session.ApplyInventoryTransfers(view, [new(55, 1), new(55, 5)]).Applied);
        Assert.Equal(3, session.Snapshot.ObjectOwners[55]);
        var applied = session.ApplyInventoryTransfers(view, [new(55, 1)]);
        Assert.True(applied.Applied, applied.Refusal);
        Assert.NotNull(applied.Undo);
        Assert.Equal(key with { OwnerId = 1 }, Assert.Single(session.CaptureInventoryStatus().Items, x => x.ObjectId == 55));
        Assert.False(session.ApplyInventoryTransfers(view, [new(55, 5)]).Applied);
        // A refused empty batch must leave the successful undo available.
        Assert.False(session.ApplyInventoryTransfers(session.CaptureInventoryStatus(), []).Applied);
        Assert.True(session.UndoInventoryTransfers(applied.Undo).Applied);
        Assert.False(session.UndoInventoryTransfers(applied.Undo).Applied);
        Assert.Equal(view.Items, session.CaptureInventoryStatus().Items);
        Assert.Equal(before.ObjectOwners.OrderBy(x => x.Key), session.Snapshot.ObjectOwners.OrderBy(x => x.Key));
        Assert.Equal(before.ObjectStates.OrderBy(x => x.Key), session.Snapshot.ObjectStates.OrderBy(x => x.Key));
        Assert.Equal(before.ObjectClasses.OrderBy(x => x.Key), session.Snapshot.ObjectClasses.OrderBy(x => x.Key));
        Assert.Equal(before.KnownGlobalVariables.OrderBy(x => x.Key), session.Snapshot.KnownGlobalVariables.OrderBy(x => x.Key));
        Assert.Equal(actors, System.Text.Json.JsonSerializer.Serialize(session.Actors));
        Assert.Equal(trace, session.SessionTrace.Take(trace.Length));
        Assert.Equal(2, session.SessionTrace.Count - trace.Length);
        Assert.All(session.SessionTrace.Skip(trace.Length), line => Assert.StartsWith("Explicit inventory ", line));
        Assert.Equal(scenes, session.CutscenesBegun);
        Assert.Equal(before.RoomId, session.CurrentRoomId);
        // Cancel is just discarding a captured view; reading runs nothing.
        _ = session.CaptureInventoryStatus();
        Assert.Equal(before.KnownGlobalVariables.OrderBy(x => x.Key), session.Snapshot.KnownGlobalVariables.OrderBy(x => x.Key));
    }

    [Fact]
    public void StaleFramesCrossSessionAndQueuedInputCannotEditOrUndo()
    {
        var session = AcquireKey();
        if (session is null) return;
        var captured = session.CaptureInventoryStatus();
        session.AdvanceFrame();
        Assert.False(session.ApplyInventoryTransfers(captured, [new(55, 1)]).Applied);
        var applied = session.ApplyInventoryTransfers(session.CaptureInventoryStatus(), [new(55, 1)]);
        Assert.True(applied.Applied, applied.Refusal);
        var other = ScummGameSession.BootForPlay(new GameFolderScanner().Scan(Path.Combine(TestGames.Root, "mmansionE ega es")), out _);
        Assert.False(other.ApplyInventoryTransfers(session.CaptureInventoryStatus(), [new(55, 5)]).Applied);
        Assert.False(other.UndoInventoryTransfers(applied.Undo!).Applied);
        session.AdvanceFrame();
        Assert.False(session.UndoInventoryTransfers(applied.Undo!).Applied);
        var pendingView = session.CaptureInventoryStatus();
        Assert.NotNull(session.PressPlayKey('w', out _));
        Assert.False(session.CaptureInventoryStatus().CanEdit);
        Assert.False(session.ApplyInventoryTransfers(pendingView, [new(55, 5)]).Applied);
        Assert.Equal(1, session.Snapshot.ObjectOwners[55]);
    }

    private static ScummGameSession? AcquireKey()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return null;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        void Frames(int n) { for (var i = 0; i < n; i++) session.AdvanceFrame(); }
        void Until(Func<bool> done)
        { for (var i = 0; i < 2500 && !done(); i++) Frames(1); Assert.True(done()); }
        void Click(int x, int y)
        { Assert.NotNull(session.ClickScreen(x, y + session.MainScreen.Top, out _)); Frames(1); }
        void Key(int key) { Assert.NotNull(session.PressPlayKey(key, out _)); Frames(1); }
        foreach (var (x,y) in new[] {(76,104),(196,104),(292,60)}) { Click(x,y); Frames(100); }
        Until(() => session.CurrentRoomId == 44 && session.CutsceneSkippable);
        Assert.False(session.CaptureInventoryStatus().CanEdit);
        session.PressKey(27);
        Until(() => session.PlayerHasControl);
        Click(8,120); Until(() => session.CurrentRoomId == 1); Frames(120);
        for (var i = 0; i < 2; i++) { Click(8,120); Frames(200); }
        Key('d'); Click(376-session.ViewLeft,84); Click(376-session.ViewLeft,84);
        Until(() => (session.Snapshot.ObjectStates[54] & 8) != 0);
        Key('d'); Click(416-session.ViewLeft,84); Click(416-session.ViewLeft,84);
        Until(() => session.Snapshot.ObjectOwners[55] == 3);
        Until(() => session.CaptureInventoryStatus().CanEdit);
        return session;
    }

    [Fact]
    public void SelectedPartyNamesComeFromReachedNamesAndOwnInputBindings()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.False(session.CaptureInventoryStatus().CanEdit);
        ManiacCutsceneSetup.PrepareIfNeeded(game, session);
        for (var i = 0; i < 2000 && !session.CaptureInventoryStatus().CanEdit; i++) session.AdvanceFrame();
        var status = session.CaptureInventoryStatus();
        Assert.True(status.CanEdit, status.Refusal);
        Assert.Equal(Enumerable.Range(1,7), status.Characters.Select(x => x.ActorId));
        Assert.Equal(new[] { "Syd", "Razor", "Dave", "Michael", "Bernard", "Wendy", "Jeff" }, status.Characters.Select(x => x.Name));
        Assert.All(status.Characters, x => Assert.Contains("Script 4", x.Evidence));
    }
}
