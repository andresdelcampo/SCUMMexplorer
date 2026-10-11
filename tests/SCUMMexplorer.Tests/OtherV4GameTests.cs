using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit;

namespace SCUMMexplorer.Tests;

/// <summary>
/// A v4 game that is not LOOM CD is browsed from all its disks. Set <c>SCUMM_MONKEY1_VGA_TEST_ROOT</c> (and, for the 16-colour copy,
/// <c>SCUMM_MONKEY1_EGA_TEST_ROOT</c>; for the 256-colour Indiana Jones, <c>SCUMM_INDY3_VGA_TEST_ROOT</c>) to the absolute folders (<c>Games/FUTURE/monkey1/VGA</c>, <c>…/EGA</c>); the checks are skipped without them.
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
        Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Costume);
        // Every sound is an SO block with both an AdLib and a PC speaker part, across all four disks.
        var sounds = game.V4Disks.Disks.SelectMany(d => d.ReadSounds()).ToArray();
        Assert.Equal(90, sounds.Length);
        Assert.All(sounds, sound => { Assert.NotNull(sound.Adlib); Assert.NotNull(sound.Speaker); });
    }

    [Fact]
    public void BrowsesTheRoomFilesOfTheSuppliedIndianaJones256ColourGameWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_VGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(GameId.Unknown, game.Id);
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.NotNull(game.V4Disks);
        var disk = Assert.Single(game.V4Disks.Disks);
        // The 87 room files whose picture decodes, through the four older 256-colour strip codes (2, 3, 4 and 7) as well as raw and bit-coded strips.
        var rooms = game.Rooms.ToArray();
        Assert.Equal(87, rooms.Length);
        Assert.All(rooms, room => Assert.True(disk.DecodeRoom(room.ResourceId!.Value).IsValid));
        Assert.Contains(game.Resources, r => r.Kind == ResourceKind.Costume);
        Assert.DoesNotContain(rooms, room => room.ResourceId is 98 or 99);
        // The sounds sit in a second SO block inside the first; the parts are found through it.
        var sounds = disk.ReadSounds();
        Assert.Equal(73, sounds.Count);
        Assert.All(sounds, sound => Assert.NotNull(sound.Adlib));
    }

    [Fact]
    public void BrowsesTheSixteenColourVersionWhenConfigured()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_MONKEY1_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(ScummVersion.V4, game.Version);
        Assert.NotNull(game.V4Disks);
        Assert.Equal(8, game.V4Disks.Disks.Count);
        // The rooms have no PA block: the fixed EGA palette (16 colours) and the old-v3 strip code.
        var rooms = game.Rooms.ToArray();
        Assert.NotEmpty(rooms);
        Assert.All(rooms.Where(r => r.ResourceId is 10 or 33 or 34), room =>
        {
            var image = game.V4ArchiveFor(room)!.DecodeRoom(room.ResourceId!.Value);
            Assert.True(image.IsValid);
            Assert.All(image.Pixels.ToArray(), pixel => Assert.True(pixel < 16));
        });
    }
}
