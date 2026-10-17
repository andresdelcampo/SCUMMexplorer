using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 Monkey2 (17 October 2026): the rooms of Monkey Island 2 (fixture <c>mi2 talkie en</c>) show the first visit the generic session runs (boot, a visit that
/// does not play the route, frames until the room settles); three named rooms' settled object states are pinned. Room 55 has 25 objects drawn by its scripts,
/// each with several stored state images (IM01..), so it also pins the image-per-state choice. Record: <c>.verification/monkey2-objects-20261017/README.md</c>.
/// Skips without the data.
/// </summary>
public sealed class ScummMonkey2FirstVisitTests
{
    private static GameLibrary? Game()
    {
        var path = Path.Combine(TestGames.Root, "mi2 talkie en");
        return File.Exists(Path.Combine(path, "monkey2.000")) || File.Exists(Path.Combine(path, "MONKEY2.000")) ? new GameFolderScanner().Scan(path) : null;
    }

    private static int[] ActiveImages(ScummRoomScene scene) => scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Order().ToArray();

    [Fact]
    public void TheProfileGivesTheFirstVisit()
    {
        if (Game() is not { } game) return;
        Assert.True(ScummProfile.For(game).HasFirstVisitScenes);
    }

    [Fact]
    public void ObjectImagesLeaveTheRoomsTransparentColourUndrawn()
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 55);

        // The party guests of Room 55 are coded with transparent strips (gdi.cpp transpCheck): the room shows through around them.
        var guest = scene.Images.Single(image => image.Id == 615);
        Assert.NotNull(guest.Transparent);
        Assert.Contains(true, guest.Transparent!);
        var composed = scene.Selection.Compose(scene.Room);
        var hole = Array.IndexOf(guest.Transparent!, true);
        var (x, y) = (guest.X + hole % guest.Width, guest.Y + hole / guest.Width);
        Assert.NotEqual(guest.Pixels[hole], composed[y * scene.Room.Width + x]);
    }

    [Theory]
    [InlineData(39, new[] { 391, 409 })]
    [InlineData(55, new[] { 615, 616, 617, 618, 619, 620, 621, 622, 623, 624, 625, 626, 627, 628, 629, 630, 631, 632, 633, 634, 635, 636, 637, 638, 647 })]
    [InlineData(79, new[] { 772, 776 })]
    public void ARoomSettlesWithTheObjectsItsScriptsDraw(int room, int[] active)
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, room);

        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Equal(active, ActiveImages(scene));
        Assert.Empty(scene.Gaps());
        Assert.Contains(scene.State.Actors, actor => actor.Number == 1 && actor.Room == room);
        // One image per object: the views key on the object id.
        Assert.Equal(scene.Images.Count, scene.Images.Select(image => image.Id).Distinct().Count());
    }
}
