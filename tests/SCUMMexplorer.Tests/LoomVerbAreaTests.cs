using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The verb area drawn from the session's verb slots (a presentation of verb
/// state), and ScummVM's <c>findVerbAtPos</c> over it.
/// </summary>
public sealed class LoomVerbAreaTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static RoomObjectImage? Image(GameLibrary game, int room, int objectId) =>
        new LoomRoomObjects().Decode(File.ReadAllBytes(game.Rooms.Single(item => item.ResourceId == room).FilePath))
            .FirstOrDefault(item => item.Id == objectId);

    private static LoomScriptDisassembly Script(params LoomScriptInstruction[] instructions) =>
        new(250, 1, 0, 32, 16, instructions, null, null, [], [], "Synthetic test script");

    [Fact]
    public void TheDistaffNotesScript018TurnsOnAreDrawnAndFound()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));

        // After the boot every verb Room 01 local 211 created is off: nothing is drawn.
        var empty = LoomVerbArea.Compose(session.Verbs, (room, obj) => Image(game, room, obj));
        Assert.Empty(empty.Items);
        Assert.All(empty.Pixels, pixel => Assert.Equal(0, pixel));

        // Script 018 [2] (started by script 003 in play): var[0x064] = 2 and verbs 0x01-0x08
        // on at y 0x90 (standard difficulty); their images stay Room 01's objects 100-142.
        // Outside expert mode (+0x0076) it also turns on 0x31 at (48, 168) and 0x32 at (24, 168).
        session.RunTopLevelScript(Script(
            new(0, 0x0A, "startScript", "0x12, [0x0002]", "0A 12 02 00 FF"),
            new(5, 0, "stopObjectCode", "", "00")));
        Assert.Equal((ushort)2, session.Snapshot.KnownGlobalVariables["var[0x064]"]);
        var area = LoomVerbArea.Compose(session.Verbs, (room, obj) => Image(game, room, obj));
        Assert.Equal([.. Enumerable.Range(1, 8), 0x31, 0x32], area.Items.Select(item => item.VerbId));
        var first = area.Items[0];
        Assert.Equal((16, 144, 100, 1), (first.X, first.Y, first.ImageObject, first.ImageRoom));
        Assert.True(first.Width > 0 && first.Height > 0);
        Assert.Contains(area.Pixels, pixel => pixel != 0);

        // findVerbAtPos: a point inside note 0x03's rectangle finds it; the room above does not.
        var third = area.Items[2];
        Assert.Equal(0x03, LoomVerbArea.FindVerbAt(area.Items, third.X + 1, third.Y + 1)?.VerbId);
        Assert.Null(LoomVerbArea.FindVerbAt(area.Items, third.X + 1, 100));
    }

    [Fact]
    public void FindVerbAtTakesTheHighestSlotThatIsOn()
    {
        LoomVerbAreaItem Item(int slot, int verb, int mode) => new(slot, verb, mode, 16, 144, 24, 16, 100, 1, null);
        Assert.Equal(0x09, LoomVerbArea.FindVerbAt([Item(1, 0x01, 1), Item(9, 0x09, 1)], 20, 150)?.VerbId);
        // Dim verbs are drawn but not found.
        Assert.Equal(0x01, LoomVerbArea.FindVerbAt([Item(1, 0x01, 1), Item(9, 0x09, 2)], 20, 150)?.VerbId);
        Assert.Null(LoomVerbArea.FindVerbAt([Item(1, 0x01, 1)], 40, 150));
    }
}
