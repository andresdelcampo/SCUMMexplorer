using SCUMMexplorer.Core.Maniac;

namespace SCUMMexplorer.Tests;

public sealed class ScummV2LightingTests
{
    [Theory]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void NativeTorchPickupAndSwitchOnSelectsItsOwnMovingLightShape(string fixture)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out var indexed);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 28));
        session.Settle();
        session.ActivateObjectVerb(69, 14);
        session.ActivateObjectVerb(69, 4);
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 36));
        for (var frame = 0; frame < 120; frame++) session.AdvanceFrame();
        session.PointAtScreen((179, 79));
        var before = System.Text.Json.JsonSerializer.Serialize(session.Snapshot);
        var screen = new ScummPlayScreen(game, session, indexed.States);
        var capture = session.CaptureRoom();
        Assert.Equal(12, capture.Lights);
        Assert.Equal((9, 7), capture.Flashlight);
        Assert.Equal((179, 63), capture.RoomPointer);
        var first = screen.Scene()!.Compose();
        Assert.Contains(first, p => p != 0);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(session.Snapshot));
        session.PointAtScreen((250, 100));
        before = System.Text.Json.JsonSerializer.Serialize(session.Snapshot);
        var second = screen.Scene()!.Compose();
        Assert.False(first.SequenceEqual(second));
        // Rendering does not run scripts or change state; pointer input updates only its native mouse variables.
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(session.Snapshot));
        Assert.Contains(session.Snapshot.Uncertainties, note => note.Contains("activated directly"));
    }

    [Theory]
    [InlineData(1, 1440)]
    [InlineData(2, 1432)]
    public void NativeFlashlightCurvesHaveVersionSpecificNibbleEdges(int version, int litPixels)
    {
        var original = Enumerable.Repeat((byte)7, 320 * 136).ToArray();
        var shown = ScummV2Lighting.Compose(original, 320, 136, 4, (6, 4), version, (179, 79));
        Assert.Equal(litPixels, shown.Count(p => p == 7));
        // Mouse snaps to (176,78), so the 48x32 rectangle begins (152,62).
        Assert.Equal(0, shown[70 * 320 + 151]);
        Assert.Equal(7, shown[70 * 320 + 152]);
        Assert.Equal(0, shown[61 * 320 + 176]);
        Assert.Equal(7, shown[62 * 320 + 176]);
        // Top-right corner intentionally differs from top-left in the v1 source table.
        Assert.Equal(version == 1 ? 7 : 0, shown[62 * 320 + 192]);
        Assert.All(original, value => Assert.Equal(7, value));
    }

    [Fact]
    public void FlashlightMovesAsWholeRectangleAtViewportEdgesAndCameraOffset()
    {
        var background = Enumerable.Repeat((byte)5, 640 * 136).ToArray();
        var shown = ScummV2Lighting.Compose(background, 640, 136, 4, (6, 4), 2, (-13, -10), viewLeft: 160);
        Assert.Equal(5, shown[8 * 640 + 160]);
        Assert.Equal(0, shown[8 * 640 + 159]);
        Assert.Equal(0, shown[8 * 640 + 208]);
        Assert.Equal(1432, shown.Count(p => p == 5));
    }

    [Fact]
    public void RoomLightBitDominatesFlashlightAndDarknessNeverMutatesBackBuffer()
    {
        var background = Enumerable.Repeat((byte)3, 320 * 136).ToArray();
        Assert.Same(background, ScummV2Lighting.Compose(background, 320, 136, 11, (6, 4), 2, (160, 60)));
        Assert.All(ScummV2Lighting.Compose(background, 320, 136, 0, (6, 4), 2, (160, 60)), p => Assert.Equal(0, p));
        Assert.All(ScummV2Lighting.Compose(background, 320, 136, 4, (0, 4), 2, (160, 60)), p => Assert.Equal(0, p));
        Assert.Same(background, ScummV2Lighting.Compose(background, 320, 136, 0, (6, 4), 3, (160, 60)));
    }

    [Theory]
    [InlineData("mmansion ega en")]
    [InlineData("mmansionE ega es")]
    [InlineData("zak ega en")]
    [InlineData("zakE ega en")]
    public void AllEarlyProfilesInheritNativeFlashlightAndActorDarkColors(string fixture)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = (ScummV2DataSource)ScummDataSources.For(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), boot: false);
        Assert.Equal((6, 4), session.CaptureRoom().Flashlight);
        var costume = session.ReadCostume(1);
        Assert.NotNull(costume);
        var palette = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        Assert.Equal(8, costume.MapColor(2, palette, 1, lights: 0));
        Assert.Equal(game.V1Game is null ? 8 : 0, costume.MapColor(1, palette, 1, lights: 0));
        if (game.V1Game is null)
            Assert.Equal(costume.PaletteColor == 12 ? 8 : 0, costume.MapColor(12, palette, 1, lights: 0));
    }

    [Theory]
    [InlineData("mmansion ega en", 5)]
    [InlineData("mmansionE ega es", 5)]
    [InlineData("zak ega en", 36)]
    [InlineData("zakE ega en", 36)]
    public void OwnSourceDarkEntryHidesBackgroundButRetainsNativeActorLayer(string fixture, int room)
    {
        var folder = Path.Combine(TestGames.Root, fixture);
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var scene = fixture.StartsWith("mmansion") ? ManiacFirstVisit.Run(game, room) : ScummRoomFirstVisit.Run(game, room);
        Assert.False(scene.RoomLit);
        Assert.Contains(scene.Selection.Compose(scene.Room), p => p != 0);
        Assert.All(scene.Compose(), p => Assert.Equal(0, p));
        var actors = scene.ActorLayer();
        Assert.All(Enumerable.Range(0, actors.Pixels.Length).Where(actors.IsDrawn), i => Assert.Contains(actors.Pixels[i], new byte[] { 0, 8 }));
        var composed = scene.ComposeWithActors();
        Assert.All(Enumerable.Range(0, composed.Length), i => Assert.Equal(actors.IsDrawn(i) ? actors.Pixels[i] : 0, composed[i]));
    }
}
