namespace SCUMMexplorer.Tests;

/// <summary>Own-copy native source previews, explicitly separate from earned story routes and universal costume colours.</summary>
public sealed class ScummZakClassicTransformationPaletteTests
{
    private static GameLibrary? Open()
    {
        var folder = Path.Combine(TestGames.Root, "zak ega en");
        return Directory.Exists(folder) ? new GameFolderScanner().Scan(folder) : null;
    }

    [Theory]
    [InlineData(20, 9, 0x0A)]
    [InlineData(22, 5, 0x10)]
    [InlineData(27, 5, 0x1F)]
    [InlineData(32, 9, 0x66)]
    [InlineData(39, 9, 0x18)]
    public void NativeZakRoomTransformationRetainsTheReachedActorPalette(int room, int costume, int offset)
    {
        if (Open() is not { } game) return;
        var observations = new List<ScummScriptObservation>();
        var observer = new ScummScriptObserver(observations.Add,
            site => site.Operation == "actorOps", captureOperandBindings: true);
        var data = new ScummV1DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
        var before = session.Actors.Single(a => a.Number == 1).Look.Palette.ToArray();
        Assert.Equal(13, before[1]); // Script 108 +00EC reached by the normal opening.
        observations.Clear();
        // Disclosed direct room selection. We do not claim to have played the room's story prerequisites.
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        var look = session.Actors.Single(a => a.Number == 1).Look;
        Assert.Equal(costume, look.Costume);
        Assert.Equal(before, look.Palette);
        var reached = Assert.Single(observations, o => o.Site is { Stage: "instruction" } site &&
            site.Context == $"Room {room:00} / Entry script" && site.Offset == offset);
        Assert.Equal(1, reached.Values["var[0x000]"]);
        if (room is 27 or 32 or 39)
        {
            var index = reached.Values["var[0x00E]"];
            Assert.Equal(room == 27 ? 0xB0 : 0xB4, index);
            Assert.Equal(costume, reached.Values[$"var[0x{index:X3}]"]);
        }
        Assert.DoesNotContain(observations, o => o.Site is { Stage: "instruction", Operands: { } text } &&
            text.StartsWith("0x01,", StringComparison.Ordinal) && text.EndsWith("sub=0x02", StringComparison.Ordinal));
        Assert.Equal(ScummV1ColorBasis.Estimate, ScummV1ActorColors.Read(game).ColorOf(costume).Basis);
    }

    [Fact]
    public void NativeTransformationTableContainsDistinctActorBoundUnderwaterForms()
    {
        if (Open() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal(new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 7, 9, 9, 8, 8 },
            Enumerable.Range(0xAB, 13).Select(id => session.Snapshot.KnownGlobalVariables[$"var[0x{id:X3}]"]));
        // Room 27 reads +4; Room 32 reads +8. Costume 8 is a real source-table result, absent from literal costume assignments.
        var colors = ScummV1ActorColors.Read(game);
        Assert.DoesNotContain(colors.Settings, s => s.IsCostume && s.Value == 8);
        Assert.Equal(ScummV1ColorBasis.Estimate, colors.ColorOf(8).Basis);
        Assert.Equal(3, session.Actors.Single(a => a.Number == 3).Look.Palette[3]);
        // This saved colour is 7; Script 5 later conditionally writes 8 to Leslie. Do not confuse the saved source colour with a reached universal palette.
        Assert.Equal(7, session.Snapshot.KnownGlobalVariables["var[0x0C4]"]);
    }

    [Fact]
    public void NativeBirdRoomAndExplicitPerchedExplorationRetainTheNativeBirdPalette()
    {
        if (Open() is not { } game) return;
        var observations = new List<ScummScriptObservation>();
        var data = new ScummV1DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data,
            observer: new ScummScriptObserver(observations.Add, site => site.Operation == "actorOps"));
        var before = session.Actors.Single(a => a.Number == 6).Look.Palette.ToArray();
        Assert.Equal(6, before[6]); // Script 108 +0114, independent of the bird's later costumes.
        observations.Clear();
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 42));
        for (var frame = 0; frame < 500; frame++) session.AdvanceFrame();
        var flying = session.Actors.Single(a => a.Number == 6);
        Assert.Equal(15, flying.Look.Costume);
        Assert.Equal(before, flying.Look.Palette);
        Assert.Equal(42, flying.Room);
        Assert.Contains(observations, o => o.Site is { Stage: "instruction", Context: "Room 42 / Object 628 / Verb 0xFD", Offset: 0x17 });
        // An explicitly disclosed verb exploration, not proof that the feather/mind-transfer prerequisites were earned.
        session.ActivateObjectVerb(629, 253);
        var perched = session.Actors.Single(a => a.Number == 6);
        Assert.Equal(14, perched.Look.Costume);
        Assert.Equal(before, perched.Look.Palette);
        Assert.Contains(observations, o => o.Site is { Stage: "instruction", Context: "Room 42 / Object 629 / Verb 0xFD", Offset: 0x13 });
        Assert.Equal(ScummV1ColorBasis.Estimate, ScummV1ActorColors.Read(game).ColorOf(14).Basis);
        Assert.Equal(ScummV1ColorBasis.Estimate, ScummV1ActorColors.Read(game).ColorOf(15).Basis);
    }

    [Fact]
    public void WaterScenePaletteWriteBelongsToTheDolphinActorAndNotTheNearbyFallingZakCostume()
    {
        if (Open() is not { } game) return;
        var settings = ScummV1ActorColors.Read(game).Settings;
        Assert.Contains(settings, s => s.Source == "Script 50" && s.Offset == 0x1D && !s.IsCostume && s.Actor == 5 && s.Value == 11);
        Assert.Contains(settings, s => s.Source == "Script 50" && s.Offset == 0x28 && s.IsCostume && s.Actor == 1 && s.Value == 9);
        Assert.DoesNotContain(settings, s => s.Source == "Script 50" && !s.IsCostume && s.Actor == 1);
        Assert.Contains(settings, s => s.Source == "Room 31 / Entry script" && s.Offset == 0x1B && s.IsCostume && s.Actor == 5 && s.Value == 13);
        Assert.Contains(settings, s => s.Source == "Room 41 / Entry script" && s.Offset == 0x22 && s.IsCostume && s.Actor == 5 && s.Value == 13);
    }
}
