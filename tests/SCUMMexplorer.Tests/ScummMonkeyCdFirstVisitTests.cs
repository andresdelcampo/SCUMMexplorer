using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 MonkeyCd (17 October 2026): the rooms of The Secret of Monkey Island CD show the first visit the generic session runs (boot, a visit that does not play
/// the route, frames until the room settles); three named rooms' settled object states are pinned. Record: <c>.verification/monkeycd-objects-20261017/README.md</c>.
/// Skips when the disc image is absent.
/// </summary>
public sealed class ScummMonkeyCdFirstVisitTests
{
    private static GameLibrary? Game()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 vga en");
        return File.Exists(Path.Combine(path, "The_Secret_of_Monkey_Island.cue")) ? new GameFolderScanner().Scan(path) : null;
    }

    private static int[] ActiveImages(ScummRoomScene scene) => scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Order().ToArray();

    [Fact]
    public void TheProfileGivesTheFirstVisitForTheCdRelease()
    {
        if (Game() is not { } game) return;
        Assert.True(ScummProfile.For(game).HasFirstVisitScenes);
    }

    [Theory]
    [InlineData(30, new[] { 390, 398, 399 })]
    [InlineData(37, new[] { 479, 485 })]
    [InlineData(70, new[] { 773, 776, 778, 781 })]
    public void ARoomSettlesWithTheObjectsItsScriptsDraw(int room, int[] active)
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, room);

        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Equal(active, ActiveImages(scene));
        Assert.Empty(scene.Gaps());
    }

    [Fact]
    public void TheScummBarRunsWithItsThirtyTwoColourPirates()
    {
        // Room 28's actors use 32-colour costumes (format 0x59); before S5 the room could not run.
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 28);
        var layer = scene.ActorLayer();

        Assert.Empty(scene.Gaps());
        Assert.Contains(layer.Actors, actor => actor.Costume == 24);
        Assert.Empty(layer.Skipped);
    }
}
