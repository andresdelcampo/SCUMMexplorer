using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedSessionTests
{
    [Fact]
    public void V2DataSourceReadsTheEnhancedLayoutWithoutV3IndexOrRoomOffsets()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(ScummVersion.V2, game.Version);
        var data = Assert.IsType<ScummV2DataSource>(ScummDataSources.For(game));
        Assert.Equal(179, data.GlobalScriptCount);
        Assert.NotNull(data.GlobalScript(1));
        var index = data.ReadIndexedObjects();
        Assert.Equal(780, index.States.Count);
        foreach (var room in game.Rooms)
        {
            var id = room.ResourceId!.Value;
            var decoded = data.ReadRoom(id);
            Assert.InRange(decoded.Width, 8, 1280);
            Assert.InRange(decoded.Height, 1, 128);
            Assert.Empty(data.RoomLocalScriptIds(id));
            _ = data.RoomEntryScript(id);
            _ = data.RoomExitScript(id);
            var verbs = data.ReadObjectVerbs(id, decoded.Objects);
            foreach (var record in decoded.Objects)
                foreach (var verb in verbs[record.Id])
                    Assert.NotNull(data.ReadObjectVerb(id, record, verb.VerbId, out _));
            _ = data.ReadObjectImages(id);
        }
        Assert.Throws<InvalidDataException>(() => data.ReadRoom(255));
    }
}
