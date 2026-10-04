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
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

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

    [Fact]
    public void ANotesAnimationEndsOnThePlainNoteItDrewLast()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game,
            new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        session.RunTopLevelScript(Script(
            new(0, 0x0A, "startScript", "0x12, [0x0002]", "0A 12 02 00 FF"),
            new(5, 0, "stopObjectCode", "", "00")));
        // Script 030 [3] plays note 3 on the distaff: script 031 draws verbs 0x13-0x17 and back over the
        // note (images 113-117-113), one a frame, and last verb 0x03 with its own image 112.
        session.RunTopLevelScript(Script(
            new(0, 0x0A, "startScript", "0x1E, [0x0003]", "0A 1E 03 00 FF"),
            new(5, 0, "stopObjectCode", "", "00")));
        for (var frame = 0; frame < 40; frame++) session.AdvanceFrame();
        var onAtNote = session.Verbs.Where(verb => verb is { Mode: 1, X: 72, Y: 144 }).ToArray();
        Assert.Equal([0x03, 0x13, 0x14, 0x15, 0x16, 0x17], onAtNote.Select(verb => verb.VerbId));
        Assert.Equal(0x03, onAtNote.MaxBy(verb => verb.DrawOrder)!.VerbId);

        // The verb screen shows what was drawn last (image 112), not the highest slot (verb 0x17, image 117).
        var area = session.VerbArea();
        var plain = Image(game, 1, 112)!;
        var glow = Image(game, 1, 117)!;
        byte At(byte[] pixels, int x, int y) => pixels[y * LoomVerbArea.Width + x];
        var matchesPlain = Enumerable.Range(0, plain.Height / 8 * 8).All(row =>
            Enumerable.Range(0, plain.Width / 8 * 8).All(column => At(area.Pixels, 72 + column, row) == plain.Pixels[row * plain.Width + column]));
        Assert.True(matchesPlain);
        Assert.NotEqual(plain.Pixels, glow.Pixels);
        var slotOrder = LoomVerbArea.Compose(session.Verbs.Select(verb => verb with { DrawOrder = 0 }), (room, obj) => Image(game, room, obj));
        Assert.NotEqual(area.Pixels, slotOrder.Pixels);
    }

    [Fact]
    public void AVerbTurnedOffClearsWhereItWasDrawn()
    {
        var table = new LoomVerbTable { ImageSize = (_, _) => (32, 24) };
        (string, int?[], string?) Op(string name, params int?[] values) => (name, values, null);
        table.Apply(0x01, [Op("new"), Op("at", 16, 144), Op("image", 100), Op("on")], _ => true, 1, null);
        Assert.Equal(new LoomScreenBox(16, 144, 47, 167, 0), table.LastCover);
        table.Apply(0x35, [Op("new"), Op("at", 96, 184), Op("image", 151), Op("on")], _ => true, 1, null);
        table.Apply(0x35, [Op("off")], _ => true, 1, null);
        // restoreVerbBG clears verb 0x35's rectangle; verb 0x01, drawn before, is under it only where they overlap.
        Assert.Equal([new LoomVerbErase(3, new LoomScreenBox(96, 184, 127, 207, 0))], table.Erases());
        Assert.Equal(new LoomScreenBox(96, 184, 127, 207, 0), table.LastCover);
        var area = LoomVerbArea.Compose(table.Snapshot(), (_, _) => new RoomObjectImage(0, 0, 0, 32, 24, Enumerable.Repeat((byte)5, 32 * 24).ToArray()), table.Erases());
        Assert.Equal(5, area.Pixels[0 * LoomVerbArea.Width + 16]);
        Assert.Equal(0, area.Pixels[40 * LoomVerbArea.Width + 96]);
    }
}
