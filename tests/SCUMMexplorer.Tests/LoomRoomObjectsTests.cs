using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

public sealed class LoomRoomObjectsTests
{
    [Fact]
    public void StartedChildLimitDoesNotDiscardRoom28ParentObjectState()
    {
        if (!File.Exists(Path.Combine(GameFolder(), "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(GameFolder());
        var room = game.Rooms.Single(r => r.ResourceId == 28);
        var index = new LflObjectStateReader().ReadSnapshot(Path.Combine(GameFolder(), "00.LFL"));
        var session = new LoomExplorationSession(game, index);
        var snapshot = session.VisitRoom(room);

        foreach (var id in Enumerable.Range(476, 7))
            Assert.Equal((byte)1, snapshot.ObjectStates[id]);
        foreach (var id in Enumerable.Range(497, 3))
            Assert.Equal((byte)0, snapshot.ObjectStates[id]);
        // The entry's child, script 016, runs to its end: its empty print sets text slot 0
        // (line height 10, centred at x 160, y from its argument) for the lines that follow.
        Assert.Contains(session.Messages.Log, message => message.Source == "Script 16 +0x0005" &&
            message.X == 160 && message.LineHeight == 10 && message.Text == "");
        Assert.True(session.LastEntryRun!.InstructionsVisited > 20);
    }
    [Fact]
    public void StoredObjectNamesLabelRoom17Hotspots()
    {
        var path = Path.Combine(GameFolder(), "17.LFL");
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);
        var records = new LoomRoomObjectDirectory().Decode(bytes);

        var names = LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(bytes), records);

        // Two graves share one stored name; the side panel tells them apart by index.
        Assert.Equal(2, names.Values.Count(name => name == "tumba"));
        Assert.All(names.Keys, id => Assert.Contains(records, record => record.Id == id));
    }

    [Theory]
    [InlineData(28)]
    [InlineData(33)]
    public void SuppliedIndexStatesAreZeroBeforeEntryScripts(int roomNumber)
    {
        var folder = GameFolder();
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var images = new LoomRoomObjects().Decode(File.ReadAllBytes(Path.Combine(folder, $"{roomNumber:00}.LFL")));
        var states = new LflObjectStateReader().Read(Path.Combine(folder, "00.LFL"));
        var selection = new RoomObjectSelection(images, states);

        Assert.NotEmpty(images);
        Assert.All(images, image => Assert.False(selection.IsActive(image)));
        Assert.All(images, image => Assert.Equal((byte)0, states[image.Id]));
    }

    [Fact]
    public void Room28TallerImageIsIndependentWithoutScriptEvidence()
    {
        var path = Path.Combine(GameFolder(), "28.LFL");
        if (!File.Exists(path)) return;
        var images = new LoomRoomObjects().Decode(File.ReadAllBytes(path));
        var first = Assert.Single(images, image => image.Id == 479);
        var second = Assert.Single(images, image => image.Id == 486);
        var taller = Assert.Single(images, image => image.Id == 493);
        var selection = new RoomObjectSelection(images);

        Assert.True(selection.Select(first));
        Assert.Equal(second, selection.Next(first));
        Assert.True(selection.Select(taller));
        Assert.True(selection.IsActive(second));
        Assert.True(selection.IsActive(taller));
    }

    [Fact]
    public void RoomDirectoryRetainsCodeOnlyObjectsAndAssignsSharedImageToItsOwner()
    {
        var path = Path.Combine(GameFolder(), "06.LFL");
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);
        var records = new LoomRoomObjectDirectory().Decode(bytes);
        var images = new LoomRoomObjects().Decode(bytes);

        Assert.Contains(records, record => record.Id == 211 && record.ImageOffset is null);
        Assert.Contains(records, record => record.Id == 212 && record.ImageOffset is not null);
        Assert.Contains(records, record => record.Id == 214 && record.ImageOffset is null);
        Assert.Contains(images, image => image.Id == 212);
        Assert.DoesNotContain(images, image => image.Id is 211 or 214);
        Assert.All(images, image =>
        {
            var record = Assert.Single(records, item => item.Id == image.Id);
            Assert.Equal((record.X, record.Y, record.Width, record.Height),
                (image.X, image.Y, image.Width, image.Height));
            Assert.NotNull(record.ImageOffset);
        });
    }

    [Fact]
    public void FindsImagesWithinSuppliedRoomsWhenAvailable()
    {
        var folder = GameFolder();
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;

        var decoder = new LoomRoomObjects();
        var background = new LoomEgaRoomDecoder();
        var indexStates = new LflObjectStateReader().Read(Path.Combine(folder, "00.LFL"));
        var total = 0;
        var room06Ids = Array.Empty<int>();
        foreach (var resource in new GameFolderScanner().Scan(folder).Rooms)
        {
            var bytes = File.ReadAllBytes(resource.FilePath);
            var room = background.Decode(bytes);
            var objects = decoder.Decode(bytes);
            _ = new RoomObjectSelection(objects, indexStates);
            if (resource.ResourceId == 6) room06Ids = objects.Select(obj => obj.Id).ToArray();
            foreach (var obj in objects)
            {
                Assert.InRange(obj.X, 0, room.Width - obj.Width);
                Assert.InRange(obj.Y, 0, room.Height - obj.Height);
                Assert.Equal(obj.Width * obj.Height, obj.Pixels.Length);
                Assert.All(obj.Pixels, pixel => Assert.InRange(pixel, (byte)0, (byte)15));
                total++;
            }
        }
        Assert.True(total > 0, "No object images were decoded.");
        Assert.Contains(212, room06Ids);
        Assert.DoesNotContain(211, room06Ids);
        Assert.DoesNotContain(214, room06Ids);
    }

    private static string GameFolder() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
}
