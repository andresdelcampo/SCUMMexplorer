using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummV1DataSourceTests
{
    [Theory]
    [InlineData("mmansion ega en", 200, 800, ScummV1Game.ManiacMansion)]
    [InlineData("zak ega en", 155, 775, ScummV1Game.ZakMcKracken)]
    public void ClassicAdapterUsesFixedIndexAndTiledRooms(string folderName, int scriptCount, int objectCount, ScummV1Game classicGame)
    {
        var folder = Path.Combine(TestGames.Root, folderName);
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = Assert.IsType<ScummV1DataSource>(ScummDataSources.For(game));
        Assert.Equal(scriptCount, data.GlobalScriptCount);
        Assert.NotNull(data.GlobalScript(1));
        Assert.Empty(data.GlobalScript(1)!.Issues ?? []);
        var indexed = data.ReadIndexedObjects();
        var rawIndex = LflEncoding.DecodeXorFF(File.ReadAllBytes(Path.Combine(folder, "00.LFL")));
        Assert.Equal(objectCount, indexed.States.Count);
        for (var id = 0; id < objectCount; id++)
        {
            Assert.Equal(rawIndex[id + 2] >> 4, indexed.States[id]);
            Assert.Equal(rawIndex[id + 2] & 15, indexed.Owners[id]);
        }
        foreach (var resource in game.Rooms)
        {
            var id = resource.ResourceId!.Value;
            var room = data.ReadRoom(id);
            var image = new ScummV1RoomDecoder(classicGame).Decode(File.ReadAllBytes(resource.FilePath));
            Assert.Equal(image.Width, room.Width);
            Assert.Equal(image.Height, room.Height);
            Assert.Empty(data.RoomLocalScriptIds(id));
            Assert.Null(data.RoomLocalScript(id, scriptCount));
            _ = data.RoomEntryScript(id);
            _ = data.RoomExitScript(id);
            var verbs = data.ReadObjectVerbs(id, room.Objects);
            foreach (var obj in room.Objects)
                foreach (var verb in verbs[obj.Id])
                    Assert.NotEmpty(data.ReadObjectVerb(id, obj, verb.VerbId, out _).Instructions);
            foreach (var objImage in data.ReadObjectImages(id))
            {
                Assert.Equal(objImage.Width * objImage.Height, objImage.Pixels.Length);
                Assert.All(objImage.Pixels, pixel => Assert.InRange(pixel, (byte)0, (byte)15));
            }
        }
        Assert.Throws<InvalidDataException>(() => data.ReadRoom(255));
        Assert.Throws<ArgumentException>(() => new ScummV2DataSource(game));
    }
}
