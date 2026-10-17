using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class ScummManiacEnhancedSessionTests
{
    [Fact]
    public void SourceSelectionCompletesOpeningAndReturnsControlOutsideMansion()
    {
        var folder = Path.Combine(TestGames.Root, "mmansionE ega es");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = (ScummV2DataSource)ScummDataSources.For(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data);
        Assert.Equal(45, session.CurrentRoomId);
        foreach (var point in new[] { (76, 104), (196, 104), (292, 60) })
        {
            session.Click(point.Item1, point.Item2);
            for (var frame = 0; frame < 100; frame++) session.AdvanceFrame();
        }
        for (var frame = 0; frame < 5000 && !session.PlayerHasControl; frame++) session.AdvanceFrame();
        Assert.Equal(44, session.CurrentRoomId);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.Equal(32, session.Snapshot.KnownGlobalVariables[ScummVars.V2.CharCount]);
        Assert.Equal(1, session.Snapshot.KnownGlobalVariables["var[0x030]"]);
        Assert.Equal(5, session.Snapshot.KnownGlobalVariables["var[0x031]"]);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, s => s.Contains("unsupported v2"));
        Assert.Contains(session.Verbs, v => v.Mode == 1 && v.Name is not null);
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        Assert.Equal(44, session.CurrentRoomId);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, s => s.Contains("unsupported v2"));
    }
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
