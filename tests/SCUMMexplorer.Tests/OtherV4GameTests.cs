using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A v4 game that is not LOOM CD is browsed from all its disks. Set <c>SCUMM_MONKEY1_VGA_TEST_ROOT</c> (and, for the 16-colour copy,
/// <c>SCUMM_MONKEY1_EGA_TEST_ROOT</c>) to the absolute folders (<c>Games/FUTURE/monkey1/VGA</c>, <c>…/EGA</c>); the checks are skipped without them.
/// </summary>
public class OtherV4GameTests
{
    [Fact]
    public void BrowsesEveryDiskOfAnotherV4GameWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_MONKEY1_VGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Unknown, game.Id);
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.NotNull(game.V4Disks);
        Assert.Null(game.V4Archive);
        Assert.Equal(4, game.V4Disks.Disks.Count);
        // Every room is on exactly one disk, and the resource says which: its decoded background is a valid picture.
        var rooms = game.Rooms.ToArray();
        Assert.Equal(rooms.Length, rooms.Select(r => r.ResourceId).Distinct().Count());
        Assert.All(rooms.Where(r => r.ResourceId is 10 or 33 or 45 or 1), room =>
        {
            var archive = game.V4ArchiveFor(room);
            Assert.NotNull(archive);
            Assert.True(archive.DecodeRoom(room.ResourceId!.Value).IsValid);
        });
        Assert.NotEmpty(game.Resources.Where(r => r.Kind == ResourceKind.Costume));
    }

    [Fact]
    public void OffersNothingForTheSixteenColourVersionWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_MONKEY1_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.Empty(game.Resources);
        Assert.Null(game.V4Disks);
    }
}
