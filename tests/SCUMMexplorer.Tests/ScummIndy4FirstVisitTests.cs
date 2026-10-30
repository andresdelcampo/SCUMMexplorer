using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 Indy4 (17 October 2026): the rooms of Indiana Jones and the Fate of Atlantis (talkie CD) show the first visit the generic session runs (boot, a visit
/// that does not play the route, frames until the room settles); v5 objects store one image per state and the settled state picks it. Three script-heavy
/// rooms' settled object states are pinned. Record: <c>.verification/indy4-objects-20261017/README.md</c>. Skips when the disc image is absent.
/// </summary>
public sealed class ScummIndy4FirstVisitTests
{
    private static GameLibrary? Game()
    {
        var path = Path.Combine(TestGames.Root, "indy4 vga en");
        return File.Exists(Path.Combine(path, "ATLANTIS.cue")) ? new GameFolderScanner().Scan(path) : null;
    }

    private static int[] ActiveImages(ScummRoomScene scene) => scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Order().ToArray();

    [Fact]
    public void TheProfileGivesTheFirstVisit()
    {
        if (Game() is not { } game) return;
        Assert.True(ScummProfile.For(game).HasFirstVisitScenes);
    }

    [Fact]
    public void AnObjectWithSeveralStateImagesGivesOneImagePerState()
    {
        // Room 5's object 75 stores more than one state image (IM01, IM02): before S5 the room could not run (duplicate object id in the selection).
        if (Game() is not { } game) return;
        var data = Assert.IsType<ScummV5DataSource>(ScummDataSources.For(game));

        var first = data.ReadObjectImages(5);
        Assert.Equal(first.Count, first.Select(image => image.Id).Distinct().Count());
        var stateOne = first.Single(image => image.Id == 75);
        var stateTwo = data.ReadObjectImages(5, new Dictionary<int, byte> { [75] = 2 }).Single(image => image.Id == 75);
        Assert.NotEqual(stateOne.Pixels, stateTwo.Pixels);
        // A state with no image of its own draws nothing (ScummVM getObjectImage returns no image).
        Assert.DoesNotContain(data.ReadObjectImages(5, new Dictionary<int, byte> { [75] = 0x0F }), image => image.Id == 75);
    }

    [Theory]
    [InlineData(32, new[] { 354, 356, 357, 358, 359, 360, 363, 364, 365, 366, 367, 368, 369, 370 })]
    [InlineData(46, new[] { 547, 548, 549, 550, 562, 563, 564, 566, 568, 575 })]
    [InlineData(50, new[] { 598, 599, 610, 611, 612, 613, 615, 617, 624, 630, 631, 650 })]
    public void ARoomSettlesWithTheObjectsItsScriptsDraw(int room, int[] active)
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, room);

        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Equal(active, ActiveImages(scene));
        Assert.Empty(scene.Gaps());
    }

    [Fact]
    public void AnArrivalWalkWithNoRouteEndsWhereItStarts()
    {
        // 30 October 2026 (.verification/indy4-rooms-20261030): Room 76's catalog visit assumes the arrival through Room 77's door
        // (loadRoomWithEgo 0x0419, walk to (735, 126)), while the entry put the ego at object 0x040B in box 13, which has no route to box 17.
        // ScummVM's walkActor ends such a walk where it started; the ego and the camera stay known, so local script 214 (the parrot) is followed.
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 76);

        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Contains(scene.State.Actors, actor => actor.Number == 1 && actor.Room == 76 && actor.X == 50 && actor.WalkBox == 13);
        Assert.DoesNotContain(scene.Snapshot.Uncertainties, item => item.Contains("is unknown", StringComparison.Ordinal) && item.Contains("Camera", StringComparison.Ordinal));
        Assert.DoesNotContain(scene.Snapshot.Uncertainties, item => item.Contains("depends on var[0x002]", StringComparison.Ordinal));
    }
}
