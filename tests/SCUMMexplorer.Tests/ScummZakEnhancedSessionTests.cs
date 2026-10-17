namespace SCUMMexplorer.Tests;

/// <summary>Zak Enhanced's own English fixture; no state is edited to reach the boot endpoint.</summary>
public sealed class ScummZakEnhancedSessionTests
{
    private static string? Folder()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? folder : null;
    }

    [Fact]
    public void EnhancedZakDoesNotClaimAPlayableBootPastUnsupportedVerbSlots()
    {
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = new ScummV2DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data);
        Assert.Null(session.BootReport!.FinalRoom);
        Assert.Empty(session.BootReport.RoomsEntered);
        Assert.False(session.PlayerHasControl);
        Assert.Contains(session.Snapshot.Uncertainties,
            item => item.Contains("unsupported v2 verbOps", StringComparison.Ordinal) && item.Contains("+0x003B", StringComparison.Ordinal));
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        Assert.Null(session.Snapshot.RoomId);
        Assert.False(session.PlayerHasControl);
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
