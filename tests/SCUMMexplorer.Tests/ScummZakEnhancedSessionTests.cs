namespace SCUMMexplorer.Tests;

/// <summary>Zak Enhanced's own English fixture; no state is edited to reach the boot endpoint.</summary>
public sealed class ScummZakEnhancedSessionTests
{
    private static string? Folder()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? folder : null;
    }

    [Theory]
    [InlineData("zakE ega en", GameFamily.ZakEnhanced)]
    [InlineData("zak ega en", GameFamily.ZakClassic)]
    public void ZaksOwnOpeningReachesItsStableFirstPlayableRoom(string edition, GameFamily family)
    {
        var folder = Path.Combine(TestGames.Root, edition);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(family, GameSupport.For(game).Family);
        var data = Assert.IsAssignableFrom<ScummV2DataSource>(ScummDataSources.For(game));
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data);
        Assert.Equal(1, session.BootReport!.FinalRoom);
        Assert.False(session.BootReport.HitFrameLimit);
        Assert.Equal(new[] { 46, 0, 46, 0, 58, 51, 55, 49, 56, 0, 0, 0, 1 }, session.BootReport.RoomsEntered);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.Equal((ushort)32767, session.Snapshot.KnownGlobalVariables["var[0x006]"]);
        Assert.Contains(session.Actors, actor => actor.Number == 1 && actor.Room == 1 && actor.Look.Costume == 1);
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        Assert.Equal(1, session.Snapshot.RoomId);
        Assert.True(session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.DoesNotContain(session.Snapshot.Uncertainties, item =>
            item.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
            item.Contains("not model", StringComparison.OrdinalIgnoreCase) ||
            item.Contains("cannot", StringComparison.OrdinalIgnoreCase) ||
            item.Contains("could not", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(session.Snapshot.Uncertainties, item => item.Contains("seeded pick", StringComparison.Ordinal));
    }

    [Fact]
    public void EnhancedZakUsesItsV2ScriptsAndRoomData()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameFamily.ZakEnhanced, GameSupport.For(game).Family);
        Assert.Equal(ScummScriptDialect.V2, ScummProfile.For(game).Dialect);
        var data = Assert.IsType<ScummV2DataSource>(ScummDataSources.For(game));
        Assert.Equal(58, game.Rooms.Count());
        Assert.Equal(155, data.GlobalScriptCount);
        Assert.Equal(148, data.GlobalScriptIds().Count);
        Assert.Equal(780, data.ReadIndexedObjects().States.Count);
        Assert.NotNull(data.GlobalScript(1));
        Assert.NotNull(data.GlobalScript(108));
        Assert.NotNull(data.RoomEntryScript(1));
        Assert.NotEmpty(data.ReadRoom(1).Objects);
        Assert.NotEmpty(data.ReadObjectImages(1));
        Assert.Empty(data.RoomLocalScriptIds(1));
        foreach (var room in game.Rooms)
        {
            var id = room.ResourceId!.Value;
            var records = data.ReadRoom(id).Objects;
            _ = data.RoomEntryScript(id);
            _ = data.RoomExitScript(id);
            var verbs = data.ReadObjectVerbs(id, records);
            foreach (var record in records)
            foreach (var verb in verbs[record.Id])
                Assert.NotNull(data.ReadObjectVerb(id, record, verb.VerbId, out _));
            _ = data.ReadObjectImages(id);
        }
    }
}
