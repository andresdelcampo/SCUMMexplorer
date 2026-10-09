using System.Text;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

public sealed class LoomDirectSpeakerTests
{
    [Theory]
    [InlineData("loom ega mt32 en", "Hetchel", 5, 0x4D)]
    [InlineData("loom ega mt32 en", "Dead One", 4, 0x56)]
    [InlineData("loom vga en", "Hetchel", 5, 0x4D)]
    [InlineData("loom vga en", "Hetchel", 5, 0x4F)]
    [InlineData("loom vga en", "Hetchel", 5, 0x51)]
    [InlineData("loom vga en", "Hetchel", 5, 0x56)]
    [InlineData("loom vga en", "Dead One", 4, 0x56)]
    public void ADirectResponseGetsItsMissingActorFromItsOwnEdition(string folder, string name, int actor, int verb)
    {
        var game = new GameFolderScanner().Scan(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", folder)));
        var data = ScummDataSources.For(game);
        var room = data.ReadRoom(9);
        var target = room.Objects.Single(o => room.ObjectNames.TryGetValue(o.Id, out var bytes) && Encoding.Latin1.GetString(bytes).Trim() == name);
        var setup = new LoomSceneSetupPlanner(game).DeriveActors(9, target.Id, verb);
        var step = Assert.Single(setup.Steps, s => s.Kind == "actor" && s.ObjectId == actor);
        Assert.All(setup.Steps, s => Assert.Equal("actor", s.Kind));
        var source = Assert.Single(LoomSceneStateScanner.For(game).FindActorPlacers(actor, 9), p => p.Where == step.EndBoundary);
        Assert.NotNull(source.Dress?.Costume);
        Assert.Contains(setup.Notes, n => n.Contains(source.Where, StringComparison.Ordinal) && n.Contains("estimate", StringComparison.Ordinal));
        var indexed = game.Vga?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
        var session = new LoomExplorationSession(game, indexed);
        session.VisitRoomById(9);
        LoomCdSceneSetupPlanner.Settle(session);
        var status = session.GameStatus;
        var variables = session.Snapshot.KnownGlobalVariables.ToDictionary();
        session.PlaceActorInRoom(step.ObjectId, step.X, step.Y, step.Verb, step.EndBoundary!);
        var shown = session.Actors.Single(a => a.Number == actor);
        Assert.True(shown.Visible);
        Assert.Equal(9, shown.Room);
        Assert.Equal(source.Dress!.Costume, shown.Look.Costume);
        var layer = LoomActorRenderer.Render([shown], 9, room.Width, room.Height, data.ReadCostume);
        Assert.True(Assert.Single(layer.Actors).Pixels > 0);
        Assert.Equal(status.AvailableNoteCount, session.GameStatus.AvailableNoteCount);
        Assert.Equal(status.StaffHeld, session.GameStatus.StaffHeld);
        Assert.Equal(variables.OrderBy(p => p.Key), session.Snapshot.KnownGlobalVariables.OrderBy(p => p.Key));
        // Applying the same preparation to an actor already present must retain source-earned look and position.
        session.PlaceActorInRoom(step.ObjectId, step.X + 10, step.Y + 10, step.Verb, step.EndBoundary!);
        var retained = session.Actors.Single(a => a.Number == actor);
        Assert.Equal((shown.Room, shown.X, shown.Y, shown.Visible, shown.Look.Costume, shown.Look.Frame, shown.Look.Facing, shown.Estimate),
            (retained.Room, retained.X, retained.Y, retained.Visible, retained.Look.Costume, retained.Look.Frame, retained.Look.Facing, retained.Estimate));
        Assert.Equal(shown.Look.Palette, retained.Look.Palette);
        session.ActivateObjectVerb(target.Id, verb);
        LoomCdSceneSetupPlanner.Settle(session);
        Assert.False(session.InCutscene);
    }
}
