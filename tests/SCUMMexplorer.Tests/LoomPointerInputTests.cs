using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>
/// verbOps as state, startObject, and the pointer given to the session as
/// assumed hover input: script 006 finds the hovered object and script 008 shows
/// its close-up (verb 0x36) from the object's verb 0x39.
/// </summary>
public sealed class LoomPointerInputTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    private static (GameLibrary Game, LoomExplorationSession Session)? Start()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(Folder);
        return (game, new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"))));
    }

    private static ushort? Var(LoomExplorationSession session, int number) =>
        session.Snapshot.KnownGlobalVariables.TryGetValue($"var[0x{number:X3}]", out var value) ? value : null;

    [Fact]
    public void TheBootBuildsTheVerbBarThroughRoom01Local211()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        var verbs = session.Verbs;
        // Room 01 local 211 +0x000C: verbOps 0x01, [new; at, 0x0010, 0x0090; image, 0x0064; off], and so on to 0x36.
        var first = Assert.Single(verbs, verb => verb.VerbId == 0x01);
        Assert.Equal((1, 16, 144, 100, 1, 0), (first.Slot, first.X!.Value, first.Y!.Value, first.ImageObject!.Value, first.ImageRoom!.Value, first.Mode!.Value));
        var closeUp = Assert.Single(verbs, verb => verb.VerbId == 0x36);
        Assert.Equal(0, closeUp.Mode);
        Assert.Null(closeUp.ImageObject);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("'verbOps' is not modeled", StringComparison.Ordinal));
    }

    [Fact]
    public void HoveringAnObjectShowsItsCloseUpAndLeavingTurnsItOff()
    {
        if (Start() is not { } started) return;
        var session = started.Session;
        // Room 02 object 156 (class 1) covers (700, 40); its verb 0x39 sets var[0x068] = 159.
        session.SetPointer((700, 40));
        session.AdvanceFrame();
        Assert.Equal((ushort?)156, Var(session, 0x066));
        Assert.Equal((ushort?)159, Var(session, 0x068));
        var closeUp = Assert.Single(session.Verbs, verb => verb.VerbId == 0x36);
        Assert.Equal((1, 159, 2, 248, 152), (closeUp.Mode!.Value, closeUp.ImageObject!.Value, closeUp.ImageRoom!.Value, closeUp.X!.Value, closeUp.Y!.Value));
        Assert.Contains(session.Snapshot.Uncertainties, line => line.Contains("placed by the explorer's hover, with no click", StringComparison.Ordinal));

        // No pointer: script 006 starts script 008 with 0, which turns verb 0x36 off.
        session.SetPointer(null);
        session.AdvanceFrame();
        Assert.Equal((ushort?)0, Var(session, 0x066));
        Assert.Equal(0, session.Verbs.Single(verb => verb.VerbId == 0x36).Mode);
        Assert.Equal(2, session.Snapshot.RoomId);
    }

    [Fact]
    public void AResumedGlobalScriptSeesTheCurrentRoomsObjects()
    {
        if (Start() is not { } started) return;
        var (game, session) = started;
        // Script 006 was pended in Room 02; in Room 03 its findObject must see Room 03's
        // object 165 (the sky, box (80, 8) 96x16), whose verb 0x39 sets var[0x068] = 166.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        session.SetPointer((128, 16));
        session.AdvanceFrame();
        Assert.Equal((ushort?)165, Var(session, 0x066));
        Assert.Equal(166, session.Verbs.Single(verb => verb.VerbId == 0x36).ImageObject);
        Assert.Contains(session.SessionTrace, line => line.Contains("startObject runs object 165 verb 0x39", StringComparison.Ordinal));
    }

    [Fact]
    public void APointerOutsideTheRoomCountsAsNoPointerAndARoomChangeTakesItOff()
    {
        if (Start() is not { } started) return;
        var (game, session) = started;
        session.SetPointer((2000, 40));
        Assert.Null(session.Pointer);
        session.SetPointer((700, 40));
        Assert.Equal((700, 40), session.Pointer);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        Assert.Null(session.Pointer);
    }

    [Fact]
    public void VerbOpsOnAVerbWithoutASlotLeavesTheVerbsAlone()
    {
        var table = new LoomVerbTable();
        // getVerbSlot returns 0 for an unknown verb: image is ignored and nothing is listed.
        var trace = table.Apply(0x40, [("image", [7], null), ("on", [], null)], _ => true, 2, null);
        Assert.Contains("scratch slot 0", trace);
        Assert.Empty(table.Snapshot());
        table.Apply(0x40, [("new", [], null), ("at", [10, 20], null), ("image", [7], null), ("on", [], null)], id => id == 7, 2, null);
        var verb = Assert.Single(table.Snapshot());
        Assert.Equal((1, 7, 2, 1), (verb.Slot, verb.ImageObject!.Value, verb.ImageRoom!.Value, verb.Mode!.Value));
        // setVerbObject keeps the old image when the object is not in the current room.
        table.Apply(0x40, [("image", [8], null)], id => id == 7, 3, null);
        Assert.Equal(7, table.Find(0x40)!.ImageObject);
        table.Apply(0x40, [("delete", [], null)], _ => true, 3, null);
        Assert.Empty(table.Snapshot());
    }
}
